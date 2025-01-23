#include <Adafruit_NeoPixel.h>
#include <Arduino.h>
#include <ESP32Encoder.h>

// Pin assignments
#define ZDIR_PIN 18
#define ZPWM_PIN 19
#define LIMIT_SWITCH_PIN 15
#define ENCODER_PIN_A 22
#define ENCODER_PIN_B 23

#define But         4     // Button Pin
#define LED_PIN     13    // Pin connected to the LED ring
#define NUM_LEDS    8   // Number of LEDs in the ring
Adafruit_NeoPixel ring = Adafruit_NeoPixel(NUM_LEDS, LED_PIN, NEO_GRB + NEO_KHZ800);

const int XDIR = 27; // Motor X Axis Direction
const int XPWM = 14; // Motor X Axis PWM
const int YDIR = 25; // Motor Y Axis Direction
const int YPWM = 26; // Motor Y Axis PWM

const int PotX = 35; // X-axis potentiometer
const int PotY = 32;  // Y-axis potentiometer
int OffsetX = 1941;  // center offsets for potentiometers
int OffsetY = 2262;  // center offsets for potentiometers

// Controller X
int SensorValueX, errorX, pwmValueX, outputX = 0;
float kpX = 0.02, kiX = 0.015, kdX = 1.2; // PID constants for X-axis

// Array to store the last 5 readings
const int numReadings = 5;
int readingsX[numReadings]; // Array to hold the readings
int readIndexX = 0;          // Index of the current reading
int integralX = 0;           // Variable to store the sum of the array
int previouserrorX = 0;      // for derivative part
int derivativeX = 0;
float alpha = 0.3;        // Smoothing factor for X and Y (0 < alpha < 1)
float FilteredValueX = 0; // Stores the filtered sensor value

// COntrollerY

int SensorValueY, errorY, pwmValueY, outputY = 0;
float kpY = 0.02, kiY = 0.015, kdY = 0.6; // PID constants for Y-axis

// Array to store the last 5 readings
int readingsY[numReadings]; // Array to hold the readings
int integralY = 0;           // Variable to store the sum of the array
int previouserrorY = 0;      // for derivative part
int readIndexY= 0;          // Index of the current reading
int derivativeY = 0;
float FilteredValueY = 0; // Stores the filtered sensor value

//Control Variables
int desiredX = 0;
int desiredY = 0;
int desiredZ = 0;
int desiredL = 0;
bool Button = false;

// TIMER
unsigned long previousMillis1 = 0;   // Last recorded time
const unsigned long interval1 = 10;


// Initialize encoder
ESP32Encoder encoder;

// Controller Z
int SensorValueZ, errorZ, pwmValueZ, outputZ = 0;
float kpZ = 0.002; float kiZ = 0.001; float kdZ = 0; // PID constants for Z-axis

// Array to store the last 5 readings
int readingsZ[numReadings]; // Array to hold the readings
int readIndexZ = 0;          // Index of the current reading
int integralZ = 0;           // Variable to store the sum of the array
int previouserrorZ = 0;      // for derivative part
int derivativeZ = 00;

bool limitSwitch = 0;


void setup() {
  Serial.begin(115200);

  // Set direction and pwm pins as output
  pinMode(XDIR, OUTPUT);
  pinMode(XPWM, OUTPUT);
  pinMode(YDIR, OUTPUT);
  pinMode(YPWM, OUTPUT);

  // Configure PWM channels
  ledcSetup(0, 25000, 8); // Channel 0, 25 kHz frequency, 8-bit resolution
  ledcAttachPin(XPWM, 0);

  ledcSetup(1, 25000, 8); // Channel 0, 25 kHz frequency, 8-bit resolution
  ledcAttachPin(YPWM, 1);

  // Initialize the readings array to 0
  for (int i = 0; i < numReadings; i++) {
    readingsX[i] = 0;
    readingsY[i] = 0;
  }

  ring.begin();
  ring.show(); // Initialize all LEDs to off

  // Set pin modes
  pinMode(ZDIR_PIN, OUTPUT);
  pinMode(ZPWM_PIN, OUTPUT);
  pinMode(LIMIT_SWITCH_PIN, INPUT_PULLDOWN); // Use INPUT_PULLDOWN for a 3.3V-connected limit switch

  // Set up encoder pins with pull-up resistors
  pinMode(ENCODER_PIN_A, INPUT_PULLUP);
  pinMode(ENCODER_PIN_B, INPUT_PULLUP);

  // Initialize encoder
  encoder.attachHalfQuad(ENCODER_PIN_A, ENCODER_PIN_B);  // Attach encoder pins
  encoder.clearCount();  // Reset encoder count

  // Set higher PWM frequency for quieter motor operation
  ledcSetup(2, 20000, 8); // Channel 0, 20 kHz frequency, 8-bit resolution
  ledcAttachPin(ZPWM_PIN, 2);
  Serial.println("setup error");
  calibrateEncoder();

}

