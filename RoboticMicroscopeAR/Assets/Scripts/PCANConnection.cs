using System;
using UnityEngine;
using Peak.Can.Basic;

public class PCANConnection : MonoBehaviour
{
    // Define the PCAN-USB device
    TPCANMsg msg;
    TPCANStatus status;
    System.UInt16 deviceHandle;
    TPCANTimestamp timeStamp;

    // Define quaternion components
    private float w, x, y, z;

    // Define CAN IDs for quaternion data
    private const uint canID1 = 0x514; // w, x
    private const uint canID2 = 0x515; // y, z

    // Flags to check if data has been received
    private bool isReceived1 = false;
    private bool isReceived2 = false;

    // Unity quaternion
    public Quaternion finalQuaternion;

    // UI Elements
    [Header("UI Elements")]
    public GameObject hoverUnlock; // Assign your hoverunlock UI object here
    public GameObject hoverLock;   // Assign your hoverlock UI object here

    void Start()
    {
        // Initialize PCAN-USB device
        deviceHandle = PCANBasic.PCAN_USBBUS1;
        status = PCANBasic.Initialize(deviceHandle, TPCANBaudrate.PCAN_BAUD_125K);

        if (status != TPCANStatus.PCAN_ERROR_OK)
        {
            Debug.LogError("Error initializing PCAN-USB device. Status: " + status);
            return;
        }

        Debug.Log("PCAN-USB device initialized.");

        // Ensure UI elements are hidden initially
        if (hoverUnlock != null) hoverUnlock.SetActive(false);
        if (hoverLock != null) hoverLock.SetActive(false);
    }

    void Update()
    {
        // Read CAN messages
        status = PCANBasic.Read(deviceHandle, out msg, out timeStamp);

        if (status == TPCANStatus.PCAN_ERROR_OK)
        {
            // Parse CAN message data
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

            // Combine quaternion data once both are received
            if (isReceived1 && isReceived2)
            {
                finalQuaternion = new Quaternion(x, -y, -z, w); // Adapt axes

                // Convert quaternion to Euler angles
                Vector3 eulerAngles = finalQuaternion.eulerAngles;

                // Use Euler x-angle to show/hide UI elements
                if (eulerAngles.x >= 7 && eulerAngles.x <= 15)
                {
                    if (hoverUnlock != null) hoverUnlock.SetActive(true);
                    if (hoverLock != null) hoverLock.SetActive(false);
                }
                else if (eulerAngles.x >= 330 && eulerAngles.x <= 340)
                {
                    if (hoverUnlock != null) hoverUnlock.SetActive(false);
                    if (hoverLock != null) hoverLock.SetActive(true);
                }
                else
                {
                    if (hoverUnlock != null) hoverUnlock.SetActive(false);
                    if (hoverLock != null) hoverLock.SetActive(false);
                }

                // Reset flags
                isReceived1 = false;
                isReceived2 = false;
            }
        }
        else if (status != TPCANStatus.PCAN_ERROR_QRCVEMPTY)
        {
            Debug.LogError("Error reading CAN message: " + status);
        }
    }

    void OnApplicationQuit()
    {
        // Uninitialize PCAN-USB device
        PCANBasic.Uninitialize(deviceHandle);
    }
}
