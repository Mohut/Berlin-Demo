using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.Collections;
using Unity.VisualScripting;

public class PointCloudAnchor : MonoBehaviour
{
    public ARPointCloudManager pointCloudManager;
    public ARAnchorManager arAnchorManager;
    public GameObject anchorPrefab; // Prefab to use as the visual representation of the anchor

    private void OnEnable()
    {
        pointCloudManager.pointCloudsChanged += OnPointCloudsChanged;
    }

    private void OnDisable()
    {
        pointCloudManager.pointCloudsChanged -= OnPointCloudsChanged;
    }

    private void OnPointCloudsChanged(ARPointCloudChangedEventArgs eventArgs)
    {
        foreach (var pointCloud in eventArgs.updated)
        {
            AddAnchorsToFeaturePoints(pointCloud);
        }
    }

    private void AddAnchorsToFeaturePoints(ARPointCloud pointCloud)
    {
        // Ensure positions are not null before proceeding
        if (pointCloud.positions.HasValue)
        {
            NativeSlice<Vector3> points = pointCloud.positions.Value;
            // Example: Create an anchor at the first feature point
            if (points.Length <= 0) return;
            
            Vector3 featurePoint = points[0];
            CreateAnchor(featurePoint);
        }
    }

    private void CreateAnchor(Vector3 position)
    {
        var anchor = new GameObject("ARAnchor").AddComponent<ARAnchor>();
        anchor.transform.position = position;
        if (anchorPrefab != null)
        {
            //Instantiate(anchorPrefab, position, Quaternion.identity);
        }
    }
    
    private void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                Debug.Log("Touched");
                
                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    // Instantiate prefab at the hit point
                    GameObject instantiatedPrefab = Instantiate(anchorPrefab, hit.point, Quaternion.identity);

                    // Create AR anchor at the hit point
                    ARAnchor anchor = arAnchorManager.AddComponent<ARAnchor>();
                    instantiatedPrefab.transform.parent = anchor.transform;
                }
                else
                {
                    Debug.Log("No Hit");
                }
            }
        }
    }
}