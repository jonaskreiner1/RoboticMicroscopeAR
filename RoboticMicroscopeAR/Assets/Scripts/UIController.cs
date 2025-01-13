using System;
using System.IO.Ports;
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

    // Serial Port Variables
    private SerialPort serialPort;
    public string comPort = "COM6"; // Set the COM port here
    public int baudRate = 9600; // Match the baud rate of your ESP32
    private bool isButtonPressed = false;
    private bool serialAvailable = true;

    // UI Elements
    [Header("UI Elements")]
    public GameObject hoverUnlock;      // UI for hover unlock
    public GameObject hoverLock;        // UI for hover lock
    public GameObject hoverLoupe;       // UI for hover loupe
    public GameObject hoverLightBulb;   // UI for hover light bulb
    public GameObject uiContainer;      // UI container (all UI elements)

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
            serialAvailable = true;
            Debug.Log($"Serial port {comPort} opened at {baudRate} baud.");
        }
        catch (System.Exception e)
        {
            serialAvailable = false;
            Debug.LogWarning($"Failed to open serial port {comPort}: {e.Message}. Switching to keyboard input.");
        }

        // Ensure UI is initially hidden
        if (uiContainer != null) uiContainer.SetActive(false);
        if (hoverUnlock != null) hoverUnlock.SetActive(false);
        if (hoverLock != null) hoverLock.SetActive(false);
        if (hoverLoupe != null) hoverLoupe.SetActive(false);
        if (hoverLightBulb != null) hoverLightBulb.SetActive(false);
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
        if (serialAvailable && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                string data = serialPort.ReadLine().Trim();
                if (data == "1" && !isButtonPressed)
                {
                    Debug.Log("Button Pressed: Received 1");
                    isButtonPressed = true;
                }
                else if (data == "0" && isButtonPressed)
                {
                    Debug.Log("Button Released: Received 0");
                    isButtonPressed = false;
                }
            }
            catch (System.TimeoutException)
            {
                // Ignore timeout exceptions
            }
        }
        else
        {
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
                finalQuaternion = new Quaternion(x, -y, -z, w); // Adapt axes
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

            // Handle hoverLock and hoverUnlock (corrected behavior)
            if (eulerAngles.x >= 7 && eulerAngles.x <= 15)
            {
                if (hoverLock != null) hoverLock.SetActive(true);
                if (hoverUnlock != null) hoverUnlock.SetActive(false);
            }
            else if (eulerAngles.x >= 330 && eulerAngles.x <= 340)
            {
                if (hoverLock != null) hoverLock.SetActive(false);
                if (hoverUnlock != null) hoverUnlock.SetActive(true);
            }
            else
            {
                if (hoverLock != null) hoverLock.SetActive(false);
                if (hoverUnlock != null) hoverUnlock.SetActive(false);
            }

            // Handle hoverLoupe and hoverLightBulb (new behavior)
            if (eulerAngles.y >= 345 || eulerAngles.y <= 20)
            {
                if (hoverLoupe != null) hoverLoupe.SetActive(true);
            }
            else
            {
                if (hoverLoupe != null) hoverLoupe.SetActive(false);
            }

            if (eulerAngles.y >= 260 && eulerAngles.y <= 290)
            {
                if (hoverLightBulb != null) hoverLightBulb.SetActive(true);
            }
            else
            {
                if (hoverLightBulb != null) hoverLightBulb.SetActive(false);
            }
        }
    }

    private void OnApplicationQuit()
    {
        // Close serial port
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log($"Serial port {comPort} closed.");
        }

        // Uninitialize PCAN
        PCANBasic.Uninitialize(deviceHandle);
    }
}
