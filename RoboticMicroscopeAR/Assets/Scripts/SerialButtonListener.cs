using System.Collections;
using System.IO.Ports; // Required for serial communication
using UnityEngine;

public class SerialButtonListener : MonoBehaviour
{
    private SerialPort serialPort; // Serial port object
    public string comPort = "COM6"; // Set the COM port here
    public int baudRate = 9600; // Match the baud rate of your ESP32

    private bool isButtonPressed = false; // Tracks the button state

    void Start()
    {
        // Initialize the serial port
        serialPort = new SerialPort(comPort, baudRate);
        try
        {
            serialPort.Open(); // Open the port
            serialPort.ReadTimeout = 100; // Set read timeout in milliseconds
            Debug.Log($"Serial port {comPort} opened at {baudRate} baud.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open serial port {comPort}: {e.Message}");
        }
    }

    void Update()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                // Read data from the serial port
                string data = serialPort.ReadLine().Trim(); // Read a line and trim extra spaces

                // Check if the data is "0" or "1"
                if (data == "1")
                {
                    if (!isButtonPressed) // Ensure this runs only when the state changes
                    {
                        Debug.Log("Button Pressed: Received 1");
                        isButtonPressed = true;
                        // You can trigger other actions here for button press
                    }
                }
                else if (data == "0")
                {
                    if (isButtonPressed) // Ensure this runs only when the state changes
                    {
                        Debug.Log("Button Released: Received 0");
                        isButtonPressed = false;
                        // You can trigger other actions here for button release
                    }
                }
            }
            catch (System.TimeoutException)
            {
                // Ignore timeout exceptions
            }
        }
    }

    private void OnApplicationQuit()
    {
        // Close the serial port when the application quits
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log($"Serial port {comPort} closed.");
        }
    }
}
