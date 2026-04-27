using System.IO.Ports;
using System.Text.Json;
using PeltierControlApp.Models;

namespace PeltierControlApp.Services;

public class PeltierService : IDisposable
{
    private SerialPort? _port;
    private readonly SettingsService _settings;
    private Timer? _loggingTimer;
    private string? _currentLogFile;

    public PeltierData Current { get; private set; } = new();
    public event Action? OnDataUpdated;
    public bool IsLogging { get; private set; }

    public PeltierService(SettingsService settings)
    {
        _settings = settings;
        try
        {
            _port = new SerialPort(_settings.Settings.ComPort, 9600);
            _port.ReadTimeout = 2000;
            _port.Open();
            _port.DiscardInBuffer();
            _port.DataReceived += HandleData;
        }
        catch { }
    }

    private void HandleData(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_port == null || !_port.IsOpen) return;

            string line = _port.ReadLine().Trim();
            int startIndex = line.IndexOf('{');
            if (startIndex == -1) return;

            var data = JsonSerializer.Deserialize<PeltierData>(line.Substring(startIndex),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data != null)
            {
                Current = data;
                OnDataUpdated?.Invoke();
            }
        }
        catch (TimeoutException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Serial Error: {ex.Message}");
        }
    }

    public void SetTarget(double temp) => _port?.WriteLine($"SET_TEMP:{temp}");

    public void StartDevice()
    {
        _port?.WriteLine("POWER:ON");
        StartLogging();
    }

    public void StopDevice()
    {
        if (_port != null && _port.IsOpen)
        {
            _port.WriteLine("POWER:OFF");
            _port.WriteLine("SET_TEMP:0.0");
            _port.BaseStream.Flush();
            Console.WriteLine("Command Sent: Power Off & Stop");
        }
        StopLogging();
    }

    private void StartLogging()
    {
        var folder = _settings.Settings.LoggingFolder;
        try { Directory.CreateDirectory(folder); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logging folder error: {ex.Message}");
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _currentLogFile = Path.Combine(folder, $"peltier_{timestamp}.csv");

        // 헤더 작성
        File.WriteAllText(_currentLogFile, "PC시간,현재 온도(°C),타겟 온도(°C),현재 습도(%),파워(%)\n", System.Text.Encoding.UTF8);

        var interval = _settings.Settings.LoggingIntervalSeconds * 1000;
        _loggingTimer = new Timer(LogData, null, interval, interval);
        IsLogging = true;
    }

    private void LogData(object? state)
    {
        if (_currentLogFile == null) return;
        try
        {
            var line = string.Join(",", [
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Current.Pt100.ToString("F1"),
                Current.Set.ToString(),
                Current.Hum.ToString("F1"),
                Current.Pwr.ToString()
            ]) + "\n";
            File.AppendAllText(_currentLogFile, line, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logging write error: {ex.Message}");
        }
    }

    private void StopLogging()
    {
        _loggingTimer?.Dispose();
        _loggingTimer = null;
        IsLogging = false;
        Console.WriteLine($"Log saved: {_currentLogFile}");
        _currentLogFile = null;
    }

    public void Dispose()
    {
        StopLogging();
        _port?.Dispose();
    }
}
