using System;
using TMPro;
using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    float currentTime = 0.0f;
    private int steps = 0;
    [SerializeField] private TMP_Text fpsText;
    private void Start()
    {
        InvokeRepeating(nameof(GetFPS), 0.5f, 0.5f);
    }
    private void GetFPS()
    {
        float avgTimestep = currentTime / steps;
        currentTime = 0;
        steps = 0;
        
        fpsText.SetText(((int)(1f / avgTimestep)).ToString());    
    }

    private void Update()
    {
        steps++;
        currentTime += Time.deltaTime;
    }
}