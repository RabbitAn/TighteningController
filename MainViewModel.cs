using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using OpenProtocolInterpreter.Alarm;
using OpenProtocolInterpreter.AutomaticManualMode;
using OpenProtocolInterpreter.Communication;
using OpenProtocolInterpreter.Tightening;
using OpenProtocolInterpreter.Tool;

namespace TighteningController;

public class TighteningResultInfo
{
    public long TighteningId { get; set; }
    public DateTime Timestamp { get; set; }
    public string VinNumber { get; set; } = "";
    public decimal Torque { get; set; }
    public string TorqueUnit { get; set; } = "";
    public int Angle { get; set; }
    public string Result { get; set; } = "";
    public int ParameterSetId { get; set; }
    public int JobId { get; set; }
    public int BatchCounter { get; set; }
    public int BatchSize { get; set; }
    public string Strategy { get; set; } = "";
    public decimal TorqueMin { get; set; }
    public decimal TorqueMax { get; set; }
    public int AngleMin { get; set; }
    public int AngleMax { get; set; }
}

public class AlarmInfo
{
    public DateTime Time { get; set; }
    public string ErrorCode { get; set; } = "";
    public string AlarmText { get; set; } = "";
    public bool ControllerReady { get; set; }
    public bool ToolReady { get; set; }
}

public class MainViewModel : INotifyPropertyChanged
{
    private readonly OpenProtocolService _service;

    private string _ipAddress = "192.168.5.212";
    private int _port = 4545;
    private bool _isConnected;
    private string _statusMessage = "未连接";
    private string _logMessages = "";
    private string _vinNumber = "";
    private int _psetId = 1;
    private int _batchSize = 1;
    private int _batchCount = 0;
    private string _lastTorque = "";
    private string _lastAngle = "";
    private string _lastResult = "";
    private string _lastTorqueLimits = "";
    private string _lastAngleLimits = "";
    private string _lastStrategy = "";
    private string _lastVin = "";
    private DateTime _lastTimestamp;
    private long _lastTighteningId;
    private bool _isAlarmActive;
    private string _alarmInfo = "无报警";
    private bool _isManualMode;
    private string _modeInfo = "未知";
    private string _toolInfo = "";
    private int _offlineResultCount;
    private bool _autoReconnect = true;
    private bool _isSubscribedTightening;
    private bool _isSubscribedAlarm;
    private bool _isSubscribedAutoManual;
    private int _selectedRevision = 1;
    private long _requestedTighteningId;

    public ObservableCollection<TighteningResultInfo> TighteningResults { get; } = new();
    public ObservableCollection<AlarmInfo> AlarmHistory { get; } = new();

    #region 属性

    public string IpAddress
    {
        get => _ipAddress;
        set { _ipAddress = value; OnPropertyChanged(); }
    }

