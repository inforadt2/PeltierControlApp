namespace PeltierControlApp.Models;

public class PeltierData
{
    public double Pt100 { get; set; }
    public double Hum { get; set; }
    public double SenT { get; set; } // SEN0546 온도 필드 추가
    public double Ds { get; set; }
    public double Set { get; set; }
    public int Pwr { get; set; }
}