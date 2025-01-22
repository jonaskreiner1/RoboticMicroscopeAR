using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using Peak.Can.Basic;
using UnityEngine.UI;

public class UIController : MonoBehaviour
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

    private Quaternion offsetQuaternion = Quaternion.identity; // Offset quaternion for resetting IMU
    private bool isOffsetSet = false; // Flag to determine if the offset has been set

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
    private bool isInZoomMode = false; // New Zoom Mode flag
    private bool isInLightBulbMode = false; // New Light Bulb Mode flag


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
    public GameObject preZoomContainer; // New pre-zoom container
    public GameObject preLightBulbContainer; // New Light Bulb container

    [Header("Slider")]
    public Slider zoomSlider; // Reference to the Slider component
    public Slider lightSlider; // Reference to the Light Slider component

    private GameObject lastHoveredUI; // Tracks the last hovered UI element

    private Coroutine imuDataCoroutine; // Coroutine for IMU data sending

    void Start()
    {
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

            if (eulerAngles.y >= 15 && eulerAngles.y <= 90)
            {
                ActivateSingleUIElement(hoverCancel);
            }
            else if (eulerAngles.z >= 30 && eulerAngles.z <= 90)
            {
                ActivateSingleUIElement(hoverLoupe);
            }
            else if (eulerAngles.z >= 270 && eulerAngles.z <= 330)
            {
                ActivateSingleUIElement(hoverLightBulb);
            }
            else
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
    else
    {
        Debug.LogWarning("No confirmation element provided!");
    }
}


private void HandleUISelection()
{
    if (lastHoveredUI == hoverUnlock)
    {
        EnterUnlockMode();
    }
    else if (lastHoveredUI == hoverLoupe)
    {
        EnterZoomMode(); // Enter Zoom Mode when hoverLoupe is selected
    }
    else if (lastHoveredUI == hoverLightBulb)
    {
        EnterLightBulbMode(); // Enter Light Bulb Mode when hoverLightBulb is selected
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

            // Adjust the raw angle by +30 degrees
            rawAngle = (rawAngle + 30) % 360;

            float normalizedValue;

            if (rawAngle >= 315 || rawAngle <= 45) // Smooth interpolation
            {
                if (rawAngle >= 315)
                {
                    normalizedValue = Mathf.Lerp(-10, 0, (rawAngle - 315) / 45); // Map 315° to 360° to -10 → 0
                }
                else // rawAngle <= 45
                {
                    normalizedValue = Mathf.Lerp(0, 10, rawAngle / 45); // Map 0° to 45° to 0 → 10
                }
            }
            else if (rawAngle > 45 && rawAngle <= 179) // Clamp to max (+10)
            {
                normalizedValue = 10;
            }
            else if (rawAngle >= 180 && rawAngle < 315) // Clamp to min (-10)
            {
                normalizedValue = -10;
            }
            else
            {
                // Safety net: Clamp to 0
                normalizedValue = 0;
            }

            // Update slider value
            lightSlider.value = normalizedValue;

            Debug.Log($"Light Slider Value Updated: {normalizedValue:F2}");

            // Send this value via serial
            string dataToSend = $"Z:{normalizedValue:F2}";
            Debug.Log($"Light Data Sent: {dataToSend}");

            if (serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    serialPort.WriteLine(dataToSend);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Failed to send Light data: {ex.Message}");
                }
            }
        }

        yield return new WaitForSeconds(0.1f); // Update every 0.1 seconds
    }
}



private System.Collections.IEnumerator SendIMUData()
{
    while (isInUnlockMode)
    {
        if (finalQuaternion != null)
        {
            Vector3 eulerAngles = finalQuaternion.eulerAngles;

            // Normalize X, add 30-degree offset to Y, Z is fixed to 0
            float adjustedX = Mathf.Clamp(eulerAngles.x > 180 ? eulerAngles.x - 360 : eulerAngles.x, -45, 45);
            float adjustedY = Mathf.Clamp((eulerAngles.y > 180 ? eulerAngles.y - 360 : eulerAngles.y) + 40, -45, 45);

            // Create the data string with Z fixed at 0
            string dataToSend = $"X:{adjustedX:F2},Y:{adjustedY:F2},Z:0";
            Debug.Log($"IMU Data Sent: {dataToSend}");

            // Send data via the serial port
            if (serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    serialPort.WriteLine(dataToSend);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to send data: {ex.Message}");
                }
            }
        }
        yield return new WaitForSeconds(0.1f); // Send data every 0.1 seconds
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

            // Adjust the raw angle by +30 degrees
            rawAngle = (rawAngle + 30) % 360;

            float normalizedValue;

            if (rawAngle >= 315 || rawAngle <= 45) // Smooth interpolation
            {
                if (rawAngle >= 315)
                {
                    normalizedValue = Mathf.Lerp(-10, 0, (rawAngle - 315) / 45); // Map 315° to 360° to -10 → 0
                }
                else // rawAngle <= 45
                {
                    normalizedValue = Mathf.Lerp(0, 10, rawAngle / 45); // Map 0° to 45° to 0 → 10
                }
            }
            else if (rawAngle > 45 && rawAngle <= 179) // Clamp to max (+10)
            {
                normalizedValue = 10;
            }
            else if (rawAngle >= 180 && rawAngle < 315) // Clamp to min (-10)
            {
                normalizedValue = -10;
            }
            else
            {
                // Safety net: Clamp to 0
                normalizedValue = 0;
            }

            // Update slider value
            zoomSlider.value = normalizedValue;

            Debug.Log($"Slider Value Updated: {normalizedValue:F2}");

            // Send this value via serial
            string dataToSend = $"Z:{normalizedValue:F2}";
            Debug.Log($"Data Sent: {dataToSend}");

            if (serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    serialPort.WriteLine(dataToSend);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Failed to send Zoom data: {ex.Message}");
                }
            }
        }

        yield return new WaitForSeconds(0.1f); // Update every 0.1 seconds
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