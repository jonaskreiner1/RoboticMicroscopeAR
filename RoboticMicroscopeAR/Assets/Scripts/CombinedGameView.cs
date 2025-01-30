using UnityEngine;

public class CombinedGameView : MonoBehaviour
{
    [Header("Render Textures")]
    public RenderTexture display1Texture; // RenderTexture for Display 1
    public RenderTexture display2Texture; // RenderTexture for Display 2

    [Header("Combined Resolution")]
    public int combinedWidth = 3840; // Combined width (e.g., 1920 + 1920 for two HD displays)
    public int combinedHeight = 1080; // Height of each display (assume same for both)

    private Camera combinedCamera;
    private RenderTexture combinedTexture;

    void Start()
    {
        // Create a combined RenderTexture
        combinedTexture = new RenderTexture(combinedWidth, combinedHeight, 24, RenderTextureFormat.ARGB32);
        combinedTexture.Create();

        // Create a new GameObject with a camera to display the combined texture
        GameObject combinedCameraObject = new GameObject("CombinedDisplayCamera");
        combinedCamera = combinedCameraObject.AddComponent<Camera>();
        combinedCamera.targetTexture = null; // Combined camera doesn't need to render to a texture
        combinedCamera.clearFlags = CameraClearFlags.SolidColor;
        combinedCamera.backgroundColor = Color.black;

        // Create a quad to display the combined RenderTexture
        GameObject combinedQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        combinedQuad.name = "CombinedDisplayQuad";
        combinedQuad.transform.position = new Vector3(0, 0, 5);
        combinedQuad.transform.localScale = new Vector3(16, 9, 1); // Adjust aspect ratio if needed
        combinedQuad.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Texture"))
        {
            mainTexture = combinedTexture
        };

        // Draw the combined RenderTexture
        CombineRenderTextures();
    }

    void CombineRenderTextures()
    {
        // Set the combined texture as the active RenderTexture
        RenderTexture.active = combinedTexture;

        // Set up a temporary texture to draw Display 1 and Display 2
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, combinedWidth, 0, combinedHeight);

        // Clear the RenderTexture
        GL.Clear(true, true, Color.black);

        // Draw Display 1 on the left half
        Graphics.DrawTexture(new Rect(0, 0, combinedWidth / 2, combinedHeight), display1Texture);

        // Draw Display 2 on the right half
        Graphics.DrawTexture(new Rect(combinedWidth / 2, 0, combinedWidth / 2, combinedHeight), display2Texture);

        // Restore the previous state
        GL.PopMatrix();

        // Unset the active RenderTexture
        RenderTexture.active = null;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        CombineRenderTextures();
    }
}
