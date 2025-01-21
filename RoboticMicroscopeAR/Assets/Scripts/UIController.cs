using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using Peak.Can.Basic;

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

    // UI Elements
    [Header("UI Elements")]
    public GameObject hoverUnlock;
    public GameObject hoverCancel;
    public GameObject hoverLoupe;
    public GameObject hoverLightBulb;
    public GameObject uiContainer;

    [Header("Confirmation UI")]
    public GameObject confirmationUnlock;
    public GameObject confirmationCancel;
    public GameObject confirmationLoupe;
    public GameObject confirmationLightBulb;
    public GameObject preControlContainer;

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

            // Log the received data
            Debug.Log($"Serial Data Received: {data}");

            if (data == "1" && !isButtonPressed)
            {
                Debug.Log("Button Pressed: Received 1");
                isButtonPressed = true;

                if (isInOrbitMode)
                {
                    Debug.Log("Already in Orbit Mode.");
                }
                else if (isInUnlockMode)
                {
                    Debug.Log("Unlock mode active: Sending IMU data...");
                    StartSendingIMUData();
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

                if (isInOrbitMode)
                {
                    StopOrbitMode();
                }
                else if (isInUnlockMode)
                {
                    StopSendingIMUData();
                    ExitUnlockMode();
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

    private void HandleUISelection()
    {
        if (lastHoveredUI == hoverUnlock)
        {
            EnterUnlockMode();
        }
        else if (lastHoveredUI == hoverCancel)
        {
            Debug.Log("Cancel Action Triggered.");
            TriggerConfirmation(confirmationCancel);
        }
        else if (lastHoveredUI == hoverLoupe)
        {
            Debug.Log("Loupe Action Triggered.");
            TriggerConfirmation(confirmationLoupe);
        }
        else if (lastHoveredUI == hoverLightBulb)
        {
            Debug.Log("Light Bulb Action Triggered.");
            TriggerConfirmation(confirmationLightBulb);
        }
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
            confirmationElement.SetActive(true);
            StartCoroutine(HideAfterDelay(confirmationElement));
        }
    }

    private System.Collections.IEnumerator HideAfterDelay(GameObject uiElement)
    {
        yield return new WaitForSeconds(1f);
        if (uiElement != null) uiElement.SetActive(false);
    }

private System.Collections.IEnumerator SendIMUData()
{
    while (isInUnlockMode)
    {
        if (finalQuaternion != null)
        {
            Vector3 eulerAngles = finalQuaternion.eulerAngles;

            // Normalize X, Y, Z to the range -45 to 45
            float adjustedX = Mathf.Clamp(eulerAngles.x > 180 ? eulerAngles.x - 360 : eulerAngles.x, -45, 45);
            float adjustedY = Mathf.Clamp(eulerAngles.y > 180 ? eulerAngles.y - 360 : eulerAngles.y, -45, 45);
            float adjustedZ = Mathf.Clamp(eulerAngles.z > 180 ? eulerAngles.z - 360 : eulerAngles.z, -45, 45);

            // Create the data string
            string dataToSend = $"X:{adjustedX:F2},Y:{adjustedY:F2},Z:{adjustedZ:F2},L:0,B:1";
            Debug.Log($"IMU Data Sent: {dataToSend}");

            // Send data via the same serial port used for receiving
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



    private void ResetUI()
    {
        if (uiContainer != null) uiContainer.SetActive(false);
        if (hoverUnlock != null) hoverUnlock.SetActive(false);
        if (hoverCancel != null) hoverCancel.SetActive(false);
        if (hoverLoupe != null) hoverLoupe.SetActive(false);
        if (hoverLightBulb != null) hoverLightBulb.SetActive(false);
        if (confirmationUnlock != null) confirmationUnlock.SetActive(false);
        if (confirmationCancel != null) confirmationCancel.SetActive(false);
        if (confirmationLoupe != null) confirmationLoupe.SetActive(false);
        if (confirmationLightBulb != null) confirmationLightBulb.SetActive(false);
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
