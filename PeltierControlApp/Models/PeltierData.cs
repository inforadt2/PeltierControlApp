namespace PeltierControlApp.Models;

public class PeltierData
{
    public double Liq { get; set; }   // 프로브 온도 (liq)
    public double Hum { get; set; }   // 습도 (hum)
    public double DhtT { get; set; }  // 온습도계 온도 (dhtT)
    public double Set { get; set; }   // 설정된 목표 온도 (set)
    public int Pwr { get; set; }      // 현재 출력 % (pwr)
}