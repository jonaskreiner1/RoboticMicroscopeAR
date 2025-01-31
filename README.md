IMU-Controlled UI System with PCAN and Serial Communication
📌 Overview
This project is a Unity-based system that uses IMU data from a CAN bus (PCAN-USB) to control a UI interface. The system processes quaternion data from an IMU sensor, interprets user movement, and enables interaction with a UI. It also communicates via a serial port (COM4, 115200 baud rate) to send and receive data.

🛠️ Features
✅ IMU-based Control - Reads quaternion data, converts it into angles, and maps them to user interactions.
✅ PCAN Integration - Reads data from a PCAN-USB connection at 125Kbps (can be increased).
✅ Serial Communication - Receives button presses and sends IMU/zoom/light intensity data over COM8.
✅ Quest System - Checks if the user reaches predefined positions and brightness levels, marking tasks as complete.
✅ Optimized Performance - Reduces CPU load by optimizing CAN and Serial read rates.

⚙️ How It Works
Reads IMU data from PCAN and processes it into Euler angles.
Maps angles to UI elements, allowing the user to hover/select using head movements.
Receives serial input (button press) to confirm selections or enter modes.
Adjusts light intensity and zoom level based on the IMU data.
Tracks user progress and activates checkmarks when tasks are completed.
📈 Optimizations for Smooth Performance
PCAN reads every 3 frames (instead of every frame) to improve efficiency.
Serial thread optimized with controlled delays (Thread.Sleep(15ms)) to prevent lag.
IMU data rounded to integers to reduce unnecessary precision.
Higher CAN baud rate support (500Kbps if hardware allows).
🔧 Setup & Usage
Connect the IMU sensor to the PCAN-USB adapter and ensure CAN messages are sent.
Connect the serial device to COM8 (adjust in the script if needed).
Run the Unity scene, ensuring all GameObjects are linked in the Inspector.
Move your head to interact with UI elements and complete tasks.