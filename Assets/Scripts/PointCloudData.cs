using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
public class PointCloudData : MonoBehaviour
{
    [SerializeField] private ARPointCloudManager pointCloudManager;
    [SerializeField] private ARPointCloud pointCloud;
    [SerializeField] private TMP_Text testText;
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private GameObject pointCloudCubePrefab;
    private NativeSlice<Vector3> points = new NativeSlice<Vector3>();
    private List<GameObject> cubes = new List<GameObject>();
    
    private void OnEnable()
    {
        pointCloudManager.pointCloudsChanged += UpdatePointCloud;
    }

    private void OnDisable()
    {
        pointCloudManager.pointCloudsChanged -= UpdatePointCloud;
    }

    private void Update()
    {
        if (Input.touchCount <= 0) return;
        
        if (Input.touches[0].phase == TouchPhase.Began)
        {
            Touch touch = Input.GetTouch(0);
            //LookForPoint(Camera.main.ScreenToViewportPoint(touch.position));
            ShootRayCast(touch.position);
        }
    }

    private void ShootRayCast(Vector3 touchPosition)
    {
        Debug.unityLogger.Log(touchPosition.ToString());
        Physics.Raycast(Camera.main.ScreenPointToRay(touchPosition), out RaycastHit hit);
        if (hit.collider != null)
        {
            Debug.Log(hit.collider.gameObject.name);
            Instantiate(cubePrefab, hit.point, Quaternion.identity);
        }
    }

    private Vector3 LookForPoint(Vector3 touchPosition)
    {
        Vector3 nearestPoint = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        
        
        foreach (Vector3 point in points)
        {
            Vector3 adjustedTouchPosition = new Vector3(touchPosition.x, touchPosition.y, point.z);
            if(Vector3.Distance(touchPosition, point) < Vector3.Distance(nearestPoint, touchPosition))
                nearestPoint = point;
        }
        
        Debug.Log(nearestPoint);
        Instantiate(cubePrefab, nearestPoint, Quaternion.identity);
        return nearestPoint;
    }
    


    
    private void UpdatePointCloud(ARPointCloudChangedEventArgs args)
    {
        foreach (GameObject cube in cubes)
        {
            Destroy(cube);
        }
        cubes.Clear();
        
        points = args.updated[0].positions.Value;
        testText.SetText(args.updated[0].positions.Value[0].ToString());

        foreach (Vector3 point in points)
        {
            cubes.Add(Instantiate(pointCloudCubePrefab, point, Quaternion.identity));
        }
    }
}