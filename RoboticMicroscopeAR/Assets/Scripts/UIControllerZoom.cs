using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using Peak.Can.Basic;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // Required for scene loading
public class UIControllerZoom : MonoBehaviour
{
    // PCAN Variables
    TPCANMsg msg;

    TPCANStatus status;
    System.UInt16 deviceHandle;
    TPCANTimestamp timeStamp;

    private float w, x, y, z; // Quaternion components
    private const uint canID1 = 0x514; // w, x
    private const uint canID2 = 0x515; // y, z
    public Quaternion finalQuaternion;

    private Quaternion offsetQuaternion = Quaternion.identity;
    private bool isOffsetSet = false;

    // Serial Port Variables
    private SerialPort serialPort;
    public string comPort = "COM8";
    public int baudRate = 115200;
    private bool isButtonPressed = false;
    private bool isInUnlockMode = false;
    private bool isInOrbitMode = false;
    private Thread serialThread;
    private bool isThreadRunning = false;
    private string serialData = null;
    private bool isInZoomMode = false;
    private bool isInLightBulbMode = false;

    private int currentX = 0;
    private int currentY = 0;
    private int currentZ = 0;
    private int currentL = 5;

    // UI Elements
    [Header("UI Elements")]
    public GameObject hoverUnlock;
    public GameObject hoverCancel;
    public GameObject hoverLoupe;
    public GameObject hoverLightBulb;
    public GameObject uiContainer;

    [Header("Confirmation UI")]
    public GameObject confirmationCancel;

    [Header("PreContainer")]
    public GameObject preControlContainer;
    public GameObject preZoomContainer;
    public GameObject preLightBulbContainer;

    [Header("Slider")]
    public Slider zoomSlider;
    public Slider lightSlider;
    public Light directionalLight; // Drag and drop your DirectionalLight here in the Inspector

    [Header("Background Sprite")]
    public SpriteRenderer pahBackgroundSprite; // Assign this in the Inspector

    [Header("Scene Management")]
    public string nextSceneName = "Intro3"; // Set scene name in Inspector


    private GameObject lastHoveredUI;
    private Coroutine imuDataCoroutine;

    void Start()
    {
        // Ensure the script only runs in its assigned scene
        if (SceneManager.GetActiveScene().name != gameObject.scene.name)
        {
            Debug.Log($"[{gameObject.name}] is disabled because it's not in the active scene: {SceneManager.GetActiveScene().name}");
            enabled = false;
            return; // Stop execution of Start()
        }

        Debug.Log($"[{gameObject.name}] is running in scene: {SceneManager.GetActiveScene().name}");

        // Initialize PCAN
        deviceHandle = PCANBasic.PCAN_USBBUS1;
        status = PCANBasic.Initialize(deviceHandle, TPCANBaudrate.PCAN_BAUD_125K);
        if (status != TPCANStatus.PCAN_ERROR_OK)
        {
            Debug.LogError("Error initializing PCAN-USB device. Status: " + status);
            return;
        }
        Debug.Log("PCAN-USB device initialized.");

        // Initialize Serial Port
        serialPort = new SerialPort(comPort, baudRate);
        try
        {
            serialPort.Open();
            serialPort.ReadTimeout = 100;
            Debug.Log($"Serial port {comPort} opened at {baudRate} baud.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to open serial port {comPort}: {e.Message}. Switching to keyboard input.");
        }

        // Start serial reading thread
        isThreadRunning = true;
        serialThread = new Thread(ReadSerial);
        serialThread.Start();

        if (zoomSlider != null)
        {
            zoomSlider.value = 0; // Start at the default value
            zoomSlider.gameObject.SetActive(false); // Hide initially
        }

        if (lightSlider != null)
        {
            lightSlider.value = 0; // Start at the default value
            lightSlider.gameObject.SetActive(false); // Hide initially
        }

        // Ensure UI is initially hidden
        ResetUI();
        if (uiContainer != null) uiContainer.SetActive(false);
        if (preControlContainer != null) preControlContainer.SetActive(false);

        currentX = 0;
        currentY = 0;
        currentZ = 0;
        currentL = 5;
    }


    void Update()
    {
        // Handle Button Input (Serial or Keyboard)
        HandleButtonInput();

        // Always process PCAN data regardless of UI visibility
        HandlePCANInput();

        // If the UI is visible, manage UI elements based on PCAN data
        if (isButtonPressed && !isInUnlockMode && !isInOrbitMode)
        {
            UpdateUIBasedOnPCAN();
        }
    }

