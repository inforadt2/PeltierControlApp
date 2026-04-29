#include <Wire.h>
#include <LiquidCrystal_I2C.h>
#include <Adafruit_SHT31.h>
#include <Adafruit_MAX31865.h>
#include <PID_v1.h>
#include <OneWire.h>
#include <DallasTemperature.h>

#define RPWM_PIN 5      
#define LPWM_PIN 6      
#define R_EN_PIN 7      
#define L_EN_PIN 8      
#define MAX_CS_PIN 10    
#define ONE_WIRE_BUS 2

Adafruit_SHT31 sht31 = Adafruit_SHT31();
Adafruit_MAX31865 rtd = Adafruit_MAX31865(MAX_CS_PIN, 11, 12, 13);
OneWire oneWire(ONE_WIRE_BUS);
DallasTemperature sensors(&oneWire);
LiquidCrystal_I2C lcd(0x27, 16, 2);
double Setpoint = 0.0, Input, Output;

double Kp = 12.0;  
double Ki = 0.1; 
double Kd = 20.0;

float tempPT100 = 0.0, humidity = 0.0, tempDS = 0.0, tempSHT = 0.0;
bool isSystemOn = false; 
int currentPower = 0;
int powerRampStep = 15;
int lastDirection = 0;  // 1=쿨링, -1=히팅, 0=정지
unsigned long lastUpdate = 0;
PID myPID(&Input, &Output, &Setpoint, Kp, Ki, Kd, DIRECT);

void setup() {
  Serial.begin(9600);
  Wire.begin();
  Wire.setWireTimeout(3000, true); 
  
  lcd.init(); lcd.backlight();
  sht31.begin(0x44);
  rtd.begin(MAX31865_3WIRE);
  sensors.begin();
  
  pinMode(RPWM_PIN, OUTPUT); pinMode(LPWM_PIN, OUTPUT);
  pinMode(R_EN_PIN, OUTPUT); pinMode(L_EN_PIN, OUTPUT);
  digitalWrite(R_EN_PIN, HIGH); digitalWrite(L_EN_PIN, HIGH);
  
  myPID.SetMode(AUTOMATIC);
  myPID.SetOutputLimits(-255, 255); 
  myPID.SetSampleTime(1000);
}

void loop() {
  if (Serial.available() > 0) {
    String cmd = Serial.readStringUntil('\n');
    cmd.trim();
    if (cmd.startsWith("SET_TEMP:")) Setpoint = cmd.substring(9).toFloat();
    else if (cmd == "POWER:ON") isSystemOn = true;
    else if (cmd == "POWER:OFF") { isSystemOn = false; stopPeltier(); }
  }

  if (millis() - lastUpdate > 1000) {
    lastUpdate = millis();
    tempPT100 = rtd.temperature(100.0, 430.0);
    tempDS = sensors.getTempCByIndex(0);
    sensors.requestTemperatures();
    
    float h = sht31.readHumidity();
    float t = sht31.readTemperature();
    if (!isnan(h)) humidity = h;
    if (!isnan(t)) tempSHT = t;

    if (isSystemOn) {
      Input = tempPT100;

      if (Input < Setpoint && Output < 0) {
         myPID.SetMode(MANUAL);
         Output = Output * 0.2;
         myPID.SetMode(AUTOMATIC);
      }

      myPID.Compute();
      controlPeltier(Output);
    } else {
      stopPeltier();
    }
    sendJson();
    updateLCD();
  }
}

void controlPeltier(double targetOut) {
  bool newCooling = targetOut > 1.0;
  bool newHeating = targetOut < -1.0;

  // 방향 전환 감지: 현재 반대 방향으로 전환 요청 시 먼저 0으로 감속
  if ((newCooling && lastDirection == -1) || (newHeating && lastDirection == 1)) {
    currentPower -= powerRampStep;
    if (currentPower <= 0) {
      currentPower = 0;
      lastDirection = 0;  // 0이 되면 방향 초기화 → 다음 루프에서 정상 진입
    }
    analogWrite(RPWM_PIN, 0);
    analogWrite(LPWM_PIN, 0);
    return;  // 0될 때까지 대기
  }

  // 정상 제어
  int absPower = (int)abs(targetOut);
  if (currentPower < absPower) currentPower += powerRampStep;
  else if (currentPower > absPower) currentPower -= powerRampStep;
  currentPower = constrain(currentPower, 0, 255);

  if (newCooling) {
    lastDirection = 1;
    analogWrite(RPWM_PIN, 0); 
    analogWrite(LPWM_PIN, currentPower); 
  } 
  else if (newHeating) {
    lastDirection = -1;
    analogWrite(RPWM_PIN, currentPower); 
    analogWrite(LPWM_PIN, 0); 
  } 
  else {
    lastDirection = 0;
    stopPeltier();
  }
}

void stopPeltier() {
  analogWrite(RPWM_PIN, 0); analogWrite(LPWM_PIN, 0);
  currentPower = 0;
  lastDirection = 0;
  Output = 0;
}

void sendJson() {
  int pwrPercent = map(currentPower, 0, 255, 0, 100);
  Serial.print("{\"pt100\":"); Serial.print(tempPT100, 1);
  Serial.print(",\"hum\":"); Serial.print(humidity, 1);
  Serial.print(",\"senT\":"); Serial.print(tempSHT, 1); 
  Serial.print(",\"ds\":"); Serial.print(tempDS, 1);
  Serial.print(",\"set\":"); Serial.print(Setpoint, 1);
  Serial.print(",\"pwr\":"); Serial.print(pwrPercent);
  Serial.println("}");
}

void updateLCD() {
  int pwrPercent = map(currentPower, 0, 255, 0, 100);
  static int refreshCounter = 0;
  refreshCounter++;
  if (refreshCounter >= 5) {
    lcd.init(); lcd.backlight();
    refreshCounter = 0;
  } else {
    lcd.clear();
  }

  lcd.setCursor(0, 0);
  lcd.print("P:"); lcd.print(tempPT100, 1);
  lcd.print(" SV:"); lcd.print(Setpoint, 1);
  
  lcd.setCursor(0, 1);
  lcd.print("H:"); lcd.print((int)humidity);
  lcd.print("% D:"); lcd.print((int)(tempDS + 0.5));
  lcd.print(" W:"); lcd.print(pwrPercent); lcd.print("%");
}