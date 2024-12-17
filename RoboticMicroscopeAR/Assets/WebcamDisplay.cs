using UnityEngine;

public class WebcamDisplay : MonoBehaviour
{
    private WebCamTexture webcamTexture;
    private WebCamDevice[] devices;

    [SerializeField]
    private int selectedWebcamIndex = 0; // Selected webcam index in the Inspector

    public int SelectedWebcamIndex
    {
        get => selectedWebcamIndex;
        set => selectedWebcamIndex = value; // Allow assignment
    }

    void OnValidate()
    {
        devices = WebCamTexture.devices;

        if (devices.Length > 0)
        {
            selectedWebcamIndex = Mathf.Clamp(selectedWebcamIndex, 0, devices.Length - 1);
        }
    }

    void Start()
    {
        devices = WebCamTexture.devices;

        if (devices.Length > 0)
        {
            StartWebcam(selectedWebcamIndex);
        }
        else
        {
            Debug.LogError("No webcam found!");
        }
    }

    void StartWebcam(int index)
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }

        // Initialize WebCamTexture for the selected device
        webcamTexture = new WebCamTexture(devices[index].name);

        Renderer renderer = GetComponent<Renderer>();
        Material webcamMaterial = renderer.material;
        webcamMaterial.mainTexture = webcamTexture;

        webcamTexture.Play();

        AdjustScaleToFitScreen();
    }

    void AdjustScaleToFitScreen()
    {
        // Adjust the object's scale to fit the webcam texture to the full screen
        if (webcamTexture == null) return;

        float webcamAspect = (float)webcamTexture.width / webcamTexture.height; // Webcam aspect ratio
        float screenAspect = (float)Screen.width / Screen.height; // Screen aspect ratio

        // Calculate scale to fit the screen
        Vector3 scale = transform.localScale;

        if (webcamAspect > screenAspect)
        {
            // Wider than the screen, adjust height
            scale.y = scale.x / webcamAspect;
        }
        else
        {
            // Taller than the screen, adjust width
            scale.x = scale.y * webcamAspect;
        }

        transform.localScale = scale;
    }

    void OnDisable()
    {
        if (webcamTexture != null)
        {
            webcamTexture.Stop();
        }
    }
}
