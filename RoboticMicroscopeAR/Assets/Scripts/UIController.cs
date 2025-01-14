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
    private bool isReceived1 = false;
    private bool isReceived2 = false;
    public Quaternion finalQuaternion;

    private Quaternion offsetQuaternion = Quaternion.identity; // Offset quaternion for resetting IMU
    private bool isOffsetSet = false; // Flag to determine if the offset has been set

    // Serial Port Variables
    private SerialPort serialPort;
    public string comPort = "COM6"; // Set the COM port here
    public int baudRate = 9600; // Match the baud rate of your ESP32
    private bool isButtonPressed = false;
    private Thread serialThread; // Thread for serial reading
    private bool isThreadRunning = false;
    private string serialData = null;

    // UI Elements
    [Header("UI Elements")]
    public GameObject hoverUnlock;      // UI for hover unlock
    public GameObject hoverLock;        // UI for hover lock
    public GameObject hoverLoupe;       // UI for hover loupe
    public GameObject hoverLightBulb;   // UI for hover light bulb
    public GameObject uiContainer;      // UI container (all UI elements)

    [Header("Confirmation UI")]
    public GameObject confirmationLock;
    public GameObject confirmationUnlock;
    public GameObject confirmationLoupe;
    public GameObject confirmationLightBulb;

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
        if (uiContainer != null) uiContainer.SetActive(false);
        if (hoverUnlock != null) hoverUnlock.SetActive(false);
        if (hoverLock != null) hoverLock.SetActive(false);
        if (hoverLoupe != null) hoverLoupe.SetActive(false);
        if (hoverLightBulb != null) hoverLightBulb.SetActive(false);
        if (confirmationLock != null) confirmationLock.SetActive(false);
        if (confirmationUnlock != null) confirmationUnlock.SetActive(false);
        if (confirmationLoupe != null) confirmationLoupe.SetActive(false);
        if (confirmationLightBulb != null) confirmationLightBulb.SetActive(false);
    }

    void Update()
    {
        // Handle Button Input (Serial or Keyboard)
        HandleButtonInput();

        // Control UI visibility based on button state
        if (uiContainer != null)
        {
            uiContainer.SetActive(isButtonPressed);
        }

        // Always process PCAN data regardless of UI visibility
        HandlePCANInput();

        // If the UI is visible, manage UI elements based on PCAN data
        if (isButtonPressed)
        {
            UpdateUIBasedOnPCAN();
        }
    }

    private void HandleButtonInput()
    {
        if (!string.IsNullOrEmpty(serialData))
        {
            string data = serialData;
            serialData = null; // Clear the buffer to avoid processing the same data repeatedly

            // Process the serial data
            if (data == "1" && !isButtonPressed)
            {
                Debug.Log("Button Pressed: Received 1");
                isButtonPressed = true;
            }
            else if (data == "0" && isButtonPressed)
            {
                Debug.Log("Button Released: Received 0");
                isButtonPressed = false;
                TriggerConfirmation();
            }
        }

        // Fallback to keyboard input
        if (Input.GetKeyDown(KeyCode.B) && !isButtonPressed)
        {
            Debug.Log("Button Pressed: B key pressed");
            isButtonPressed = true;
        }
        if (Input.GetKeyUp(KeyCode.B) && isButtonPressed)
        {
            Debug.Log("Button Released: B key released");
            isButtonPressed = false;
            TriggerConfirmation();
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
                    serialData = data; // Store the data to be processed in the Update loop
                }
            }
            catch (TimeoutException)
            {
                // Suppress timeout exceptions as they're expected when no data is available
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Unexpected serial read error: {ex.Message}");
            }
        }
    }

    private void HandlePCANInput()
    {
        // Read CAN messages
        status = PCANBasic.Read(deviceHandle, out msg, out timeStamp);
        if (status == TPCANStatus.PCAN_ERROR_OK)
        {
            if (msg.ID == canID1)
            {
                w = BitConverter.ToSingle(msg.DATA, 0); // Bytes 0-3
                x = BitConverter.ToSingle(msg.DATA, 4); // Bytes 4-7
                isReceived1 = true;
            }
            else if (msg.ID == canID2)
            {
                y = BitConverter.ToSingle(msg.DATA, 0); // Bytes 0-3
                z = BitConverter.ToSingle(msg.DATA, 4); // Bytes 4-7
                isReceived2 = true;
            }

            if (isReceived1 && isReceived2)
            {
                // Create the raw quaternion from the received data
                Quaternion rawQuaternion = new Quaternion(x, -y, -z, w);

                // If the offset has not been set, set it now
                if (!isOffsetSet)
                {
                    offsetQuaternion = Quaternion.Inverse(rawQuaternion);
                    isOffsetSet = true;
                }

                // Apply the offset to get the adjusted quaternion
                finalQuaternion = offsetQuaternion * rawQuaternion;

                isReceived1 = false;
                isReceived2 = false;
            }
        }
        else if (status != TPCANStatus.PCAN_ERROR_QRCVEMPTY)
        {
            Debug.LogError("Error reading CAN message: " + status);
        }
    }

    private void UpdateUIBasedOnPCAN()
    {
        if (finalQuaternion != null)
        {
            Vector3 eulerAngles = finalQuaternion.eulerAngles;

            // Handle hoverLock and hoverUnlock
            if (eulerAngles.x >= 7 && eulerAngles.x <= 45)
            {
                ActivateSingleUIElement(hoverLock);
            }
            else if (eulerAngles.x >= 315 && eulerAngles.x <= 353)
            {
                ActivateSingleUIElement(hoverUnlock);
            }
            else if (eulerAngles.y >= 345 || eulerAngles.y <= 40)
            {
                ActivateSingleUIElement(hoverLoupe);
            }
            else if (eulerAngles.y >= 260 && eulerAngles.y <= 320)
            {
                ActivateSingleUIElement(hoverLightBulb);
            }
            else
            {
                DeactivateAllUIElements();
            }
        }
    }

    private void ActivateSingleUIElement(GameObject uiElement)
    {
        // Deactivate all elements first
        DeactivateAllUIElements();

        // Activate only the specified element
        if (uiElement != null) uiElement.SetActive(true);
    }

    private void DeactivateAllUIElements()
    {
        if (hoverLock != null) hoverLock.SetActive(false);
        if (hoverUnlock != null) hoverUnlock.SetActive(false);
        if (hoverLoupe != null) hoverLoupe.SetActive(false);
        if (hoverLightBulb != null) hoverLightBulb.SetActive(false);
    }

    private void TriggerConfirmation()
    {
        if (hoverLock != null && hoverLock.activeSelf && confirmationLock != null)
        {
            confirmationLock.SetActive(true);
            StartCoroutine(HideAfterDelay(confirmationLock));
        }
        else if (hoverUnlock != null && hoverUnlock.activeSelf && confirmationUnlock != null)
        {
            confirmationUnlock.SetActive(true);
            StartCoroutine(HideAfterDelay(confirmationUnlock));
        }
        else if (hoverLoupe != null && hoverLoupe.activeSelf && confirmationLoupe != null)
        {
            confirmationLoupe.SetActive(true);
            StartCoroutine(HideAfterDelay(confirmationLoupe));
        }
        else if (hoverLightBulb != null && hoverLightBulb.activeSelf && confirmationLightBulb != null)
        {
            confirmationLightBulb.SetActive(true);
            StartCoroutine(HideAfterDelay(confirmationLightBulb));
        }
    }

    private System.Collections.IEnumerator HideAfterDelay(GameObject uiElement)
    {
        yield return new WaitForSeconds(1f);
        if (uiElement != null) uiElement.SetActive(false);
    }

    private void OnApplicationQuit()
    {
        // Stop the serial thread
        isThreadRunning = false;
        if (serialThread != null) serialThread.Join();

        // Close serial port
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log($"Serial port {comPort} closed.");
        }

        // Uninitialize PCAN
        PCANBasic.Uninitialize(deviceHandle);
    }

    // Method to reset IMU
    public void ResetIMU()
    {
        isOffsetSet = false; // Clear the offset so the next IMU data sets it
    }
}
