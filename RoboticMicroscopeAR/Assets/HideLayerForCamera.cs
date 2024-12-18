using UnityEngine;

public class HideLayerForCamera : MonoBehaviour
{
    public Camera targetCamera; // Assign the target camera
    public string layerToHide;  // Layer name to hide

    void Start()
    {
        if (targetCamera == null || string.IsNullOrEmpty(layerToHide))
        {
            Debug.LogError("Camera or Layer name not assigned.");
            return;
        }

        // Get the layer index
        int layerIndex = LayerMask.NameToLayer(layerToHide);

        if (layerIndex >= 0)
        {
            // Modify the Culling Mask to hide the layer
            targetCamera.cullingMask &= ~(1 << layerIndex);
            Debug.Log($"Layer '{layerToHide}' hidden for Camera '{targetCamera.name}'.");
        }
        else
        {
            Debug.LogError("Layer not found.");
        }
    }
}
