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

// [튜닝] Kp는 낮추고, Ki는 아주 작게, Kd는 높여서 브레이크를 강하게 잡습니다.
double Kp = 8.0;  
double Ki = 0.05; 
double Kd = 15.0; 

float tempPT100 = 0.0, humidity = 0.0, tempDS = 0.0;
bool isSystemOn = false; 
int currentPower = 0;
int powerRampStep = 10; // 반응 속도를 위해 램프 단계를 높임
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
    if (!isnan(h)) humidity = h;

    if (isSystemOn) {
      Input = tempPT100;

      // [핵심] 목표 온도보다 낮아지면(오버슈트 발생 시) PID 적분항을 강제로 억제
      if (Input < Setpoint && Output < 0) {
        // 냉각 중인데 목표보다 낮아졌다면 출력을 급격히 줄임
         myPID.SetMode(MANUAL);
         Output = Output * 0.2; // 출력을 20% 수준으로 급감
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
  int absPower = (int)abs(targetOut);
  
  if (currentPower < absPower) currentPower += powerRampStep;
  else if (currentPower > absPower) currentPower -= powerRampStep;
  currentPower = constrain(currentPower, 0, 255);

  // 배선을 바꾸셨으므로 현재 방향에 맞춰 작동
  // targetOut이 음수(-)일 때 냉각(LPWM)이 작동하는 구조
  if (targetOut > 1.0) { 
    analogWrite(RPWM_PIN, 0); 
    analogWrite(LPWM_PIN, currentPower); 
  } 
  else if (targetOut < -1.0) { 
    analogWrite(RPWM_PIN, currentPower); 
    analogWrite(LPWM_PIN, 0); 
  } 
  else {
    stopPeltier();
  }
}

void stopPeltier() {
  analogWrite(RPWM_PIN, 0); analogWrite(LPWM_PIN, 0);
  currentPower = 0;
  Output = 0; // PID 출력 초기화
}

void sendJson() {
  int pwrPercent = map(currentPower, 0, 255, 0, 100);
  Serial.print("{\"pt100\":"); Serial.print(tempPT100, 1);
  Serial.print(",\"hum\":"); Serial.print(humidity, 1); // 습도 추가
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