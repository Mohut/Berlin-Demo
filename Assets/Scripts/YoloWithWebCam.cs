using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Sentis;
using UnityEngine.UI;

public class YoloWithWebCam : MonoBehaviour
{
    [SerializeField] private ModelAsset modelAsset;
    [SerializeField] private RectTransform boxParent;
    [SerializeField] private RawImage rawImage; // Display for webcam feed

    private Worker worker;
    private WebCamTexture webCamTexture;

    private void Start()
    {
       
        // Initialize YOLO model
        Model runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);

        // Initialize the webcam
        webCamTexture = new WebCamTexture();
        rawImage.texture = webCamTexture;
        rawImage.material.mainTexture = webCamTexture;

        
        // Start the webcam feed
        webCamTexture.Play();

        Debug.Log("width and height of webcam texture" + " " + webCamTexture.width + " " + webCamTexture.height);
    }

    private void Update()
    {
        // Run inference on each frame from the webcam
        if (webCamTexture.didUpdateThisFrame)
        {

            //Texture2D resizedTexture = ResizeTexture(webCamTexture, 640, 640);

            // Convert the webcam frame to Tensor
            Tensor<float> inputTensor = TextureConverter.ToTensor(webCamTexture, width: 640, height: 640, channels: 3 );


            //inputTensor.Reshape(new TensorShape(1, 3, 640, 640 ));

            // Run the YOLO model on the frame
            worker.Schedule(inputTensor);
            Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;

            // Process and display the bounding boxes
            ProcessYoloOutput(outputTensor.ReadbackAndClone());

            Debug.Log(outputTensor.shape);
            // Clean up
            outputTensor.Dispose();
            inputTensor.Dispose();
        }
    }



    private void OnDestroy()
    {
        // Clean up resources when closing the app
        if (worker != null)
        {
            worker.Dispose();
        }

        if (webCamTexture != null)
        {
            webCamTexture.Stop();
        }
    }

    void ProcessYoloOutput(Tensor<float> outputTensor)
    {
        // Extract bounding boxes and confidence scores
        List<Rect> boxes = new List<Rect>();
        List<float> scores = new List<float>();
        List<float> classes = new List<float>();

        float confidenceThreshold = 0.5f;

        for (int i = 0; i < 8400; i++)
        {
            float confidence = outputTensor[0, 4, i];

            if (confidence > confidenceThreshold)
            {
                float x_center = outputTensor[0, 0, i]; // x_center
                float y_center = outputTensor[0, 1, i]; // y_center
                float width = outputTensor[0, 2, i];     // width
                float height = outputTensor[0, 3, i];    // height

                // Calculate top-left corner
                float x_min = x_center - (width / 2);
                float y_min = y_center - (height / 2);

                // Create a bounding box
                boxes.Add(new Rect(x_min, y_min, width, height));
                scores.Add(confidence);
                classes.Add(outputTensor[0, 5, i]);
            }
        }

        // Apply Non-Maximum Suppression (NMS)
        List<int> selectedIndices = NonMaxSuppression(boxes, scores, 0.5f);

        // Clear previous bounding boxes
        foreach (Transform child in boxParent)
        {
            Destroy(child.gameObject);
        }

        // Draw the bounding boxes
        foreach (int index in selectedIndices)
        {
            DrawBoundingBox(boxes[index], scores[index]);
        }

  

        outputTensor.Dispose();
    }

    void DrawBoundingBox(Rect box, float score)
    {
        // Create a GameObject to represent the bounding box
        GameObject boxObj = new GameObject("BoundingBox");
        RectTransform rectTransform = boxObj.AddComponent<RectTransform>();

        // Make sure the bounding box is within the canvas space
        rectTransform.SetParent(boxParent, false);

        // Set the size and position
        rectTransform.sizeDelta = new Vector2(box.width, box.height);
        rectTransform.anchoredPosition = new Vector2(box.x, -box.y);

        // Optionally add a UI Image component to visualize the box
        var image = boxObj.AddComponent<Image>();
        image.color = new Color(1, 0, 0, 0.5f); // Semi-transparent red

        // Optionally, add a label for the score
        var textObj = new GameObject("ScoreLabel");
        var textTransform = textObj.AddComponent<RectTransform>();
        textTransform.SetParent(boxParent, false);
        textTransform.anchoredPosition = rectTransform.anchoredPosition + new Vector2(0, box.height / 2 + 10);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = $"Score: {score:F2}";
        text.fontSize = 14;
        text.color = Color.black;
    }

    List<int> NonMaxSuppression(List<Rect> boxes, List<float> scores, float iouThreshold)
    {
        List<int> selectedIndices = new List<int>();
        List<int> indices = new List<int>(boxes.Count);
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
        return selectedIndices;
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

    // Example of ResizeTexture function
    Texture2D ResizeTexture(WebCamTexture source, int newWidth, int newHeight)
    {
        Texture2D result = new Texture2D(newWidth, newHeight);
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
}
