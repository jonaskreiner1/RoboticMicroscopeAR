using UnityEngine;

public class WebcamDisplay : MonoBehaviour
{
    private WebCamTexture webcamTexture;
    private WebCamDevice[] devices;

    [SerializeField]
    private int selectedWebcamIndex = 0; // Selected webcam index in the Inspector
    private Material webcamMaterial;

    void OnValidate()
    {
        // Get the list of available webcam devices
        devices = WebCamTexture.devices;

        if (devices.Length > 0)
        {
            for (int i = 0; i < devices.Length; i++)
            {
                Debug.Log("Webcam " + i + ": " + devices[i].name);
            }

            // Keep the selected index within range
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

        // Initialize the webcam
        webcamTexture = new WebCamTexture(devices[index].name);

        Renderer renderer = GetComponent<Renderer>();
        webcamMaterial = renderer.material;
        webcamMaterial.mainTexture = webcamTexture;

        webcamTexture.Play();

        // Adjust UVs after starting the webcam
        AdjustUVs();
    }

    void AdjustUVs()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) return;

        Mesh mesh = meshFilter.mesh;
        Vector2[] uv = mesh.uv;

        // Calculate the aspect ratio of the webcam and the display mesh
        float webcamAspect = (float)webcamTexture.width / webcamTexture.height;
        float meshAspect = transform.localScale.x / transform.localScale.y;

        // Adjust UVs based on aspect ratios
        for (int i = 0; i < uv.Length; i++)
        {
            uv[i] = new Vector2(uv[i].x, uv[i].y);

            if (webcamAspect > meshAspect)
            {
                uv[i].x = (uv[i].x - 0.5f) * (meshAspect / webcamAspect) + 0.5f; // Center the feed
            }
            else
            {
                uv[i].y = (uv[i].y - 0.5f) * (webcamAspect / meshAspect) + 0.5f; // Center the feed
            }
        }

        mesh.uv = uv;
    }

    void OnDisable()
    {
        if (webcamTexture != null)
        {
            webcamTexture.Stop();
        }
    }
}
