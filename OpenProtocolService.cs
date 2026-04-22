using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenProtocolInterpreter;
using OpenProtocolInterpreter.Alarm;
using OpenProtocolInterpreter.AutomaticManualMode;
using OpenProtocolInterpreter.Communication;
using OpenProtocolInterpreter.IOInterface;
using OpenProtocolInterpreter.Job;
using OpenProtocolInterpreter.KeepAlive;
using OpenProtocolInterpreter.ParameterSet;
using OpenProtocolInterpreter.Tightening;
using OpenProtocolInterpreter.Tool;
using OpenProtocolInterpreter.Vin;

namespace TighteningController;

public class OpenProtocolService : IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly MidInterpreter _interpreter;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _heartbeatTask;
    private bool _isConnected;
    private bool _disposed;
    private readonly object _sendLock = new object();

    private readonly Stopwatch _lastActivity = new Stopwatch();
    private readonly Stopwatch _lastHeartbeatSent = new Stopwatch();

    private const int HeartbeatIntervalMs = 10000;
    private const int HeartbeatTimeoutMs = 15000;
    private const int ReconnectDelayMs = 3000;
    private const int MaxReconnectAttempts = 5;

    private string _ipAddress = "";
    private int _port;
    private int _reconnectAttempts;
    private bool _autoReconnect;

    public event Action<string>? MessageReceived;
    public event Action<bool>? ConnectionStateChanged;
    public event Action? HeartbeatTimeout;
    public event Action<Mid0061>? TighteningResultReceived;
    public event Action<Mid0065>? OldTighteningResultReceived;
    public event Action<Mid0066>? OfflineResultCountReceived;
    public event Action<Mid0071>? AlarmReceived;
    public event Action<Mid0076>? AlarmStatusReceived;
    public event Action<Mid0401>? AutoManualModeChanged;
    public event Action<Mid0002>? CommunicationStartAccepted;
    public event Action<Mid0004>? CommandErrorReceived;
    public event Action<Mid0005>? CommandAcceptedReceived;
    public event Action<Mid0041>? ToolDataReceived;
    public event Action? Reconnecting;
    public event Action? ReconnectFailed;

    public bool IsConnected => _isConnected;
    public bool IsAlarmActive { get; private set; }
    public bool IsManualMode { get; private set; }

    public OpenProtocolService()
    {
        _interpreter = new MidInterpreter().UseAllMessages();
    }

    public async Task<bool> ConnectAsync(string ipAddress, int port, bool autoReconnect = true)
    {
        _ipAddress = ipAddress;
        _port = port;
        _autoReconnect = autoReconnect;
        _reconnectAttempts = 0;

        return await ConnectInternalAsync();
    }

    private async Task<bool> ConnectInternalAsync()
    {
        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.SendTimeout = 5000;
            _tcpClient.ReceiveTimeout = 0;
            await _tcpClient.ConnectAsync(_ipAddress, _port);
            _stream = _tcpClient.GetStream();
            _isConnected = true;
            _lastActivity.Restart();
            _lastHeartbeatSent.Restart();
            ConnectionStateChanged?.Invoke(true);

            _cts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token));
            _heartbeatTask = Task.Run(() => HeartbeatLoop(_cts.Token));
            _ = Task.Run(() => TimeoutLoop(_cts.Token));

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Connection error: {ex.Message}");
            _isConnected = false;
            ConnectionStateChanged?.Invoke(false);
            return false;
        }
    }

    public void Disconnect()
    {
        _autoReconnect = false;
        DisconnectInternal();
    }

    private void DisconnectInternal()
    {
        _cts?.Cancel();
        _isConnected = false;
        ConnectionStateChanged?.Invoke(false);

        try { _stream?.Close(); } catch { }
        try { _tcpClient?.Close(); } catch { }

        _stream = null;
        _tcpClient = null;
    }

    private async Task TryReconnectAsync()
    {
        if (!_autoReconnect) return;

        Reconnecting?.Invoke();

        while (_reconnectAttempts < MaxReconnectAttempts && _autoReconnect)
        {
            _reconnectAttempts++;
            Debug.WriteLine($"Reconnect attempt {_reconnectAttempts}/{MaxReconnectAttempts}...");

            try
            {
                DisconnectInternal();
                await Task.Delay(ReconnectDelayMs);

                if (await ConnectInternalAsync())
                {
                    _reconnectAttempts = 0;
                    Debug.WriteLine("Reconnected successfully!");
                    await RequestConnection();
                    await SubscribeToTighteningResults();
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Reconnect error: {ex.Message}");
            }
        }

        ReconnectFailed?.Invoke();
    }

    private async Task HeartbeatLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _isConnected)
        {
            try
            {
                if (!_isConnected) break;

                await Task.Delay(1000, token);

                if (!_isConnected) break;

                if (_lastActivity.ElapsedMilliseconds > HeartbeatIntervalMs &&
                    _lastHeartbeatSent.ElapsedMilliseconds > HeartbeatIntervalMs)
                {
                    var mid = new Mid9999(0);
                    var package = mid.Pack();
                    var data = Encoding.ASCII.GetBytes(package + '\0');

                    lock (_sendLock)
                    {
                        _stream?.WriteAsync(data, token);
                    }
                    _lastHeartbeatSent.Restart();
                    Debug.WriteLine("Heartbeat sent (MID9999)");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Heartbeat error: {ex.Message}");
            }
        }
    }

    private async Task TimeoutLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _isConnected)
        {
            try
            {
                await Task.Delay(1000, token);

                if (!_isConnected) break;

                if (_lastActivity.ElapsedMilliseconds > HeartbeatTimeoutMs)
                {
                    Debug.WriteLine("Heartbeat timeout! No data received for 15 seconds.");
                    HeartbeatTimeout?.Invoke();
                    HandleConnectionLost();
                    _ = TryReconnectAsync();
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Timeout check error: {ex.Message}");
            }
        }
    }

    private void HandleConnectionLost()
    {
        _isConnected = false;
        ConnectionStateChanged?.Invoke(false);

        try { _stream?.Close(); } catch { }
        try { _tcpClient?.Close(); } catch { }

        _stream = null;
        _tcpClient = null;
    }

    private async Task ReceiveLoop(CancellationToken token)
    {
        var buffer = new byte[8192];
        var messageBuffer = new StringBuilder();

        while (!token.IsCancellationRequested && _isConnected)
        {
            try
            {
                if (_stream == null || !_stream.CanRead) break;

                int bytesRead;
                try
                {
                    bytesRead = await _stream.ReadAsync(buffer, token);
                }
                catch (IOException)
                {
                    if (_isConnected)
                    {
                        HandleConnectionLost();
                        _ = TryReconnectAsync();
                    }
                    break;
                }

                if (bytesRead == 0)
                {
                    HandleConnectionLost();
                    _ = TryReconnectAsync();
                    break;
                }

                _lastActivity.Restart();

                var received = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(received);

                var content = messageBuffer.ToString();
                var messages = content.Split('\0', StringSplitOptions.RemoveEmptyEntries);

                if (content.EndsWith('\0'))
                {
                    foreach (var msg in messages)
                    {
                        ProcessMessage(msg.Trim());
                    }
                    messageBuffer.Clear();
                }
                else
                {
                    for (int i = 0; i < messages.Length - 1; i++)
                    {
                        ProcessMessage(messages[i].Trim());
                    }
                    messageBuffer.Clear();
                    messageBuffer.Append(messages[^1]);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Receive error: {ex.Message}");
            }
        }
    }

    private void ProcessMessage(string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message) || message.Length < 20)
            {
                Debug.WriteLine($"Invalid message (too short): {message}");
                return;
            }

            var mid = _interpreter.Parse(message);
            MessageReceived?.Invoke(mid.ToString() ?? "");

            switch (mid)
            {
                case Mid0002 commStartAck:
                    CommunicationStartAccepted?.Invoke(commStartAck);
                    break;

                case Mid0004 commandError:
                    CommandErrorReceived?.Invoke(commandError);
                    break;

                case Mid0005 commandAccepted:
                    CommandAcceptedReceived?.Invoke(commandAccepted);
                    break;

                case Mid0041 toolData:
                    ToolDataReceived?.Invoke(toolData);
                    break;

                case Mid0061 tighteningResult:
                    TighteningResultReceived?.Invoke(tighteningResult);
                    _ = SendTighteningAckAsync();
                    break;

                case Mid0065 oldTighteningResult:
                    OldTighteningResultReceived?.Invoke(oldTighteningResult);
                    break;

                case Mid0066 offlineResultCount:
                    OfflineResultCountReceived?.Invoke(offlineResultCount);
                    break;

                case Mid0071 alarm:
                    IsAlarmActive = true;
                    AlarmReceived?.Invoke(alarm);
                    _ = SendAlarmAckAsync(alarm.Header.Mid);
                    break;

                case Mid0074:
                    IsAlarmActive = false;
                    _ = SendAlarmStatusAckAsync(Mid0075.MID);
                    break;

                case Mid0076 alarmStatus:
                    IsAlarmActive = alarmStatus.AlarmStatus;
                    AlarmStatusReceived?.Invoke(alarmStatus);
                    _ = SendAlarmStatusAckAsync(Mid0077.MID);
                    break;

                case Mid0401 autoManualMode:
                    IsManualMode = autoManualMode.ManualAutomaticMode;
                    AutoManualModeChanged?.Invoke(autoManualMode);
                    _ = SendAutoManualModeAckAsync();
                    break;

                case Mid9999:
                    Debug.WriteLine("Keep alive response received");
                    break;

                default:
                    Debug.WriteLine($"Unhandled MID: {mid.Header.Mid}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Parse error: {ex.Message}, Message: {message}");
        }
    }

    public Task<bool> SendMidAsync(Mid mid)
    {
        try
        {
            if (_stream == null || !_isConnected) return Task.FromResult(false);

            var package = mid.Pack();
            var data = Encoding.ASCII.GetBytes(package + '\0');

            lock (_sendLock)
            {
                _stream.Write(data);
            }
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Send error: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> RequestConnection()
    {
        var mid = new Mid0001(1);
        return await SendMidAsync(mid);
    }

    public async Task<bool> StopCommunication()
    {
        var mid = new Mid0003();
        return await SendMidAsync(mid);
    }

    public async Task<bool> SubscribeToTighteningResults(int revision = 1)
    {
        var mid = new Mid0060(revision);
        return await SendMidAsync(mid);
    }

    public async Task<bool> UnsubscribeFromTighteningResults(int revision = 1)
    {
        var mid = new Mid0063(revision);
        return await SendMidAsync(mid);
    }

    public async Task<bool> RequestLastTighteningResult(int revision = 1)
    {
        var mid = new Mid0063(revision);
        return await SendMidAsync(mid);
    }

    public async Task<bool> RequestOldTighteningResult(long tighteningId, int revision = 1)
    {
        var mid = new Mid0064(revision);
        mid.TighteningId = tighteningId;
        return await SendMidAsync(mid);
    }

    public async Task<bool> SubscribeToAlarms(int revision = 1)
    {
        var mid = new Mid0070(revision);
        return await SendMidAsync(mid);
    }

    public async Task<bool> UnsubscribeFromAlarms(int revision = 1)
    {
        var mid = new Mid0073(revision);
        return await SendMidAsync(mid);
    }

    public async Task<bool> AcknowledgeAlarmRemotely()
    {
        var mid = new Mid0078();
        return await SendMidAsync(mid);
    }

    public async Task<bool> SubscribeToAutoManualMode()
    {
        var mid = new Mid0400();
        return await SendMidAsync(mid);
    }

    public async Task<bool> UnsubscribeFromAutoManualMode()
    {
        var mid = new Mid0403();
        return await SendMidAsync(mid);
    }

    public async Task<bool> RequestToolData(int toolNumber = 1)
    {
        var mid = new Mid0040(6);
        mid.ToolNumber = toolNumber;
        return await SendMidAsync(mid);
    }

    public async Task<bool> EnableTool(int toolNumber = 1)
    {
        var mid = new OpenProtocolInterpreter.Tool.Mid0043();
        mid.ToolNumber = toolNumber;
        return await SendMidAsync(mid);
    }

    public async Task<bool> DisableTool(int toolNumber = 1)
    {
        var mid = new OpenProtocolInterpreter.Tool.Mid0042();
        mid.ToolNumber = toolNumber;
        mid.DisableType = OpenProtocolInterpreter.DisableType.Disable;
        return await SendMidAsync(mid);
    }

    public async Task<bool> SendVin(string vinNumber)
    {
        var mid = new Mid0050();
        mid.VinNumber = vinNumber;
        return await SendMidAsync(mid);
    }

    public async Task<bool> UnsubscribeVin()
    {
        var mid = new Mid0053();
        return await SendMidAsync(mid);
    }

    public async Task<bool> SelectParameterSet(int psetId)
    {
        var mid = new Mid0018();
        mid.ParameterSetId = psetId;
        return await SendMidAsync(mid);
    }

    public async Task<bool> ResetBatch()
    {
        var mid = new Mid0020();
        return await SendMidAsync(mid);
    }

    public async Task<bool> SetBatchSize(int batchSize)
    {
        var mid = new Mid0019();
        mid.BatchSize = batchSize;
        return await SendMidAsync(mid);
    }

    public async Task<bool> StartTightening()
    {
        var mid = new Mid0224();
        return await SendMidAsync(mid);
    }

    public async Task<bool> RequestJobInfo(int revision = 1)
    {
        var mid = new Mid0030(revision);
        return await SendMidAsync(mid);
    }

    public async Task<bool> SelectJob(int jobId)
    {
        var mid = new Mid0031();
        return await SendMidAsync(mid);
    }

    public async Task<bool> SendTighteningAckAsync()
    {
        var mid = new Mid0062(1);
        return await SendMidAsync(mid);
    }

    public async Task<bool> SendAlarmAckAsync(int midNumber)
    {
        var mid = new Mid0072();
        return await SendMidAsync(mid);
    }

    public async Task<bool> SendAlarmStatusAckAsync(int midNumber)
    {
        var mid = new Mid0077();
        return await SendMidAsync(mid);
    }

    public async Task<bool> SendAutoManualModeAckAsync()
    {
        var mid = new Mid0402();
        return await SendMidAsync(mid);
    }

    public async Task<bool> GenericSubscribe(int subscriptionMid, int wantedRevision = 1)
    {
        var mid = new Mid0008();
        mid.SubscriptionMid = subscriptionMid.ToString().PadLeft(4, '0');
        mid.WantedRevision = wantedRevision;
        return await SendMidAsync(mid);
    }

    public async Task<bool> GenericUnsubscribe(int unsubscriptionMid, int extraDataRevision = 1)
    {
        var mid = new Mid0009();
        mid.UnsubscriptionMid = unsubscriptionMid.ToString().PadLeft(4, '0');
        mid.ExtraDataRevision = extraDataRevision;
        return await SendMidAsync(mid);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
