using UnityEngine;
using Vuforia;

public class ScreenMatchVideoBackground : MonoBehaviour
{
    private MeshRenderer screenRenderer;

    void Start()
    {
        // Subscribe to Vuforia's VuforiaStarted event to detect when it initializes
        VuforiaApplication.Instance.OnVuforiaStarted += AdjustScreenToMatchVideoBackground;
    }

    void AdjustScreenToMatchVideoBackground()
    {
        // Find the VideoBackground renderer Vuforia generates at runtime
        var videoBackground = FindVideoBackgroundRenderer();

        if (videoBackground != null)
        {
            // Copy the size and position of the video background to this screen
            AdjustScreenSize(videoBackground);
        }
        else
        {
            Debug.LogError("Video Background Renderer not found. Ensure Vuforia is running.");
        }
    }

    Renderer FindVideoBackgroundRenderer()
    {
        // Search for the video background object Vuforia creates dynamically
        GameObject videoBackgroundObject = GameObject.Find("VideoBackground");

        if (videoBackgroundObject != null)
        {
            return videoBackgroundObject.GetComponent<Renderer>();
        }
        return null;
    }

    void AdjustScreenSize(Renderer videoBackground)
    {
        if (screenRenderer == null)
            screenRenderer = GetComponent<MeshRenderer>();

        if (videoBackground != null && screenRenderer != null)
        {
            // Match position and scale to the Vuforia video background
            transform.position = videoBackground.transform.position;
            transform.rotation = videoBackground.transform.rotation;
            transform.localScale = videoBackground.transform.localScale;

            Debug.Log("Screen size and position adjusted to match Vuforia video background.");
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from the Vuforia event to prevent memory leaks
        if (VuforiaApplication.Instance != null)
            VuforiaApplication.Instance.OnVuforiaStarted -= AdjustScreenToMatchVideoBackground;
    }
}