    private void HandleButtonInput()
    {
        if (!string.IsNullOrEmpty(serialData))
        {
            string data = serialData;
            serialData = null;

            Debug.Log($"Serial Data Received: {data}");

            if (data == "1" && !isButtonPressed)
            {
                Debug.Log("Button Pressed: Received 1");
                isButtonPressed = true;

                if (isInZoomMode)
                {
                    Debug.Log("Zoom Mode active: Sending Z data...");
                    StartSendingZoomData();
                }
                else if (isInUnlockMode)
                {
                    Debug.Log("Unlock mode active: Sending IMU data...");
                    StartSendingIMUData();
                }
                else if (isInLightBulbMode)
                {
                    Debug.Log("Light Bulb Mode active: Sending Light Bulb data...");
                    StartSendingLightBulbData();
                }
                else
                {
                    ShowUI();
                }
            }
            else if (data == "0" && isButtonPressed)
            {
                Debug.Log("Button Released: Received 0");
                isButtonPressed = false;

                if (isInZoomMode)
                {
                    StopSendingZoomData();
                    ExitZoomMode();
                }
                else if (isInUnlockMode)
                {
                    StopSendingIMUData();
                    ExitUnlockMode();
                }
                else if (isInLightBulbMode)
                {
                    StopSendingLightBulbData();
                    ExitLightBulbMode();
                }
                else
                {
                    HandleUISelection();
                    HideUI();
                }
            }
        }
    }


    private void ShowUI()
    {
        if (uiContainer != null)
        {
            uiContainer.SetActive(true); // Show the UI container
        }
    }

    private void HideUI()
    {
        if (uiContainer != null)
        {
            uiContainer.SetActive(false); // Hide the UI container
        }
    }

    private void EnterUnlockMode()
    {
        if (hoverUnlock.activeSelf)
        {
            Debug.Log("Unlock Mode Triggered.");
            isInUnlockMode = true;
            preControlContainer.SetActive(true); // Show pre-control container
            HideUI(); // Hide UI container
        }
        else
        {
            TriggerConfirmation(lastHoveredUI);
        }
    }

    private void ExitUnlockMode()
    {
        Debug.Log("Exiting Unlock Mode...");
        isInUnlockMode = false;
        preControlContainer.SetActive(false); // Hide pre-control container
    }

    private void StartSendingIMUData()
    {
        if (imuDataCoroutine == null)
        {
            // Change the color of PaHbackground to a softer, transparent red
            if (pahBackgroundSprite != null)
            {
                pahBackgroundSprite.color = new Color32(255, 102, 102, 178); // Softer red with 70% transparency
            }

            imuDataCoroutine = StartCoroutine(SendIMUData());
        }
    }

    private void StopSendingIMUData()
    {
        if (imuDataCoroutine != null)
        {
            StopCoroutine(imuDataCoroutine);
            imuDataCoroutine = null;
        }

        // Reset the color of PaHbackground to white with 70% transparency
        if (pahBackgroundSprite != null)
        {
            pahBackgroundSprite.color = new Color32(255, 255, 255, 178); // White with 70% transparency
        }
    }

    private void StopOrbitMode()
    {
        Debug.Log("Exiting Orbit Mode...");
        isInOrbitMode = false;
        HideUI();
    }



