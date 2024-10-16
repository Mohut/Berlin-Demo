using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ImageTrackingWithAnchoredObjects : MonoBehaviour
{
    public ARTrackedImageManager trackedImageManager; // Manages tracked images
    public ARAnchorManager anchorManager;             // Manages anchors in world space
    public GameObject redCubePrefab;                  // Prefab for the red cube
    public GameObject redSpherePrefab;                // Prefab for the red spheres

    private GameObject instantiatedCube;              // Reference to the instantiated cube
    private GameObject[] instantiatedSpheres;         // Reference to the instantiated spheres
    private ARAnchor cubeAnchor;                      // Anchor for the cube
    private ARAnchor[] sphereAnchors;                 // Anchors for the spheres

    // Positions where the spheres will spawn relative to the red cube
    public Vector3[] sphereRelativePositions = {
        new Vector3(0.5f, 0, 0),    // Sphere 1
        new Vector3(0, 0, 0.5f),    // Sphere 2
        new Vector3(0, 0.5f, 0)     // Sphere 3
    };

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
            SpawnCubeAndSpheres(trackedImage.transform.position, trackedImage.transform.rotation);
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
                SpawnCubeAndSpheres(trackedImage.transform.position, trackedImage.transform.rotation);
            }
        }
    }

    // Spawns the cube and spheres relative to the tracked image's position
    private void SpawnCubeAndSpheres(Vector3 position, Quaternion rotation)
    {
        // Clean up previous objects and anchors
        if (instantiatedCube != null)
        {
            Destroy(instantiatedCube);
            foreach (var sphere in instantiatedSpheres)
            {
                Destroy(sphere);
            }
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

        // Instantiate the spheres at positions relative to the cube and anchor them
        instantiatedSpheres = new GameObject[sphereRelativePositions.Length];
        sphereAnchors = new ARAnchor[sphereRelativePositions.Length];
        for (int i = 0; i < sphereRelativePositions.Length; i++)
        {
            Vector3 spherePosition = position + sphereRelativePositions[i]; // Calculate position relative to the cube
            instantiatedSpheres[i] = Instantiate(redSpherePrefab, spherePosition, Quaternion.identity);

            // Create an anchor for each sphere
            sphereAnchors[i] = anchorManager.AddAnchor(new Pose(spherePosition, Quaternion.identity));
            if (sphereAnchors[i] != null)
            {
               // instantiatedSpheres[i].transform.SetParent(sphereAnchors[i].transform, true);
                instantiatedSpheres[i].transform.SetParent(cubeAnchor.transform, false);// Attach sphere to its anchor
            }
        }
    }
}
