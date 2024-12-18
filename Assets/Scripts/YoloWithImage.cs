using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Sentis;
using UnityEngine.UI;

public class YoloWithImage : MonoBehaviour
{
    [SerializeField] private ModelAsset modelAsset;
    [SerializeField] private RectTransform boxParent;
    [SerializeField] private RawImage rawImage;
    
    private Worker worker;
    private Tensor<float> inputTensor;
    private Tensor<float> outputTensor;
    private Tensor<float> cpuCopyTensor;
    
    List<Rect> boxes = new List<Rect>();
    List<float> scores = new List<float>();
    List<float> classes = new List<float>();
    List<int> selectedIndices = new List<int>();
    List<int> indices = new List<int>();
    List<int> nonMaxSupressionList = new List<int>();

    const int k_LayersPerFrame = 10;
    IEnumerator m_Schedule;
    bool m_Started = false;
    
    private void Start()
    {
        //  rawImage.texture.height = Screen.height;
        //  rawImage.texture.width = Screen.width;
        //  Initialize YOLO model

        rawImage.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);
        rawImage.rectTransform.anchorMin = new Vector2(0, 0);
        rawImage.rectTransform.anchorMax = new Vector2(1, 1);
        rawImage.rectTransform.offsetMin = Vector2.zero;
        rawImage.rectTransform.offsetMax = Vector2.zero;


        Model runtimeModel = ModelLoader.Load(modelAsset);

        worker = new Worker(runtimeModel, BackendType.CPU);
        inputTensor = new Tensor<float>(new TensorShape(1, 3, 640, 640));
    }

    private void Update()
    {
        // This line
        TextureConverter.ToTensor(rawImage.texture, inputTensor, new TextureTransform());   
        
        if (!m_Started)
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
        
        // This line
        outputTensor = worker.PeekOutput() as Tensor<float>;
        
        cpuCopyTensor = outputTensor.ReadbackAndClone();
        m_Started = false;
        
        ProcessYoloOutput(cpuCopyTensor);
        
        cpuCopyTensor.Dispose();
        outputTensor.Dispose();
        inputTensor.Dispose();


        if (Screen.orientation == ScreenOrientation.LandscapeLeft || Screen.orientation == ScreenOrientation.Portrait)
        {
            AdjustAspectRatio();
        }
    }

    private void OnDestroy()
    {
        inputTensor?.Dispose();
        cpuCopyTensor?.Dispose();
        outputTensor?.Dispose();
        worker?.Dispose();
    }

    void ProcessYoloOutput(Tensor<float> outputTensor)
    {
        boxes.Clear();
        scores.Clear();
        classes.Clear();
        nonMaxSupressionList.Clear();
        
        float confidenceThreshold = 0.5f;

        for (int i = 0; i < 8400; i++)
        {
            float confidence = outputTensor[0, 4, i];

            if (confidence > confidenceThreshold)
            {
                float x_center = outputTensor[0, 0, i];  // x_center
                float y_center = outputTensor[0, 1, i];  // y_center
                float width = outputTensor[0, 2, i];     // width
                float height = outputTensor[0, 3, i];    // height

                // Calculate top-left corner
                float x_min = x_center - (width / 2);
                float y_min = y_center - (height / 2);

                // Create a bounding box
                boxes.Add(new Rect(x_min, y_min, width, height));
                scores.Add(confidence);
                classes.Add(outputTensor[0, 5, i]);
                Debug.Log(outputTensor.shape);
            }
        }

        // Apply Non-Maximum Suppression (NMS)
        NonMaxSuppression(boxes, scores, 0.5f, out nonMaxSupressionList);

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
        // Map the bounding box coordinates to the screen space
        float scaleX = rawImage.rectTransform.rect.width / 640f; // Assuming model input is 640x640
        float scaleY = rawImage.rectTransform.rect.height / 640f;

        float x = box.x * scaleX;
        float y = box.y * scaleY;
        float width = box.width * scaleX;
        float height = box.height * scaleY;

        GameObject boxObj = new GameObject("BoundingBox");
        RectTransform rectTransform = boxObj.AddComponent<RectTransform>();
        rectTransform.SetParent(boxParent, false);

        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);

        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.anchoredPosition = new Vector2(x, -y);

        var image = boxObj.AddComponent<Image>();
        image.color = new Color(1, 0, 0, 0.5f);

        var textObj = new GameObject("ScoreLabel");
        var textTransform = textObj.AddComponent<RectTransform>();
        textTransform.SetParent(boxParent, false);
        textTransform.anchoredPosition = rectTransform.anchoredPosition + new Vector2(0, height / 2 + 10);

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

    void AdjustAspectRatio()
    {
        float screenAspect = (float)Screen.width / Screen.height;
        float inputAspect = 640f / 640f; // YOLO input resolution

        if (screenAspect > inputAspect)
        {
            rawImage.rectTransform.sizeDelta = new Vector2(Screen.height * inputAspect, Screen.height);
        }
        else
        {
            rawImage.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.width / inputAspect);
        }
    }

}
