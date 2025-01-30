using UnityEngine;

[ExecuteAlways]
public class WebcamDisplay : MonoBehaviour
{
    private WebCamTexture webcamTexture;
    private WebCamDevice[] devices;

    [SerializeField]
    private string selectedWebcamName = ""; // Selected webcam device name

    [SerializeField]
    private Vector2 targetResolution = new Vector2(2592, 1944); // Default resolution

    public string SelectedWebcamName
    {
        get => selectedWebcamName;
        set
        {
            if (selectedWebcamName != value)
            {
                selectedWebcamName = value;
                UpdateWebcamTexture();
            }
        }
    }

    public Vector2 TargetResolution
    {
        get => targetResolution;
        set
        {
            targetResolution = value;
            UpdateWebcamTexture();
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return; // Only validate in editor mode
        }

        devices = WebCamTexture.devices;

        if (devices.Length > 0 && string.IsNullOrEmpty(selectedWebcamName))
        {
            selectedWebcamName = devices[0].name;
        }

        UpdateWebcamTexture();
    }

    void Start()
    {
        if (Application.isPlaying) // Only initialize when in play mode
        {
            InitializeWebcam();
        }
    }

    public void UpdateWebcamTexture()
    {
        if (Application.isPlaying) // Only update in play mode
        {
            InitializeWebcam();
        }
    }

    void InitializeWebcam()
    {
        devices = WebCamTexture.devices;

        if (devices.Length > 0)
        {
            StartWebcam(selectedWebcamName);
        }
        else
        {
            Debug.LogError("No webcam found!");
        }
    }

    void StartWebcam(string webcamName)
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }

        int deviceIndex = System.Array.FindIndex(devices, d => d.name == webcamName);
        if (deviceIndex < 0)
        {
            Debug.LogError("Selected webcam not found!");
            return;
        }

        webcamTexture = new WebCamTexture(devices[deviceIndex].name, (int)targetResolution.x, (int)targetResolution.y);

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial.mainTexture = webcamTexture;
        }

        webcamTexture.Play();
        AdjustScaleToFitResolution();
    }

    void AdjustScaleToFitResolution()
    {
        if (webcamTexture == null) return;

        float webcamAspect = (float)webcamTexture.width / webcamTexture.height;
        float targetAspect = targetResolution.x / targetResolution.y;

        Vector3 scale = transform.localScale;

        if (webcamAspect > targetAspect)
        {
            scale.y = scale.x / webcamAspect;
        }
        else
        {
            scale.x = scale.y * webcamAspect;
        }

        transform.localScale = scale;
    }

    void OnDisable()
    {
        if (Application.isPlaying && webcamTexture != null) // Only stop webcam in play mode
        {
            webcamTexture.Stop();
        }
    }
}
