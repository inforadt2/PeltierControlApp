using System.IO;
using PeltierControlApp.Models;
using System.IO.Ports;
using System.Reflection.Metadata;
using System.Text.Json;

namespace PeltierControlApp.Services;

public class PeltierService : IDisposable
{
    private SerialPort? _port;
    private readonly SettingsService _settings;
    private Timer? _loggingTimer;
    private StreamWriter? _logWriter;
    private readonly object _dataLock = new();
    private readonly CancellationTokenSource _cts = new(); // 종료 신호용

    private PeltierData _current = new();
    public PeltierData Current
    {
        get { lock (_dataLock) return _current; }
        private set { lock (_dataLock) _current = value; }
    }

    public event Action? OnDataUpdated;
    public bool IsLogging { get; private set; }

    public PeltierService(SettingsService settings)
    {
        _settings = settings;
        Task.Run(() => ConnectionLoop(_cts.Token)); // 토큰 전달
    }

    private string? _controllerIp; // 현재 제어 중인 IP 주소

    // 1. 포트 연결 상태 프로퍼티
    public bool IsConnected => _port?.IsOpen ?? false;

    // 제어권 관리 프로퍼티
    public string? ControllerIp => _controllerIp;

    // 제어권 획득 메서드
    public void TakeControl(string ip) => _controllerIp = ip;

    private async Task ConnectionLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // 포트가 없거나 닫혀있으면 연결 시도
                if (_port == null || !_port.IsOpen)
                {
                    // 이전 포트 객체 정리
                    if (_port != null)
                    {
                        try { _port.DataReceived -= HandleData; _port.Dispose(); } catch { }
                        _port = null;
                    }

                    _port = new SerialPort(_settings.Settings.ComPort, 9600);
                    _port.ReadTimeout = 3000;
                    _port.NewLine = "\n";

                    // 👇 아두이노 통신을 위해 반드시 추가해야 하는 두 줄입니다.
                    _port.DtrEnable = true;
                    _port.RtsEnable = true;

                    _port.Open();
                    _port.DiscardInBuffer();
                    _port.DataReceived += HandleData;

                    System.Diagnostics.Debug.WriteLine("아두이노 연결 성공");
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("아두이노 연결 대기 중...");
            }

            // 5초 대기 (종료 신호가 오면 즉시 대기 중단)
            try { await Task.Delay(5000, token); } catch { break; }
        }
    }

    private void HandleData(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port == null || !_port.IsOpen) return;
        try
        {
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
        var fullPath = Path.IsPathRooted(folder) ? folder : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);

        try
        {
            Directory.CreateDirectory(fullPath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filePath = Path.Combine(fullPath, $"peltier_{timestamp}.csv");

            _logWriter = new StreamWriter(filePath, true, System.Text.Encoding.UTF8);

            // [수정] 헤더에 SEN0546 온도 추가 (PT100과 습도 사이)
            _logWriter.WriteLine("PC시간,현재 온도(PT100)(°C),현재 온도(SEN0546)(°C),현재 습도(%),타겟 온도(°C),파워(%)");

            var interval = _settings.Settings.LoggingIntervalSeconds * 1000;
            _loggingTimer = new Timer(LogData, null, interval, interval);
            IsLogging = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Log Init Error: {ex.Message}");
        }
    }

    private void LogData(object? state)
    {
        if (_logWriter == null) return;
        var data = Current;

        try
        {
            // 4. 타겟 온도(Set) 소수점 1자리(F1) 포맷 지정
            _logWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{data.Pt100:F1},{data.SenT:F1},{data.Hum:F1},{data.Set:F1},{data.Pwr}");
            _logWriter.Flush();
        }
        catch { }
    }

    private void StopLogging()
    {
        _loggingTimer?.Dispose();
        _loggingTimer = null;
        _logWriter?.Dispose(); // 파일 닫기
        _logWriter = null;
        IsLogging = false;
    }

    public void Dispose()
    {
        _cts.Cancel(); // 백그라운드 루프 안전하게 종료
        StopLogging();
        if (_port != null)
        {
            try { _port.DataReceived -= HandleData; _port.Dispose(); } catch { }
        }
    }
}
