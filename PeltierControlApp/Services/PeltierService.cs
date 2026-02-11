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
            _port = new SerialPort("COM4", 9600); // 본인의 포트에 맞게
            _port.Open();

            // [추가] 연결 직후 버퍼에 쌓여있던 찌꺼기 데이터를 지웁니다.
            _port.DiscardInBuffer();

            _port.DataReceived += HandleData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"포트 열기 실패: {ex.Message}");
        }
    }

    private void HandleData(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_port == null || !_port.IsOpen) return;
            string? line = _port.ReadLine();
            if (string.IsNullOrEmpty(line)) return;

            int startIndex = line.IndexOf('{');
            if (startIndex == -1) return;

            var data = JsonSerializer.Deserialize<PeltierData>(line.Substring(startIndex),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data != null)
            {
                Current = data;

                // [수정됨] 제어 기준이 DHT로 변경됨에 따라 예측 정지 로직도 DhtT 기준이어야 함
                if (Current.Set > 0)
                {
                    // 가열 중: 목표값보다 현재값(DhtT)이 낮지만, 목표 근처(0.5도)에 도달했을 때
                    // 조건: Set > DhtT (가열상황) && DhtT >= Set - 0.5
                    if (Current.Set > Current.DhtT && Current.DhtT >= (Current.Set - 0.5))
                    {
                        StopDevice(); // 관성을 고려해 미리 정지 명령 전송
                    }
                    // 냉각 중: 목표값보다 현재값(DhtT)이 높지만, 목표 근처에 도달했을 때
                    // 조건: Set < DhtT (냉각상황) && DhtT <= Set + 0.5
                    else if (Current.Set < Current.DhtT && Current.DhtT <= (Current.Set + 0.5))
                    {
                        StopDevice();
                    }
                }
                OnDataUpdated?.Invoke();
            }
        }
        catch { }
    }

    public void SetTarget(double temp) => _port?.WriteLine($"SET_TEMP:{temp}");
    public void StopDevice()
    {
        if (_port != null && _port.IsOpen)
        {
            _port.WriteLine("SET_TEMP:0.0");
            _port.BaseStream.Flush(); // 명령이 즉시 전송되도록 버퍼를 비움
            Console.WriteLine("Command Sent: Stop");
        }
    }
    public void Dispose() => _port?.Dispose();
}