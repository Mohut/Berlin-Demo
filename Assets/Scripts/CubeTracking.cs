using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class CubeTracking : MonoBehaviour
{
    public ARTrackedImageManager trackedImageManager;
    public ARAnchorManager anchorManager;
    public GameObject redCubePrefab;

    private GameObject instantiatedCube;             
    private GameObject[] instantiatedSpheres;        
    private ARAnchor cubeAnchor;                     
    private ARAnchor[] sphereAnchors;                

    void OnEnable()
    {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    void OnDisable()
    {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    // Called when tracked images are added, updated, or removed
    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            // Spawn the cube and spheres when the image is detected
            SpawnCube(trackedImage.transform.position, trackedImage.transform.rotation);
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.None)
            {
                // When tracking is lost, the objects will stay in place due to anchors
            }
            else if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
            {
                // Re-instantiate the objects at the new position if the image is detected again
                SpawnCube(trackedImage.transform.position, trackedImage.transform.rotation);
            }
        }
    }

    // Spawns the cube and spheres relative to the tracked image's position
    private void SpawnCube(Vector3 position, Quaternion rotation)
    {
        // Clean up previous objects and anchors
        if (instantiatedCube != null)
        {
            Destroy(instantiatedCube);
        }
        
        if (cubeAnchor != null)
        {
            Destroy(cubeAnchor.gameObject);
            foreach (var anchor in sphereAnchors)
            {
                Destroy(anchor.gameObject);
            }
        }

        // Instantiate the cube at the tracked image position and anchor it
        instantiatedCube = Instantiate(redCubePrefab, position, rotation);
        cubeAnchor = anchorManager.AddAnchor(new Pose(position, rotation));
        if (cubeAnchor != null)
        {
           instantiatedCube.transform.SetParent(cubeAnchor.transform, true); // Attach the cube to the anchor
        }
    }
}