    private void ReadSerial()
    {
        while (isThreadRunning)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    string data = serialPort.ReadLine().Trim();
                    serialData = data;
                }
            }
            catch (TimeoutException)
            {
                // Suppress timeout exceptions
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Unexpected serial read error: {ex.Message}");
            }
        }
    }

    private void HandlePCANInput()
    {
        status = PCANBasic.Read(deviceHandle, out msg, out timeStamp);
        if (status == TPCANStatus.PCAN_ERROR_OK)
        {
            if (msg.ID == canID1)
            {
                w = BitConverter.ToSingle(msg.DATA, 0);
                x = BitConverter.ToSingle(msg.DATA, 4);
            }
            else if (msg.ID == canID2)
            {
                y = BitConverter.ToSingle(msg.DATA, 0);
                z = BitConverter.ToSingle(msg.DATA, 4);
            }

            Quaternion rawQuaternion = new Quaternion(x, -y, -z, w);

            if (!isOffsetSet)
            {
                offsetQuaternion = Quaternion.Inverse(rawQuaternion);
                isOffsetSet = true;
            }

            finalQuaternion = offsetQuaternion * rawQuaternion;
        }
    }

    private void UpdateUIBasedOnPCAN()
    {
        if (finalQuaternion != null)
        {
            Vector3 eulerAngles = finalQuaternion.eulerAngles;

            // Extract raw X and Y angles
            float rawX = eulerAngles.x;
            float rawY = eulerAngles.y;

            // Priority order for selection
            if (rawY >= 330 && rawY <= 360) // Hover Cancel for Y between 330 and 360
            {
                ActivateSingleUIElement(hoverCancel);
            }
            else if (rawX >= 260 && rawX <= 355) // Hover Zoom for X between 260 and 345
            {
                ActivateSingleUIElement(hoverLightBulb); // Assuming hoverLoupe is the Zoom UI element
            }
            else if (rawX >= 5 && rawX <= 90) // Hover Light Bulb for X between 15 and 90
            {
                ActivateSingleUIElement(hoverLoupe);
            }
            else if ((rawX > 345 && rawX <= 360) || (rawX >= 0 && rawX < 15)) // Hover Unlock for X between 345-360 and 0-15
            {
                ActivateSingleUIElement(hoverUnlock);
            }
            else // Fallback to hoverUnlock if no condition is satisfied
            {
                ActivateSingleUIElement(hoverUnlock);
            }
        }
    }






    private void ActivateSingleUIElement(GameObject uiElement)
    {
        DeactivateAllUIElements();
        if (uiElement != null)
        {
            uiElement.SetActive(true);
            lastHoveredUI = uiElement; // Track the last hovered UI
        }
    }

    private void DeactivateAllUIElements()
    {
        if (hoverCancel != null) hoverCancel.SetActive(false);
        if (hoverUnlock != null) hoverUnlock.SetActive(false);
        if (hoverLoupe != null) hoverLoupe.SetActive(false);
        if (hoverLightBulb != null) hoverLightBulb.SetActive(false);
    }
    private void TriggerConfirmation(GameObject confirmationElement)
    {
        if (confirmationElement != null)
        {
            Debug.Log($"Triggering confirmation for: {confirmationElement.name}");

            // Ensure all parents are active
            Transform parent = confirmationElement.transform.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    Debug.LogWarning($"Parent object '{parent.name}' is inactive. Activating it.");
                    parent.gameObject.SetActive(true);
                }
                parent = parent.parent;
            }

            confirmationElement.SetActive(true); // Activate the confirmation element itself
            StartCoroutine(HideAfterDelay(confirmationElement)); // Hide it after a delay
        }
        // Removed the Debug.LogWarning for missing confirmation element.
    }


    private void HandleUISelection()
    {
        if (lastHoveredUI == hoverUnlock)
        {
            TriggerConfirmation(confirmationCancel);
        }
        else if (lastHoveredUI == hoverLoupe)
        {
            EnterZoomMode();
        }
        else if (lastHoveredUI == hoverLightBulb)
        {
            TriggerConfirmation(confirmationCancel); // Enter Light Bulb Mode when hoverLightBulb is selected
        }
        else if (lastHoveredUI == hoverCancel)
        {
            Debug.Log("Cancel Action Triggered.");
            TriggerConfirmation(confirmationCancel); // Show confirmationCancel when cancel is hovered
        }
    }



    private void EnterZoomMode()
    {
        if (zoomSlider != null)
        {
            Debug.Log("Entering Zoom Mode...");
            isInZoomMode = true;

            // Activate the preZoomContainer
            if (preZoomContainer != null)
            {
                preZoomContainer.SetActive(true);
            }

            // Show slider
            zoomSlider.gameObject.SetActive(true);
            zoomSlider.value = 0; // Reset the slider value to the center
        }
    }


    private void ExitZoomMode()
    {
        if (zoomSlider != null)
        {
            Debug.Log("Exiting Zoom Mode...");
            isInZoomMode = false;

            // Start a coroutine to hide the slider after 2 seconds
            StartCoroutine(HideZoomSliderAfterDelay());
        }

        // Load the next scene (Intro3) after exiting Light Mode
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            serialPort.Close(); 
            PCANBasic.Uninitialize(deviceHandle);
            Debug.Log($"Loading next scene: {nextSceneName}");
            SceneManager.LoadScene(nextSceneName);
        }
    }
    private void EnterLightBulbMode()
    {
        if (hoverLightBulb.activeSelf)
        {
            Debug.Log("Entering Light Bulb Mode...");
            isInLightBulbMode = true;

            // Activate the preLightBulbContainer
            if (preLightBulbContainer != null)
            {
                preLightBulbContainer.SetActive(true);
            }

            // Show the light slider
            if (lightSlider != null)
            {
                lightSlider.gameObject.SetActive(true);
                lightSlider.value = 0; // Reset the slider to the center
            }

            HideUI();
        }
        else
        {
            TriggerConfirmation(lastHoveredUI);
        }
    }
    private void ExitLightBulbMode()
    {
        Debug.Log("Exiting Light Bulb Mode...");
        isInLightBulbMode = false;

        // Start a coroutine to hide the slider after 2 seconds
        StartCoroutine(HideLightSliderAfterDelay());

    }


    private System.Collections.IEnumerator HideLightSliderAfterDelay()
    {
        yield return new WaitForSeconds(2f); // Wait for 2 seconds
        if (lightSlider != null)
        {
            lightSlider.gameObject.SetActive(false); // Hide the slider
        }

        // Deactivate the preLightBulbContainer
        if (preLightBulbContainer != null)
        {
            preLightBulbContainer.SetActive(false);
        }
    }
    private void StartSendingLightBulbData()
    {
        if (imuDataCoroutine == null)
        {
            imuDataCoroutine = StartCoroutine(SendLightBulbData());
        }
    }

    private void StopSendingLightBulbData()
    {
        if (imuDataCoroutine != null)
        {
            StopCoroutine(imuDataCoroutine);
            imuDataCoroutine = null;
        }
    }

    private System.Collections.IEnumerator HideZoomSliderAfterDelay()
    {
        yield return new WaitForSeconds(2f); // Wait for 2 seconds
        if (zoomSlider != null)
        {
            zoomSlider.gameObject.SetActive(false); // Hide the slider
        }

        // Deactivate the preZoomContainer
        if (preZoomContainer != null)
        {
            preZoomContainer.SetActive(false);
        }
    }



    private void StartSendingZoomData()
    {
        if (imuDataCoroutine == null)
        {
            imuDataCoroutine = StartCoroutine(SendZoomData());
        }
    }
    private void StopSendingZoomData()
    {
        if (imuDataCoroutine != null)
        {
            StopCoroutine(imuDataCoroutine);
            imuDataCoroutine = null;
        }
    }

    private void SendSerialData()
    {
        string dataToSend = $"X{currentX},Y{currentY},Z{currentZ},L{currentL}";
        Debug.Log($"Data Sent: {dataToSend}");

        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                serialPort.WriteLine(dataToSend);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to send data: {ex.Message}");
            }
        }
    }




    private System.Collections.IEnumerator HideAfterDelay(GameObject uiElement)
    {
        yield return new WaitForSeconds(1f);
        if (uiElement != null) uiElement.SetActive(false);
    }

    private System.Collections.IEnumerator SendLightBulbData()
    {
        while (isInLightBulbMode)
        {
            if (finalQuaternion != null && lightSlider != null)
            {
                // Extract Y angle from quaternion and normalize to range 0–360
                float rawAngle = finalQuaternion.eulerAngles.y;

                float smoothValue; // Smooth floating-point value for slider

                // Map angles to slider values
                if (rawAngle >= 290 && rawAngle <= 335) // Smooth interpolation for 290° to 325°
                {
                    smoothValue = Mathf.Lerp(0, 10, (rawAngle - 290) / 45); // Map 290° to 325° → 0 to 10
                }
                else if (rawAngle > 335 || rawAngle <= 179) // Clamp to max (10)
                {
                    smoothValue = 10;
                }
                else if (rawAngle > 179 && rawAngle < 290) // Clamp to min (0)
                {
                    smoothValue = 0;
                }
                else
                {
                    // Safety net: Clamp to 5 (fallback, should not be reached)
                    smoothValue = 5;
                }

                // Update slider smoothly with the calculated value
                lightSlider.value = smoothValue;
                float intensity = Mathf.Clamp01((lightSlider.value - 1) * 0.1f);

                // Set the intensity of the directional light
                directionalLight.intensity = intensity;
                // Convert smooth value to integer and update the shared `currentL`
                currentL = Mathf.RoundToInt(smoothValue);

                Debug.Log($"Light Slider Value (Smooth): {smoothValue:F2}, Sent Value (Integer): {currentL}");

                // Send the unified data string
                SendSerialData();

                yield return new WaitForSeconds(0.1f); // Update every 0.1 seconds
            }
        }
    }






    private System.Collections.IEnumerator SendIMUData()
    {
        // Ensure the SpriteRenderer is assigned
        if (pahBackgroundSprite != null)
        {
            // Set the background color to a softer, transparent red
            pahBackgroundSprite.color = new Color32(255, 102, 102, 178); // Softer red with 70% transparency
            Debug.Log($"Color set to transparent red: {pahBackgroundSprite.color}");
        }

        while (isInUnlockMode)
        {
            if (finalQuaternion != null)
            {
                Vector3 eulerAngles = finalQuaternion.eulerAngles;

                // Process X (337.5° to 22.5° → -45 to 45)
                float rawX = eulerAngles.x;
                if (rawX >= 337.5f || rawX <= 22.5f)
                {
                    if (rawX >= 337.5f)
                    {
                        currentX = Mathf.RoundToInt(Mathf.Lerp(45, 0, (rawX - 337.5f) / 22.5f)); // 337.5° to 360° → 45 to 0
                    }
                    else // rawX <= 22.5°
                    {
                        currentX = Mathf.RoundToInt(Mathf.Lerp(0, -45, rawX / 22.5f)); // 0° to 22.5° → 0 to -45
                    }
                }
                else if (rawX > 22.5f && rawX <= 180f)
                {
                    currentX = -45;
                }
                else if (rawX > 180f && rawX < 337.5f)
                {
                    currentX = 45;
                }

                // Process Y (300° to 330° → -20 to 70)
                float rawY = eulerAngles.y;
                if (rawY >= 300f && rawY <= 330f) // Map 300° to 330° → -20 to 70
                {
                    currentY = Mathf.RoundToInt(Mathf.Lerp(-20, 70, (rawY - 300f) / 30f));
                }
                else if (rawY > 330f || rawY <= 180f) // Clamp to max (70)
                {
                    currentY = 70;
                }
                else if (rawY > 180f && rawY < 300f) // Clamp to min (-20)
                {
                    currentY = -20;
                }

                // Send the unified data string
                SendSerialData();

                yield return new WaitForSeconds(0.1f); // Delay before next update
            }
        }

        if (pahBackgroundSprite != null)
        {
            // Reset the background color to white with 70% transparency when exiting the mode
            pahBackgroundSprite.color = new Color32(255, 255, 255, 178); // White with 70% transparency
            Debug.Log($"Color reset to transparent white: {pahBackgroundSprite.color}");
        }
    }







    private System.Collections.IEnumerator SendZoomData()
    {
        while (isInZoomMode)
        {
            if (finalQuaternion != null && zoomSlider != null)
            {
                // Extract Y angle from quaternion and normalize to range 0–360
                float rawAngle = finalQuaternion.eulerAngles.y;

                float normalizedValue;

                // Calculate the normalized zoom value based on the adjusted angle
                if (rawAngle >= 290 && rawAngle <= 335) // Interpolation for range 290° to 335°
                {
                    normalizedValue = Mathf.Lerp(-35, 65, (rawAngle - 290) / 45); // Map 290° to 335° → -35 to 65
                }
                else if (rawAngle > 335 || rawAngle <= 179) // Clamp values for angles outside interpolation range
                {
                    normalizedValue = 65; // Clamp to max (+65) for angles 335°-360° and 0°-179°
                }
                else if (rawAngle > 179 && rawAngle < 290) // Clamp to min (-35) for angles 180°-289°
                {
                    normalizedValue = -35;
                }
                else
                {
                    // Safety net: Clamp to 0 (should not be reached in normal conditions)
                    normalizedValue = 0;
                }

                // Update the slider value
                zoomSlider.value = normalizedValue;

                // Convert the normalized value to an integer for `currentZ`
                currentZ = Mathf.RoundToInt(normalizedValue);

                Debug.Log($"Zoom Slider Value Updated: {normalizedValue:F2}, Current Z Value: {currentZ}");

                // Send the unified serial data string
                SendSerialData();

                // Wait before the next update
                yield return new WaitForSeconds(0.1f);
            }
        }
    }











    private void ResetUI()
    {
        if (uiContainer != null) uiContainer.SetActive(false);
        if (hoverUnlock != null) hoverUnlock.SetActive(false);
        if (hoverCancel != null) hoverCancel.SetActive(false);
        if (hoverLoupe != null) hoverLoupe.SetActive(false);
        if (hoverLightBulb != null) hoverLightBulb.SetActive(false);

        if (confirmationCancel != null) confirmationCancel.SetActive(false); // Only retain this confirmation
        if (preControlContainer != null) preControlContainer.SetActive(false);
        if (preZoomContainer != null) preZoomContainer.SetActive(false);
        if (preLightBulbContainer != null) preLightBulbContainer.SetActive(false);
    }



    private void OnApplicationQuit()
    {
        isThreadRunning = false;
        if (serialThread != null) serialThread.Join();

        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log($"Serial port {comPort} closed.");
        }

        PCANBasic.Uninitialize(deviceHandle);
    }

    public void ResetIMU()
    {
        isOffsetSet = false;
    }
}