void loop() {
  
  unsigned long currentMillis = millis(); // Get the current time
  if (currentMillis - previousMillis1 >= interval1) {
    previousMillis1 = currentMillis; // Save the current time
    // Perform tasks every given interval (10ms)
    readSerial();
    ReadButton();
    finderrorXY();
    controlXY();
    setLED(desiredL*25);
    controlZ();

    //debugZ();
    //debugY();
    //debugX();
    //debugSensorValues();
  }
}

void finderrorXY() {
  // X-axis calculations
  SensorValueX = analogRead(PotX) - OffsetX;
  FilteredValueX = alpha * SensorValueX + (1 - alpha) * FilteredValueX;
  
  previouserrorX = errorX;
  errorX = desiredX*10 - FilteredValueX; // changes degress into potentiometer scale (-45-45° = -450-450)
  derivativeX = errorX - previouserrorX;
  // Subtract the value at the current index from the sum
  integralX -= readingsX[readIndexX];
  // Save the new reading in the array
  readingsX[readIndexX] = errorX;
  // Add the new value to the sum
  integralX += errorX;
  // Increment the index, wrapping around if necessary
  readIndexX = (readIndexX + 1) % numReadings;


  // Y-axis calculations
  SensorValueY = analogRead(PotY) - OffsetY;
  FilteredValueY = alpha * SensorValueY + (1 - alpha) * FilteredValueY;
  
  previouserrorY = errorY;
  errorY = desiredY*13 - FilteredValueY;
  derivativeY = errorY - previouserrorY;
  // Subtract the value at the current index from the sum
  integralY -= readingsY[readIndexY];
  // Save the new reading in the array
  readingsY[readIndexY] = errorY;
  // Add the new value to the sum
  integralY += errorY;
  // Increment the index, wrapping around if necessary
  readIndexY = (readIndexY + 1) % numReadings;

}

void controlXY() {

  // Use the sum of the last 10 error values for the integral term
  outputX = kpX * errorX + kiX * integralX + kdX * derivativeX;

  digitalWrite(XDIR, outputX > 0); //writes high or low depending on the sign of the output
  
  if(abs(outputX) > 2){
    if(abs(integralX) > 100){
      pwmValueX = map(abs(outputX), 0, 100, 40, 70);
      //setLED(0);
    }
    else {pwmValueX = 0;} //setLED(250);} //DEBUG

  }
  else pwmValueX = 0;

  ledcWrite(0, pwmValueX);


  // Use the sum of the last 10 error values for the integral term
  outputY = kpY * errorY + kiY * integralY + kdY * derivativeY;

  digitalWrite(YDIR, outputY < 0); //writes high or low depending on the sign of the output
  
  if(abs(outputY) > 2){
    if(abs(integralY) > 100){
      pwmValueY = map(abs(outputY), 0, 100, 30, 50);
    }
    else pwmValueX = 0;
  }
  else pwmValueY = 0;

  ledcWrite(1, pwmValueY);


}

void debugX() {
  Serial.print("r: ");
  Serial.print((int)FilteredValueX);
  Serial.print("\t\tp: ");
  Serial.print(errorX);

    Serial.print("\t\tP: ");
  Serial.print((int)(errorX * kpX));

  Serial.print("\t\ti: ");
  Serial.print((int)(integralX));

  Serial.print("\t\tI: ");
  Serial.print((int)(integralX * kiX));

  Serial.print("\t\td: ");
  Serial.print((int)(derivativeX));

  Serial.print("\t\tD: ");
  Serial.print((int)(derivativeX * kdX));

  Serial.print("\t\t\to: ");
  Serial.print(outputX);

  Serial.print("\t\t\tPWM: ");
  Serial.println(pwmValueX);
}

void debugY() {
  Serial.print("r: ");
  Serial.print((int)FilteredValueY);
  Serial.print("\t\tp: ");
  Serial.print(errorY);

  Serial.print("\t\tP: ");
  Serial.print((int)(errorY * kpY));

  Serial.print("\t\ti: ");
  Serial.print((int)(integralY));

  Serial.print("\t\tI: ");
  Serial.print((int)(integralY * kiY));

  Serial.print("\t\td: ");
  Serial.print((int)(derivativeY));

  Serial.print("\t\tD: ");
  Serial.print((int)(derivativeY * kdY));

  Serial.print("\t\t\to: ");
  Serial.print(outputY);

  Serial.print("\t\t\tPWM: ");
  Serial.println(pwmValueY);
}

void debugSensorValues() {
  Serial.print("X: ");
  Serial.print((int)FilteredValueX);
  Serial.print("\t\tY: ");
  Serial.println((int)FilteredValueY);
}

void setLED(int brightness){
  for (int i = 0; i < NUM_LEDS; i++) {
    ring.setPixelColor(i, ring.Color(brightness, brightness, brightness)); // RGB: White
  }
  ring.show();
}

void ReadButton(){
  if(digitalRead(But)!= Button)
  {
    Button = digitalRead(But);
    Serial.println(Button);
  }
}

