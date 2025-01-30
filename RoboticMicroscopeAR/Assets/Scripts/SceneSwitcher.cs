using UnityEngine;
using System.Collections;
using System.IO.Ports;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    public string portName = "COM5"; // Replace with the actual port name
    public int baudRate = 115200; // Replace with the actual baud rate
    public string intro2SceneName = "Intro2"; // Name of the scene to switch to

    private SerialPort serialPort;
    private bool receivedOne = false;
    private bool receivedZero = false;

    void Start()
    {
        // Open the serial port
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.Open();
            Debug.Log("Serial port opened successfully.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error opening serial port: " + ex.Message);
        }
    }

    void Update()
    {
        // Read data from the serial port
        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                if (serialPort.BytesToRead > 0)
                {
                    byte[] buffer = new byte[serialPort.BytesToRead];
                    serialPort.Read(buffer, 0, buffer.Length);

                    // Check received data
                    if (buffer[0] == 0x31) // 0x31 is the ASCII code for '1'
                    {
                        receivedOne = true;
                        Debug.Log("Received '1'");
                    }
                    else if (buffer[0] == 0x30) // 0x30 is the ASCII code for '0'
                    {
                        receivedZero = true;
                        Debug.Log("Received '0'");
                    }

                    // Check if both '1' and '0' are received
                    if (receivedOne && receivedZero)
                    {
                        // Switch to the "Intro2" scene
                        Debug.Log("Switching to scene: " + intro2SceneName);
                        SceneManager.LoadScene(intro2SceneName);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Error reading from serial port: " + ex.Message);
            }
        }
    }

    void OnApplicationQuit()
    {
        // Close the serial port
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }
}