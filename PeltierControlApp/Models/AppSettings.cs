namespace PeltierControlApp.Models;

public class AppSettings
{
    public string ComPort { get; set; } = "COM9";
    public string LoggingFolder { get; set; } = @"C:\PeltierLogs";
    public int LoggingIntervalSeconds { get; set; } = 1;
}
