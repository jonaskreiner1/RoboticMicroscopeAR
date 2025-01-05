using UnityEngine;

public class ToggleUIElements : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject hoverUnlock; // Assign your hoverunlock UI object here
    public GameObject hoverLock;   // Assign your hoverlock UI object here

    void Update()
    {
        // Check for left (L) key input
        if (Input.GetKeyDown(KeyCode.L))
        {
            ToggleVisibility(hoverUnlock);
        }

        // Check for right (R) key input
        if (Input.GetKeyDown(KeyCode.R))
        {
            ToggleVisibility(hoverLock);
        }
    }

    void ToggleVisibility(GameObject uiElement)
    {
        if (uiElement != null)
        {
            // Toggle the active state
            uiElement.SetActive(!uiElement.activeSelf);
        }
        else
        {
            Debug.LogWarning("UI Element is not assigned!");
        }
    }
}