    public int Port
    {
        get => _port;
        set { _port = value; OnPropertyChanged(); }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            _isConnected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanOperate));
        }
    }

    public bool CanConnect => !IsConnected;
    public bool CanOperate => IsConnected;

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string LogMessages
    {
        get => _logMessages;
        set { _logMessages = value; OnPropertyChanged(); }
    }

    public string VinNumber
    {
        get => _vinNumber;
        set { _vinNumber = value; OnPropertyChanged(); }
    }

    public int PsetId
    {
        get => _psetId;
        set { _psetId = value; OnPropertyChanged(); }
    }

    public int BatchSize
    {
        get => _batchSize;
        set { _batchSize = value; OnPropertyChanged(); }
    }

    public int BatchCount
    {
        get => _batchCount;
        set { _batchCount = value; OnPropertyChanged(); }
    }

    public string LastTorque
    {
        get => _lastTorque;
        set { _lastTorque = value; OnPropertyChanged(); }
    }

    public string LastAngle
    {
        get => _lastAngle;
        set { _lastAngle = value; OnPropertyChanged(); }
    }

    public string LastResult
    {
        get => _lastResult;
        set { _lastResult = value; OnPropertyChanged(); }
    }

    public string LastTorqueLimits
    {
        get => _lastTorqueLimits;
        set { _lastTorqueLimits = value; OnPropertyChanged(); }
    }

    public string LastAngleLimits
    {
        get => _lastAngleLimits;
        set { _lastAngleLimits = value; OnPropertyChanged(); }
    }

    public string LastStrategy
    {
        get => _lastStrategy;
        set { _lastStrategy = value; OnPropertyChanged(); }
    }

    public string LastVin
    {
        get => _lastVin;
        set { _lastVin = value; OnPropertyChanged(); }
    }

    public DateTime LastTimestamp
    {
        get => _lastTimestamp;
        set { _lastTimestamp = value; OnPropertyChanged(); }
    }

    public long LastTighteningId
    {
        get => _lastTighteningId;
        set { _lastTighteningId = value; OnPropertyChanged(); }
    }

    public bool IsAlarmActive
    {
        get => _isAlarmActive;
        set { _isAlarmActive = value; OnPropertyChanged(); }
    }

    public string AlarmInfo
    {
        get => _alarmInfo;
        set { _alarmInfo = value; OnPropertyChanged(); }
    }

    public bool IsManualMode
    {
        get => _isManualMode;
        set { _isManualMode = value; OnPropertyChanged(); }
    }

    public string ModeInfo
    {
        get => _modeInfo;
        set { _modeInfo = value; OnPropertyChanged(); }
    }

    public string ToolInfo
    {
        get => _toolInfo;
        set { _toolInfo = value; OnPropertyChanged(); }
    }

    public int OfflineResultCount
    {
        get => _offlineResultCount;
        set { _offlineResultCount = value; OnPropertyChanged(); }
    }

    public bool AutoReconnect
    {
        get => _autoReconnect;
        set { _autoReconnect = value; OnPropertyChanged(); }
    }

    public bool IsSubscribedTightening
    {
        get => _isSubscribedTightening;
        set { _isSubscribedTightening = value; OnPropertyChanged(); }
    }

    public bool IsSubscribedAlarm
    {
        get => _isSubscribedAlarm;
        set { _isSubscribedAlarm = value; OnPropertyChanged(); }
    }

    public bool IsSubscribedAutoManual
    {
        get => _isSubscribedAutoManual;
        set { _isSubscribedAutoManual = value; OnPropertyChanged(); }
    }

    public int SelectedRevision
    {
        get => _selectedRevision;
        set { _selectedRevision = value; OnPropertyChanged(); }
    }

    public long RequestedTighteningId
    {
        get => _requestedTighteningId;
        set { _requestedTighteningId = value; OnPropertyChanged(); }
    }

    #endregion

    #region 命令

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand SendVinCommand { get; }
    public ICommand UnsubscribeVinCommand { get; }
    public ICommand SelectPsetCommand { get; }
    public ICommand ResetBatchCommand { get; }
    public ICommand SetBatchCommand { get; }
    public ICommand StartTighteningCommand { get; }
    public ICommand EnableToolCommand { get; }
    public ICommand DisableToolCommand { get; }
    public ICommand SubscribeTighteningCommand { get; }
    public ICommand UnsubscribeTighteningCommand { get; }
    public ICommand SubscribeAlarmCommand { get; }
    public ICommand UnsubscribeAlarmCommand { get; }
    public ICommand AcknowledgeAlarmCommand { get; }
    public ICommand SubscribeAutoManualCommand { get; }
    public ICommand UnsubscribeAutoManualCommand { get; }
    public ICommand RequestToolDataCommand { get; }
    public ICommand RequestOldTighteningResultCommand { get; }
    public ICommand SelectJobCommand { get; }
    public ICommand ClearLogCommand { get; }

    #endregion

    public MainViewModel()
    {
        _service = new OpenProtocolService();

        _service.ConnectionStateChanged += OnConnectionStateChanged;
        _service.MessageReceived += OnMessageReceived;
        _service.TighteningResultReceived += OnTighteningResult;
        _service.OldTighteningResultReceived += OnOldTighteningResult;
        _service.OfflineResultCountReceived += OnOfflineResultCount;
        _service.AlarmReceived += OnAlarmReceived;
        _service.AlarmStatusReceived += OnAlarmStatusReceived;
        _service.AutoManualModeChanged += OnAutoManualModeChanged;
        _service.CommunicationStartAccepted += OnCommunicationStartAccepted;
        _service.CommandErrorReceived += OnCommandError;
        _service.CommandAcceptedReceived += OnCommandAccepted;
        _service.ToolDataReceived += OnToolDataReceived;
        _service.HeartbeatTimeout += OnHeartbeatTimeout;
        _service.Reconnecting += OnReconnecting;
        _service.ReconnectFailed += OnReconnectFailed;

        ConnectCommand = new RelayCommand(async _ => await ConnectAsync(), _ => CanConnect);
        DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsConnected);
        SendVinCommand = new RelayCommand(async _ => await SendVinAsync(), _ => CanOperate);
        UnsubscribeVinCommand = new RelayCommand(async _ => await UnsubscribeVinAsync(), _ => CanOperate);
        SelectPsetCommand = new RelayCommand(async _ => await SelectPsetAsync(), _ => CanOperate);
        ResetBatchCommand = new RelayCommand(async _ => await ResetBatchAsync(), _ => CanOperate);
        SetBatchCommand = new RelayCommand(async _ => await SetBatchAsync(), _ => CanOperate);
        StartTighteningCommand = new RelayCommand(async _ => await StartTighteningAsync(), _ => CanOperate);
        EnableToolCommand = new RelayCommand(async _ => await EnableToolAsync(), _ => CanOperate);
        DisableToolCommand = new RelayCommand(async _ => await DisableToolAsync(), _ => CanOperate);
        SubscribeTighteningCommand = new RelayCommand(async _ => await SubscribeTighteningAsync(), _ => CanOperate && !IsSubscribedTightening);
        UnsubscribeTighteningCommand = new RelayCommand(async _ => await UnsubscribeTighteningAsync(), _ => CanOperate && IsSubscribedTightening);
        SubscribeAlarmCommand = new RelayCommand(async _ => await SubscribeAlarmAsync(), _ => CanOperate && !IsSubscribedAlarm);
        UnsubscribeAlarmCommand = new RelayCommand(async _ => await UnsubscribeAlarmAsync(), _ => CanOperate && IsSubscribedAlarm);
        AcknowledgeAlarmCommand = new RelayCommand(async _ => await AcknowledgeAlarmAsync(), _ => CanOperate && IsAlarmActive);
        SubscribeAutoManualCommand = new RelayCommand(async _ => await SubscribeAutoManualAsync(), _ => CanOperate && !IsSubscribedAutoManual);
        UnsubscribeAutoManualCommand = new RelayCommand(async _ => await UnsubscribeAutoManualAsync(), _ => CanOperate && IsSubscribedAutoManual);
        RequestToolDataCommand = new RelayCommand(async _ => await RequestToolDataAsync(), _ => CanOperate);
        RequestOldTighteningResultCommand = new RelayCommand(async _ => await RequestOldTighteningResultAsync(), _ => CanOperate);
        SelectJobCommand = new RelayCommand(async _ => await SelectJobAsync(), _ => CanOperate);
        ClearLogCommand = new RelayCommand(_ => LogMessages = "");
    }

    #region 命令执行方法

    private async Task ConnectAsync()
    {
        AddLog($"正在连接 {IpAddress}:{Port}...");
        var success = await _service.ConnectAsync(IpAddress, Port, AutoReconnect);
        if (success)
        {
            AddLog("TCP连接成功，发送MID0001通信启动...");
            await _service.RequestConnection();
        }
        else
        {
            AddLog("连接失败");
        }
    }

    private void Disconnect()
    {
        _service.Disconnect();
        IsSubscribedTightening = false;
        IsSubscribedAlarm = false;
        IsSubscribedAutoManual = false;
        AddLog("已断开连接");
    }

    private async Task SendVinAsync()
    {
        AddLog($"发送VIN: {VinNumber} (MID0050)");
        await _service.SendVin(VinNumber);
    }

    private async Task UnsubscribeVinAsync()
    {
        AddLog("取消VIN订阅 (MID0053)");
        await _service.UnsubscribeVin();
    }

    private async Task SelectPsetAsync()
    {
        AddLog($"选择PSET: {PsetId} (MID0018)");
        await _service.SelectParameterSet(PsetId);
    }

    private async Task ResetBatchAsync()
    {
        AddLog("批次清零 (MID0020)");
        await _service.ResetBatch();
        BatchCount = 0;
    }

    private async Task SetBatchAsync()
    {
        AddLog($"设定批次大小: {BatchSize} (MID0019)");
        await _service.SetBatchSize(BatchSize);
    }

    private async Task StartTighteningAsync()
    {
        AddLog("开始拧紧 (MID0224)");
        await _service.StartTightening();
    }

    private async Task EnableToolAsync()
    {
        AddLog("使能工具 (MID0043)");
        await _service.EnableTool();
    }

    private async Task DisableToolAsync()
    {
        AddLog("禁用工具 (MID0042)");
        await _service.DisableTool();
    }

    private async Task SubscribeTighteningAsync()
    {
        AddLog($"订阅拧紧结果 (MID0060 Rev{SelectedRevision})");
        await _service.SubscribeToTighteningResults(SelectedRevision);
    }

    private async Task UnsubscribeTighteningAsync()
    {
        AddLog("取消订阅拧紧结果 (MID0063)");
        await _service.UnsubscribeFromTighteningResults(SelectedRevision);
    }

    private async Task SubscribeAlarmAsync()
    {
        AddLog("订阅报警 (MID0070)");
        await _service.SubscribeToAlarms();
    }

    private async Task UnsubscribeAlarmAsync()
    {
        AddLog("取消订阅报警 (MID0073)");
        await _service.UnsubscribeFromAlarms();
    }

    private async Task AcknowledgeAlarmAsync()
    {
        AddLog("远程确认报警 (MID0078)");
        await _service.AcknowledgeAlarmRemotely();
    }

    private async Task SubscribeAutoManualAsync()
    {
        AddLog("订阅自动/手动模式 (MID0400)");
        await _service.SubscribeToAutoManualMode();
    }

    private async Task UnsubscribeAutoManualAsync()
    {
        AddLog("取消订阅自动/手动模式 (MID0403)");
        await _service.UnsubscribeFromAutoManualMode();
    }

    private async Task RequestToolDataAsync()
    {
        AddLog("请求工具数据 (MID0040)");
        await _service.RequestToolData();
    }

    private async Task RequestOldTighteningResultAsync()
    {
        AddLog($"请求历史拧紧结果 ID={RequestedTighteningId} (MID0064)");
        await _service.RequestOldTighteningResult(RequestedTighteningId);
    }

    private async Task SelectJobAsync()
    {
        AddLog($"选择Job (MID0031)");
        await _service.SelectJob(1);
    }

    #endregion

    #region 事件处理

    private void OnConnectionStateChanged(bool connected)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsConnected = connected;
            StatusMessage = connected ? "已连接" : "未连接";
        });
    }

    private void OnMessageReceived(string message)
    {
        AddLog($"<RX> {message}");
    }

    private void OnCommunicationStartAccepted(Mid0002 mid)
    {
        AddLog($">> 通信启动确认 (MID0002)");
        AddLog($"   控制器: {mid.ControllerName}, CellId: {mid.CellId}, ChannelId: {mid.ChannelId}");
        if (mid.Header.Revision >= 3)
        {
            AddLog($"   OpenProtocol版本: {mid.OpenProtocolVersion}");
            AddLog($"   控制器软件版本: {mid.ControllerSoftwareVersion}");
        }
        if (mid.Header.Revision >= 7)
        {
            AddLog($"   心跳可选: {mid.OptionalKeepAlive}");
        }
    }

    private void OnCommandAccepted(Mid0005 mid)
    {
        AddLog($">> 命令接受 (MID0005) - MID: {mid.MidAccepted}");

        if (mid.MidAccepted == Mid0060.MID)
        {
            IsSubscribedTightening = true;
            AddLog("   拧紧结果订阅已激活");
        }
        else if (mid.MidAccepted == Mid0070.MID)
        {
            IsSubscribedAlarm = true;
            AddLog("   报警订阅已激活");
        }
        else if (mid.MidAccepted == Mid0400.MID)
        {
            IsSubscribedAutoManual = true;
            AddLog("   自动/手动模式订阅已激活");
        }
    }

    private void OnCommandError(Mid0004 mid)
    {
        AddLog($">> 命令错误 (MID0004) - 失败MID: {mid.FailedMid}, 错误: {mid.ErrorCode}");
    }

    private void OnTighteningResult(Mid0061 result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var torqueUnit = result.Header.Revision >= 3 ? result.TorqueValuesUnit.ToString() : "Nm";
            LastTorque = $"{result.Torque:F2} {torqueUnit}";
            LastAngle = $"{result.Angle}°";
            LastResult = result.TighteningStatus  ? "OK" : "NOK";
            LastTorqueLimits = $"{result.TorqueMinLimit:F2} ~ {result.TorqueMaxLimit:F2} {torqueUnit}";
            LastAngleLimits = $"{result.AngleMinLimit}° ~ {result.AngleMaxLimit}°";
            LastVin = result.VinNumber;
            LastTimestamp = result.Timestamp;
            LastTighteningId = result.TighteningId;
            BatchCount = result.BatchCounter;

            if (result.Header.Revision >= 2)
            {
                LastStrategy = result.Strategy.ToString();
            }

            var info = new TighteningResultInfo
            {
                TighteningId = result.TighteningId,
                Timestamp = result.Timestamp,
                VinNumber = result.VinNumber,
                Torque = result.Torque,
                TorqueUnit = torqueUnit,
                Angle = result.Angle,
                Result = LastResult,
                ParameterSetId = result.ParameterSetId,
                JobId = result.JobId,
                BatchCounter = result.BatchCounter,
                BatchSize = result.BatchSize,
                TorqueMin = result.TorqueMinLimit,
                TorqueMax = result.TorqueMaxLimit,
                AngleMin = result.AngleMinLimit,
                AngleMax = result.AngleMaxLimit,
                Strategy = result.Header.Revision >= 2 ? result.Strategy.ToString() : ""
            };

            TighteningResults.Insert(0, info);
            if (TighteningResults.Count > 100)
                TighteningResults.RemoveAt(TighteningResults.Count - 1);

            AddLog($"拧紧结果: {LastTorque}, {LastAngle}, {LastResult}, ID:{result.TighteningId}, 批次:{result.BatchCounter}/{result.BatchSize}");
        });
    }

    private void OnOldTighteningResult(Mid0065 result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var torqueUnit = result.Header.Revision >= 3 ? result.TorqueValuesUnit.ToString() : "Nm";
            AddLog($"历史拧紧结果: ID={result.TighteningId}, 扭矩={result.Torque:F2}{torqueUnit}, 角度={result.Angle}°, VIN={result.VinNumber}");
        });
    }

    private void OnOfflineResultCount(Mid0066 mid)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            OfflineResultCount = mid.NumberOfOfflineResults;
            AddLog($"离线结果数量: {mid.NumberOfOfflineResults}");
        });
    }

    private void OnAlarmReceived(Mid0071 alarm)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsAlarmActive = true;
            AlarmInfo = $"报警: {alarm.ErrorCode} - {alarm.AlarmText}";

            var info = new AlarmInfo
            {
                Time = alarm.Time,
                ErrorCode = alarm.ErrorCode,
                AlarmText = alarm.Header.Revision >= 2 ? alarm.AlarmText : "",
                ControllerReady = alarm.ControllerReadyStatus,
                ToolReady = alarm.ToolReadyStatus
            };

            AlarmHistory.Insert(0, info);
            if (AlarmHistory.Count > 50)
                AlarmHistory.RemoveAt(AlarmHistory.Count - 1);

            AddLog($"!! 报警: {alarm.ErrorCode}, 控制器就绪:{alarm.ControllerReadyStatus}, 工具就绪:{alarm.ToolReadyStatus}");
        });
    }

    private void OnAlarmStatusReceived(Mid0076 status)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsAlarmActive = status.AlarmStatus;
            AlarmInfo = status.AlarmStatus ? $"报警活跃: {status.ErrorCode}" : "无报警";
            AddLog($"报警状态: {(status.AlarmStatus ? "活跃" : "无")}, 错误码:{status.ErrorCode}");
        });
    }

    private void OnAutoManualModeChanged(Mid0401 mode)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsManualMode = mode.ManualAutomaticMode;
            ModeInfo = mode.ManualAutomaticMode ? "手动模式" : "自动模式";
            AddLog($"模式变更: {ModeInfo}");
        });
    }

    private void OnToolDataReceived(Mid0041 toolData)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ToolInfo = $"序列号:{toolData.ToolSerialNumber}, 紧固次数:{toolData.ToolNumberOfTightenings}, 校准日期:{toolData.LastCalibrationDate:yyyy-MM-dd}";
            AddLog($">> 工具数据: 序列号={toolData.ToolSerialNumber}, 紧固次数={toolData.ToolNumberOfTightenings}");
            if (toolData.Header.Revision >= 2)
            {
                AddLog($"   控制器序列号={toolData.ControllerSerialNumber}, 工具类型={toolData.ToolType}");
            }
        });
    }

    private void OnHeartbeatTimeout()
    {
        AddLog("!! 心跳超时，连接已断开！");
    }

    private void OnReconnecting()
    {
        AddLog("正在尝试重连...");
    }

    private void OnReconnectFailed()
    {
        AddLog("!! 重连失败，已达到最大重试次数");
    }

    private void AddLog(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            LogMessages = $"[{timestamp}] {message}\n{LogMessages}";
            if (LogMessages.Length > 50000)
            {
                var lines = LogMessages.Split('\n');
                LogMessages = string.Join('\n', lines.Take(500));
            }
        });
    }

    #endregion

    public void Cleanup()
    {
        _service.Dispose();
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
