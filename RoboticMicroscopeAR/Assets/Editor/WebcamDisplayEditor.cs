using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WebcamDisplay))]
public class WebcamDisplayEditor : Editor
{
    private string[] webcamNames; // Array of webcam names
    private int selectedWebcamIndex = 0;

    public override void OnInspectorGUI()
    {
        WebcamDisplay webcamDisplay = (WebcamDisplay)target;

        // Fetch webcam devices
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length > 0)
        {
            // Populate webcam names
            webcamNames = new string[devices.Length];
            for (int i = 0; i < devices.Length; i++)
            {
                webcamNames[i] = devices[i].name;
            }

            // Find the currently selected index
            selectedWebcamIndex = System.Array.IndexOf(webcamNames, webcamDisplay.SelectedWebcamName);
            if (selectedWebcamIndex < 0) selectedWebcamIndex = 0;

            // Display dropdown and detect changes
            int newSelectedIndex = EditorGUILayout.Popup("Webcam Device", selectedWebcamIndex, webcamNames);

            if (newSelectedIndex != selectedWebcamIndex)
            {
                selectedWebcamIndex = newSelectedIndex;
                webcamDisplay.SelectedWebcamName = webcamNames[selectedWebcamIndex];
                webcamDisplay.UpdateWebcamTexture(); // Immediately update texture
                EditorUtility.SetDirty(webcamDisplay); // Mark as changed
            }
        }
        else
        {
            EditorGUILayout.LabelField("No webcams detected.");
        }

        // Target resolution field
        Vector2 newResolution = EditorGUILayout.Vector2Field("Target Resolution", webcamDisplay.TargetResolution);
        if (newResolution != webcamDisplay.TargetResolution)
        {
            webcamDisplay.TargetResolution = newResolution;
            webcamDisplay.UpdateWebcamTexture();
            EditorUtility.SetDirty(webcamDisplay);
        }
    }
}
