namespace PeltierControlApp.Models;

public class PeltierData
{
    public double Pt100 { get; set; }  // PT100 온도 (메인 표시용)
    public double Hum { get; set; }    // SEN0546 습도
    public double ShtT { get; set; }   // SEN0546 온도 (백업용)
    public double Set { get; set; }    // 목표 온도
    public int Pwr { get; set; }       // 출력 %
}