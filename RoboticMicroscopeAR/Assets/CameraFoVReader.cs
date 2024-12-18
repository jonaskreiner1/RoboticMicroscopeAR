using UnityEngine;
using Vuforia;

public class CameraFoVReader : MonoBehaviour
{
    public Camera masterARCamera; // Assign your Master AR Camera here in the Inspector
    public Camera slaveCamera;    // Optional: Assign Slave Camera here if used

    void Start()
    {
        // Log information when Vuforia starts
        VuforiaApplication.Instance.OnVuforiaStarted += LogCameraFoV;

        // Log initial FoV before Vuforia starts
        LogInitialFoV();
    }

    void LogInitialFoV()
    {
        Debug.Log("=== Initial Camera Field of View ===");

        if (masterARCamera != null)
        {
            Debug.Log("Master AR Camera FoV: " + masterARCamera.fieldOfView);
        }
        else
        {
            Debug.LogWarning("Master AR Camera is not assigned!");
        }

        if (slaveCamera != null)
        {
            Debug.Log("Slave Camera FoV: " + slaveCamera.fieldOfView);
        }
    }

    void LogCameraFoV()
    {
        Debug.Log("=== FoV After Vuforia Initialization ===");

        // Check the AR Camera's FoV
        if (masterARCamera != null)
        {
            Debug.Log("Master AR Camera FoV (after Vuforia): " + masterARCamera.fieldOfView);
        }

        // Use Vuforia's Camera Intrinsics to log FoV
        var cameraIntrinsics = VuforiaBehaviour.Instance.CameraDevice.GetCameraIntrinsics();
        if (cameraIntrinsics != null)
        {
            float fovX = Mathf.Atan2(cameraIntrinsics.FocalLength.x, cameraIntrinsics.PrincipalPoint.x) * 2 * Mathf.Rad2Deg;
            float fovY = Mathf.Atan2(cameraIntrinsics.FocalLength.y, cameraIntrinsics.PrincipalPoint.y) * 2 * Mathf.Rad2Deg;

            Debug.Log($"Vuforia Camera Intrinsics FoV: X = {fovX}, Y = {fovY}");
        }
        else
        {
            Debug.LogWarning("Could not retrieve Vuforia Camera Intrinsics.");
        }

        // Log FoV for the Slave Camera (if exists)
        if (slaveCamera != null)
        {
            Debug.Log("Slave Camera FoV (after Vuforia): " + slaveCamera.fieldOfView);
        }
    }

    void OnDestroy()
    {
        if (VuforiaApplication.Instance != null)
        {
            VuforiaApplication.Instance.OnVuforiaStarted -= LogCameraFoV;
        }
    }
}
