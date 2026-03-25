using System.IO.Ports;
using System.Text.Json;
using PeltierControlApp.Models;

namespace PeltierControlApp.Services;

public class PeltierService : IDisposable
{
    private SerialPort? _port;
    public PeltierData Current { get; private set; } = new();
    public event Action? OnDataUpdated;

    public PeltierService()
    {
        try
        {

            _port = new SerialPort("COM4", 9600);
            _port.ReadTimeout = 2000; // 0.5초 타임아웃
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

            // ReadExisting 대신 ReadLine을 사용하여 \n이 올 때까지 대기하여 한 줄을 온전히 가져옵니다.
            string line = _port.ReadLine().Trim();

            int startIndex = line.IndexOf('{');
            if (startIndex == -1) return;

            // 역직렬화 시도
            var data = JsonSerializer.Deserialize<PeltierData>(line.Substring(startIndex),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data != null)
            {
                Current = data;

                // [핵심 수정] 목표 온도가 설정되어 있을 때만 작동
                //if (Current.Set > 0)
                //{
                //    // 현재 온도(ShtT)와 목표 온도(Set)의 차이가 0.5도 이내면 정지
                //    // 절댓값(Math.Abs)을 사용하면 가열/냉각 구분 없이 정교하게 멈춥니다.
                //    if (Math.Abs(Current.Set - Current.ShtT) <= 0.5)
                //    {
                //        StopDevice(); // POWER:OFF 및 SET_TEMP:0.0 전송
                //    }
                //}
                OnDataUpdated?.Invoke();
            }
        }
        catch (TimeoutException) { /* 읽기 시간 초과 시 무시 */ }
        catch (Exception ex)
        {
            // 로그를 남겨 어떤 에러인지 확인하는 것이 좋습니다.
            System.Diagnostics.Debug.WriteLine($"Serial Error: {ex.Message}");
        }
    }

    public void SetTarget(double temp) => _port?.WriteLine($"SET_TEMP:{temp}");
    public void StartDevice() => _port?.WriteLine("POWER:ON");
    public void StopDevice()
    {
        if (_port != null && _port.IsOpen)
        {
            _port.WriteLine("POWER:OFF");    // [추가] 시스템 전원 강제 차단
            _port.WriteLine("SET_TEMP:0.0"); // 목표 온도 초기화
            _port.BaseStream.Flush();
            Console.WriteLine("Command Sent: Power Off & Stop");
        }
    }
    public void Dispose() => _port?.Dispose();
}