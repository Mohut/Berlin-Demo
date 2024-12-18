using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Sentis;
using UnityEngine.UI;

public class YoloWithWebcam : MonoBehaviour
{
    [SerializeField] private ModelAsset modelAsset; // The model asset for YOLO
    [SerializeField] private RectTransform boxParent; // Parent transform for bounding boxes
    [SerializeField] private RawImage rawImage; // UI element to display webcam feed

    private Worker worker; // Worker for processing the model
    private Tensor<float> inputTensor; // Input tensor for the model
    private Tensor<float> outputTensor; // Output tensor from the model
    private Tensor<float> cpuCopyTensor; // Copy of the output tensor for CPU processing

    // Lists to store detected bounding boxes, scores, classes, and selected indices
    List<Rect> boxes = new List<Rect>();
    List<float> scores = new List<float>();
    List<float> classes = new List<float>();
    List<int> selectedIndices = new List<int>();
    List<int> indices = new List<int>();
    List<int> nonMaxSupressionList = new List<int>();

    const int k_LayersPerFrame = 10; // Number of layers to process per frame
    IEnumerator m_Schedule; // Coroutine for scheduling the model
    bool m_Started = false; // Flag to indicate if processing has started

    private WebCamTexture webCamTexture; // Texture for webcam feed

    private void Start()
    {
        // Initialize the webcam
        webCamTexture = new WebCamTexture();
        rawImage.texture = webCamTexture;
        webCamTexture.Play();
        Debug.Log("Webcam started.");

        // Initialize YOLO model
        Model runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.CPU);
        inputTensor = new Tensor<float>(new TensorShape(1, 3, 640, 640));
        Debug.Log("YOLO model initialized.");
    }

    private void Update()
    {
        if (webCamTexture.width < 100) // Check if the webcam is initialized
        {
            Debug.Log("Webcam not initialized yet.");
            return;
        }

        Debug.Log($"Webcam initialized. Width: {webCamTexture.width}, Height: {webCamTexture.height}");

        // Convert webcam image to tensor
        TextureConverter.ToTensor(webCamTexture, inputTensor, new TextureTransform());
        Debug.Log($"Input Tensor Shape: {inputTensor.shape}");

        if (!m_Started) // Start processing if not already started
        {
            m_Schedule = worker.ScheduleIterable(inputTensor);
            m_Started = true;
        }

        int it = 0;
        while (m_Schedule.MoveNext())
        {
            if (++it % k_LayersPerFrame == 0)
                return;
        }

        // Get the output tensor
        outputTensor = worker.PeekOutput() as Tensor<float>;
        Debug.Log($"Output Tensor Shape: {outputTensor.shape}");

        // Read back output tensor data
        cpuCopyTensor = outputTensor.ReadbackAndClone();
        m_Started = false;

        // Process YOLO output
        ProcessYoloOutput(cpuCopyTensor);

        // Dispose tensors to free resources
        cpuCopyTensor.Dispose();
        outputTensor.Dispose();
        inputTensor.Dispose();
    }

    private void OnDestroy()
    {
        // Stop the webcam and dispose of tensors and workers
        webCamTexture.Stop();
        inputTensor?.Dispose();
        cpuCopyTensor?.Dispose();
        outputTensor?.Dispose();
        worker?.Dispose();
        Debug.Log("Resources disposed and webcam stopped.");
    }

    void ProcessYoloOutput(Tensor<float> outputTensor)
    {
        boxes.Clear();
        scores.Clear();
        classes.Clear();
        nonMaxSupressionList.Clear();

        float confidenceThreshold = 0.3f; // Adjust threshold as needed
        float inputWidth = 640f; // Model input width
        float inputHeight = 640f; // Model input height

        float webcamWidth = webCamTexture.width;
        float webcamHeight = webCamTexture.height;

        Debug.Log($"Webcam Dimensions: Width: {webcamWidth}, Height: {webcamHeight}");

        // Assuming outputTensor shape is (1, N, 85) for YOLOv8 (where N is number of detections)
        int numDetections = outputTensor.shape[1]; // Get the number of detections
        for (int i = 0; i < numDetections; i++) // Iterate over detections
        {
            float confidence = outputTensor[0, i, 4]; // Confidence score

            if (confidence > confidenceThreshold)
            {
                // Extract bounding box values
                float x_center = outputTensor[0, i, 0] * inputWidth; // Center X
                float y_center = outputTensor[0, i, 1] * inputHeight; // Center Y
                float boxWidth = outputTensor[0, i, 2] * inputWidth;  // Width
                float boxHeight = outputTensor[0, i, 3] * inputHeight; // Height
                int classId = (int)outputTensor[0, i, 5]; // Class index

                // Log the raw output values for debugging
                Debug.Log($"Raw YOLO Output: Center({x_center}, {y_center}), Size({boxWidth}, {boxHeight}), Confidence: {confidence}, Class: {classId}");

                // Calculate top-left corner
                float x_min = x_center - (boxWidth / 2);
                float y_min = y_center - (boxHeight / 2);

                // Ensure that the coordinates are not negative
                x_min = Mathf.Max(0, x_min);
                y_min = Mathf.Max(0, y_min);

                // Ensure that the bounding box does not exceed the dimensions of the webcam
                boxWidth = Mathf.Max(0, Mathf.Min(boxWidth, webcamWidth - x_min));
                boxHeight = Mathf.Max(0, Mathf.Min(boxHeight, webcamHeight - y_min));

                // Add to the list only if the width and height are valid
                if (boxWidth > 0 && boxHeight > 0)
                {
                    boxes.Add(new Rect(x_min, webcamHeight - y_min - boxHeight, boxWidth, boxHeight)); // Invert y-axis for UI
                    scores.Add(confidence);
                    classes.Add(classId);
                    Debug.Log($"Detected box: {new Rect(x_min, y_min, boxWidth, boxHeight)} with confidence: {confidence}");
                }
                else
                {
                    Debug.LogWarning($"Invalid box dimensions: Width: {boxWidth}, Height: {boxHeight} for center ({x_center}, {y_center})");
                }
            }
        }

        Debug.Log($"Detected Boxes Count: {boxes.Count}");

        // Apply Non-Maximum Suppression (NMS)
        NonMaxSuppression(boxes, scores, 0.5f, out nonMaxSupressionList);

        // Clear previous bounding boxes from the UI
        foreach (Transform child in boxParent)
        {
            Destroy(child.gameObject);
        }

        // Draw the bounding boxes
        foreach (int index in selectedIndices)
        {
            Debug.Log($"Drawing box for index: {index}");
            DrawBoundingBox(boxes[index], scores[index]);
        }
    }


    void DrawBoundingBox(Rect box, float score)
    {
        // Create a GameObject to represent the bounding box
        GameObject boxObj = new GameObject("BoundingBox");
        RectTransform rectTransform = boxObj.AddComponent<RectTransform>();

        // Set the parent for UI hierarchy
        rectTransform.SetParent(boxParent, false);

        // Set the size and position
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.sizeDelta = new Vector2(box.width, box.height);
        rectTransform.anchoredPosition = new Vector2(box.x, -box.y); // Invert y for UI space

        // Add an Image component to visualize the box
        var image = boxObj.AddComponent<Image>();
        image.color = new Color(1, 0, 0, 0.5f); // Semi-transparent red

        // Add a label for the score
        var textObj = new GameObject("ScoreLabel");
        var textTransform = textObj.AddComponent<RectTransform>();
        textTransform.SetParent(boxParent, false);
        textTransform.anchoredPosition = rectTransform.anchoredPosition + new Vector2(0, box.height / 2 + 10);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = $"Score: {score:F2}";
        text.fontSize = 14;
        text.color = Color.black;
    }

    private void NonMaxSuppression(List<Rect> boxes, List<float> scores, float iouThreshold, out List<int> nonMaxSupressionListReference)
    {
        selectedIndices.Clear();
        indices.Clear();

        for (int i = 0; i < boxes.Count; i++)
            indices.Add(i);

        indices.Sort((a, b) => scores[b].CompareTo(scores[a])); // Sort by descending scores

        while (indices.Count > 0)
        {
            int currentIndex = indices[0];
            selectedIndices.Add(currentIndex);
            indices.RemoveAt(0);

            for (int i = indices.Count - 1; i >= 0; i--)
            {
                int index = indices[i];
                float iou = CalculateIoU(boxes[currentIndex], boxes[index]);
                if (iou > iouThreshold)
                    indices.RemoveAt(i);
            }
        }

        nonMaxSupressionListReference = selectedIndices;
    }

    private float CalculateIoU(Rect boxA, Rect boxB)
    {
        float x1 = Mathf.Max(boxA.x, boxB.x);
        float y1 = Mathf.Max(boxA.y, boxB.y);
        float x2 = Mathf.Min(boxA.xMax, boxB.xMax);
        float y2 = Mathf.Min(boxA.yMax, boxB.yMax);

        float intersection = Mathf.Max(0, x2 - x1) * Mathf.Max(0, y2 - y1);
        float areaA = boxA.width * boxA.height;
        float areaB = boxB.width * boxB.height;

        return intersection / (areaA + areaB - intersection);
    }
}
