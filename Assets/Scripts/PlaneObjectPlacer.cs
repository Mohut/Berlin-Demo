using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PlaneObjectPlacer : MonoBehaviour
{
    [SerializeField] private ARRaycastManager arRaycastManager;
    [SerializeField] private GameObject planePrefab;
    List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private void Update()
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            if (arRaycastManager.Raycast(Input.GetTouch(0).position, hits, TrackableType.Planes))
            {
                Debug.Log(hits[0].hitType);
                Instantiate(planePrefab, hits[0].pose.position, hits[0].pose.rotation);
            }
        }
    }
    
    Funktioniert so semi gut
}