void readSerial() {
  if (Serial.available() > 0) {
    String input = Serial.readStringUntil('\n');
    if (input.length() > 5) {
      int xIndex = input.indexOf('X');
      int yIndex = input.indexOf('Y');
      int zIndex = input.indexOf('Z');
      int lIndex = input.indexOf('L');

      if (xIndex != -1) {
        desiredX = input.substring(xIndex + 1, input.indexOf(',', xIndex)).toInt();
        if(desiredX>45){desiredX = 45;}
        else if(desiredX<-45){desiredX=-45;}
      }
      if (yIndex != -1) {
        desiredY = input.substring(yIndex + 1, input.indexOf(',', yIndex)).toInt();
        if(desiredY>20){desiredY = 20;}
        else if(desiredY<-70){desiredY=-70;}
      }
      if (zIndex != -1) {
        desiredZ = input.substring(zIndex + 1, input.indexOf(',', zIndex)).toInt();
        desiredZ = desiredZ-65;
        if(desiredZ>0){desiredZ = 0;}
        else if(desiredZ<-100){desiredZ=-100;}
      }
      if (lIndex != -1) {
        desiredL = input.substring(lIndex + 1, input.indexOf(',', lIndex)).toInt();
  
      }
      // Print the parsed values for debugging
      //Serial.print("desiredX: "); Serial.println(desiredX);
      //Serial.print("desiredY: "); Serial.println(desiredY);
      //Serial.print("desiredZ: "); Serial.println(desiredZ);
      //Serial.print("desiredL: "); Serial.println(desiredL);
    }
  }
}

void controlZ(){
  limitSwitch = digitalRead(LIMIT_SWITCH_PIN);
    if(limitSwitch){
      digitalWrite(ZDIR_PIN, LOW);  // Move down
      ledcWrite(2, 40);
      delay(300);
      ledcWrite(2, 0);
      delay(300);
      Serial.println("limit pressed");
      calibrateEncoder();
    }
    else{
      finderrorZ();
      moveZ();
      //debugZ();
    }
}

void calibrateEncoder(){
  Serial.println("Start Encoder Calibration");
  limitSwitch = digitalRead(LIMIT_SWITCH_PIN);
  while(limitSwitch == 0){
    digitalWrite(ZDIR_PIN, HIGH);  // Move upward
    ledcWrite(2, 80);
    limitSwitch = digitalRead(LIMIT_SWITCH_PIN);
  }
  ledcWrite(2, 0);
  digitalWrite(ZDIR_PIN, LOW);  // Move down
  ledcWrite(2, 40);
  delay(300);
  ledcWrite(2, 0);
  delay(300);
  encoder.clearCount();
  Serial.println("Encoder Calibrated");
  Serial.println(encoder.getCount());
}

void finderrorZ(){
  // Z-axis calculations
  SensorValueZ = encoder.getCount();
  previouserrorZ = errorZ;
  errorZ = desiredZ*1000 - SensorValueZ; // changes degress into potentiometer scale (-45-45° = -450-450)
  derivativeZ = errorZ - previouserrorZ; // Subtract the value at the current index from the sum
  integralZ -= readingsZ[readIndexZ]; // Save the new reading in the array
  readingsZ[readIndexZ] = errorZ; // Add the new value to the sum
  integralZ += errorZ; // Increment the index, wrapping around if necessary
  readIndexZ = (readIndexZ + 1) % numReadings;
}

void moveZ(){
  outputZ = kpZ * errorZ + kiZ * integralZ + kdZ * derivativeZ;
  limitSwitch = digitalRead(LIMIT_SWITCH_PIN);
  if(limitSwitch){
    ledcWrite(2, 0);
    //Serial.println("limit switch pressed");
  }
  else if(errorZ>-1000 && errorZ<1000){
    ledcWrite(2, 0);
    //Serial.println("stopped");

  }
  else{
  // Use the sum of the last 10 error values for the integral term
    if(outputZ>100)outputZ=100;
    else if(outputZ<-100)outputZ=-100;
    digitalWrite(ZDIR_PIN, outputZ > 0); //writes high or low depending on the sign of the output
    if (outputZ>0){pwmValueZ = map(abs(outputZ), 0, 100, 50, 80);}
    else pwmValueZ = map(abs(outputZ), 0, 100, 90, 120);
    ledcWrite(2, pwmValueZ);
  }
}

void debugZ(){
  
  Serial.print("des: ");
  Serial.print((int)desiredZ);
  Serial.print("\t\tr: ");
  Serial.print((int)SensorValueZ);
  Serial.print("\t\tp: ");
  Serial.print(errorZ);

  Serial.print("\t\tP: ");
  Serial.print((int)(errorZ * kpZ));

  Serial.print("\t\ti: ");
  Serial.print((int)(integralZ));

  Serial.print("\t\tI: ");
  Serial.print((int)(integralZ * kiZ));

  Serial.print("\t\td: ");
  Serial.print((int)(derivativeZ));

  Serial.print("\t\tD: ");
  Serial.print((int)(derivativeZ * kdZ));

  Serial.print("\t\t\to: ");
  Serial.print(outputZ);

  Serial.print("\t\t\tPWM: ");
  Serial.println(pwmValueZ);
}