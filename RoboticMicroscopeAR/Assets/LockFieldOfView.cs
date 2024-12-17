using UnityEngine;
using Vuforia;

public class LockFieldOfView : MonoBehaviour
{
    public Camera arCamera; // Assign your AR camera here
    public float fixedFoV = 60f; // Desired Field of View

    void Start()
    {
        // Check if Vuforia Engine is initialized
        if (VuforiaApplication.Instance != null)
        {
            VuforiaApplication.Instance.OnVuforiaStarted += OnVuforiaStarted;
        }
        else
        {
            Debug.LogError("Vuforia Application is not initialized. Ensure Vuforia is set up correctly.");
        }
    }

    void OnVuforiaStarted()
    {
        // Set the desired field of view after Vuforia has started
        if (arCamera != null)
        {
            arCamera.fieldOfView = fixedFoV;
            Debug.Log($"Field of View set to: {fixedFoV}");
        }
        else
        {
            Debug.LogError("AR Camera is not assigned. Please assign the AR Camera in the Inspector.");
        }
    }

    void OnDestroy()
    {
        // Safely unregister the callback to prevent memory leaks
        if (VuforiaApplication.Instance != null)
        {
            VuforiaApplication.Instance.OnVuforiaStarted -= OnVuforiaStarted;
        }
    }
}
