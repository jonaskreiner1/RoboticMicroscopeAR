using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WebcamDisplay))]
public class WebcamDisplayEditor : Editor
{
    public override void OnInspectorGUI()
    {
        WebcamDisplay webcamDisplay = (WebcamDisplay)target;

        // Get the list of webcam devices
        WebCamDevice[] devices = WebCamTexture.devices;
        string[] deviceNames = new string[devices.Length];

        for (int i = 0; i < devices.Length; i++)
        {
            deviceNames[i] = devices[i].name;
        }

        // Display the dropdown if there are devices
        if (devices.Length > 0)
        {
            webcamDisplay.SelectedWebcamIndex = EditorGUILayout.Popup("Webcam", webcamDisplay.SelectedWebcamIndex, deviceNames);
        }
        else
        {
            EditorGUILayout.LabelField("No webcams detected");
        }

        // Apply changes to the serialized object
        if (GUI.changed)
        {
            EditorUtility.SetDirty(webcamDisplay);
        }
    }
}
