using Avg.Communications;
using Avg.Communications.Sockets;
using Avg.ModuleFramework;
using Avg.ModuleFramework.Logging;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using LgWebOs.Events;
using LgWebOs.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LgWebOs
{
    public class Display : IDisposable
    {
        #region Constants
        private const long HEARTBEAT_INTERVAL = 10000;
        private const long HEARTBEAT_TIMEOUT = 2500;
        private const long HEARTBEAT_RESET_DELAY = 5000;
        private const long HEARTBEAT_REGISTRATION_INTERVAL = 60000;
        private const long RESET_CONNECTION_TIMEOUT = 2500;
        private const int COMMAND_QUEUE_INTERVAL = 250;
        private const int WOL_SEND_ATTEMPTS = 2;
        private const int WOL_SEND_DELAY = 10;
        private const double VOLUME_SCALE_MAX = 65535.0;
        private const double VOLUME_SCALE_PERCENT = 100.0;

        private const string RESPONSE_TYPE_REGISTERED = "registered";
        private const string RESPONSE_TYPE_RESPONSE = "response";
        private const string REGISTER_ID_0 = "register_0";
        private const string REGISTER_ID_1 = "register_1";

        private const string CMD_GET_VOLUME = "{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}";
        private const string CMD_GET_INPUT_SOCKET = "{\"type\":\"request\",\"id\":\"getInputSocket\",\"uri\":\"ssap://com.webos.service.networkinput/getPointerInputSocket\"}";
        private const string CMD_GET_EXTERNAL_INPUTS = "{\"type\":\"request\",\"id\":\"getExternalInputs\",\"uri\":\"ssap://tv/getExternalInputList\"}";
        private const string CMD_GET_ALL_APPS = "{\"type\":\"request\",\"id\":\"getAllApps\",\"uri\":\"ssap://com.webos.applicationManager/listLaunchPoints\"}";
        private const string CMD_POWER_OFF = "{\"type\":\"request\",\"id\":\"powerOff\",\"uri\":\"ssap://system/turnOff\"}";
        private const string CMD_VOLUME_UP = "{\"type\":\"request\",\"id\":\"volumeUp\",\"uri\":\"ssap://audio/volumeUp\"}";
        private const string CMD_VOLUME_DOWN = "{\"type\":\"request\",\"id\":\"volumeDown\",\"uri\":\"ssap://audio/volumeDown\"}";

        private const string KEY_FILE_NAME_FORMAT = "lgWebOsDisplay_{0}";
        private const string WS_PROTOCOL_PREFIX = "ws://";
        private const string CHANGE_INPUT_ID_PREFIX = "changeInput_";
        #endregion

        #region Private Variables
        private readonly WebSocketClient _wsClient;
        private readonly ILogger _logger;
        private readonly object _mainLock = new object();
        private InputControls _inputControls;
        private List<ExternalInput> _externalInputs = new List<ExternalInput>();
        private List<App> _apps = new List<App>();
        private readonly CTimer _cmdQueueDequeuer;
        private readonly CTimer _heartbeatTimer;
        private readonly CTimer _heartbeatFailedTimer;
        private readonly CommandQueue<Command> _cmdQueue = new CommandQueue<Command>();

        private ushort _port;
        private string _id;
        private string _ipAddress;
        private string _macAddress;
        private string _clientKey;
        private string _keyFilePath;
        private string _currentInput;
        private bool _isPoweredOn;
        private ushort _currentVolume;
        private bool _isMuted;
        #endregion

        #region Events
        public event UShortEventHandler PowerStateChanged;
        public event UShortEventHandler VolumeValueChanged;
        public event UShortEventHandler VolumeMuteStateChanged;
        public event UShortEventHandler CurrentInputValueChanged;
        public event StringArrayEventHandler ExternalInputNamesChanged;
        public event StringArrayEventHandler ExternalInputIconsChanged;
        public event StringArrayEventHandler AppNamesChanged;
        public event StringArrayEventHandler AppIconsChanged;
        #endregion

        #region Public Variables

        public bool Disposed { get; private set; }

        public ushort DebugMode { get { return Convert.ToUInt16(_logger.DebugLevel); } set { _logger.DebugLevel = (DebugLevels)value; } }

        public bool IsInitialized { get; private set; }

        public bool IsRegistered { get; private set; }

        public bool IsPoweredOn { get { lock (_mainLock) { return _isPoweredOn; } } }

        public ushort CurrentVolume {  get { lock (_mainLock) { return _currentVolume; } } }

        public bool IsMuted { get { lock (_mainLock) { return _isMuted; } } }

        public string CurrentInput { get { lock (_mainLock) { return _currentInput; } } }

        public List<App> Apps { get { lock (_mainLock) { return _apps.ToList(); } } }

        public List<ExternalInput> ExternalInputs { get { lock (_mainLock) { return _externalInputs.ToList(); } } }

        #endregion

        public Display()
        {
            _logger = new Logger("LgWebOs");

            _cmdQueueDequeuer = new CTimer(x =>
            {
                if (_cmdQueue.IsEmpty) return;

                var cmd = _cmdQueue.Dequeue();

                _wsClient.SendCommand(cmd.CommandString);
            }, Timeout.Infinite);

            _heartbeatFailedTimer = new CTimer(x =>
            {
                _logger.LogWarning("Hearbeat timed out, resetting connection.");
                ResetConnection();
            }, Timeout.Infinite);

            _heartbeatTimer = new CTimer(x =>
            {
                SendCommand(new Command(CommandPriorities.High, KeyUtils.GetVerifyClientKey(_clientKey)));
                lock (_mainLock)
                {
                    _heartbeatFailedTimer.Reset(HEARTBEAT_TIMEOUT);
                }
            }, Timeout.Infinite);

            _wsClient = new WebSocketClient(_logger);

            _wsClient.ConnectedChange += _wsClient_ConnectedChange;
            _wsClient.ResponseReceived += _wsClient_ResponseReceived;
        }

        #region General Methods
        public void Initialize(string id, string ipAddress, ushort port, string macAddress)
        {
            lock (_mainLock)
            {
                if (IsInitialized)
                    return;

                _id = id;
                _ipAddress = ipAddress;
                _port = port;
                _macAddress = Regex.Replace(macAddress, "[-|:]", "");

                var currentDirectory = Directory.GetApplicationRootDirectory();

                _logger.LogNotice("Current Directory: {0}", currentDirectory);

                switch (CrestronEnvironment.DevicePlatform)
                {
                    case eDevicePlatform.Appliance:
                    {
                        var currentAppDirectoryArr = Directory.GetApplicationDirectory().Contains("\\")
                            ? Directory.GetApplicationDirectory().Split('\\')
                            : Directory.GetApplicationDirectory().Split('/');

                        _keyFilePath = string.Format(@"{0}User{0}{1}{0}{2}",
                            currentDirectory.Contains("\\") ? "\\" : "/", currentAppDirectoryArr[2], 
                            string.Format(KEY_FILE_NAME_FORMAT, _id));
                    }
                        break;
                    case eDevicePlatform.Server:
                        _keyFilePath = string.Format(@"{0}/User/{1}", currentDirectory, 
                            string.Format(KEY_FILE_NAME_FORMAT, _id));
                        break;
                    default:
                        return;
                }

                _logger.LogNotice("Key File Path: {0}", _keyFilePath);

                if (File.Exists(_keyFilePath))
                {
                    using (var reader = new StreamReader(File.OpenRead(_keyFilePath)))
                    {
                        _clientKey = reader.ReadToEnd();
                    }
                }

                _wsClient.IpAddress = WS_PROTOCOL_PREFIX + ipAddress;
                _wsClient.Port = port;

                _cmdQueueDequeuer.Reset(0, COMMAND_QUEUE_INTERVAL);

                IsInitialized = true;

                PowerStateChanged?.Invoke(this, new UShortEventArgs(0));

                _wsClient.Connect();
            }
        }

        private void ResetHeartbeat(long dueTime)
        {
            lock (_mainLock)
            {
                _logger.PrintLine("Restarting heartbeat timer...");
                _heartbeatFailedTimer.Stop();
                _heartbeatTimer.Reset(dueTime);
            }
        }

        private void _wsClient_ResponseReceived(object sender, CommunicationsStringEventArgs args)
        {
            try
            {
                ResetHeartbeat(HEARTBEAT_RESET_DELAY);
                _logger.PrintLine("Response received -->{0}<--", args.Payload);

                var response = JObject.Parse(args.Payload);

                if (response == null) return;

                var responseType = response["type"]?.ToObject<string>();
                var responseId = response["id"]?.ToObject<string>();

                if (responseType == RESPONSE_TYPE_REGISTERED)
                {
                    HandleRegistrationResponse(response);
                }
                else if (responseType == RESPONSE_TYPE_RESPONSE && responseId != null)
                {
                    HandleCommandResponse(responseId, response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
            }
        }

        private void HandleRegistrationResponse(JObject response)
        {
            var registerId = response["id"]?.ToObject<string>();
            switch (registerId)
            {
                case REGISTER_ID_0:
                    if (response["payload"]?["client-key"] != null)
                    {
                        lock (_mainLock)
                        {
                            _clientKey = response["payload"]["client-key"].ToObject<string>();

                            using (var writer = new StreamWriter(File.Create(_keyFilePath)))
                            {
                                writer.Write(_clientKey);
                            }
                        }

                        ResetHeartbeat(0);
                    }
                    break;
                case REGISTER_ID_1:
                    if (!IsRegistered)
                    {
                        IsRegistered = true;
                        DisplayGetInfo();
                        ResetHeartbeat(HEARTBEAT_REGISTRATION_INTERVAL);
                    }
                    break;
                default:
                    _logger.LogWarning("Invalid register response -->{0}<--", response.ToString());
                    break;
            }
        }

        private void HandleCommandResponse(string responseId, JObject response)
        {
            switch (responseId)
            {
                case "powerOff":
                    if (response["payload"] == null) return;
                    if (response["payload"]["returnValue"].ToObject<bool>())
                    {
                        ResetConnection();
                    }
                    break;
                case "getInputSocket":
                    HandleGetInputSocketResponse(response);
                    break;
                case "getExternalInputs":
                    HandleGetExternalInputsResponse(response);
                    break;
                case "getAllApps":
                    HandleGetAllAppsResponse(response);
                    break;
                case "setVolume":
                case "volumeUp":
                case "volumeDown":
                    if (response["payload"]["returnValue"].ToObject<bool>())
                    {
                        SendCommand(new Command(CommandPriorities.Low, CMD_GET_VOLUME));
                    }
                    break;
                case "getVolume":
                    HandleGetVolumeResponse(response);
                    break;
                case "volumeMuteOn":
                    if (response["payload"]["returnValue"].ToObject<bool>())
                    {
                        lock (_mainLock)
                        {
                            _isMuted = true;
                        }
                        VolumeMuteStateChanged?.Invoke(this, new UShortEventArgs(1));
                    }
                    break;
                case "volumeMuteOff":
                    if (response["payload"]["returnValue"].ToObject<bool>())
                    {
                        lock (_mainLock)
                        {
                            _isMuted = false;
                        }
                        VolumeMuteStateChanged?.Invoke(this, new UShortEventArgs(0));
                    }
                    break;
                default:
                    if (responseId.Contains(CHANGE_INPUT_ID_PREFIX))
                    {
                        HandleChangeInputResponse(responseId, response);
                    }
                    break;
            }
        }

        private void HandleGetInputSocketResponse(JObject response)
        {
            var socketPath = response["payload"]?["socketPath"]?.ToObject<string>();

            if (socketPath == null) return;
            lock (_mainLock)
            {
                _inputControls = new InputControls(_ipAddress, _port, socketPath, _logger);
            }
            SendCommand(new Command(CommandPriorities.Low, CMD_GET_VOLUME));
        }

        private void HandleGetExternalInputsResponse(JObject response)
        {
            var externalInputs = JsonConvert.DeserializeObject<List<ExternalInput>>(
                response["payload"]["devices"].ToString());

            if (externalInputs == null || externalInputs.Count == 0) return;

            var inputNames = new List<string>();
            var inputIcons = new List<string>();
            lock (_mainLock)
            {
                _externalInputs = externalInputs;
            }

            foreach (var input in externalInputs)
            {
                inputNames.Add(input.Label);
                inputIcons.Add(input.Icon.Replace("http:",
                    string.Format("http://{0}:{1}", _ipAddress, _port)));
            }

            ExternalInputNamesChanged?.Invoke(this, new StringArrayEventArgs(inputNames.ToArray()));
            ExternalInputIconsChanged?.Invoke(this, new StringArrayEventArgs(inputIcons.ToArray()));
        }

        private void HandleGetAllAppsResponse(JObject response)
        {
            var apps = JsonConvert.DeserializeObject<List<App>>(
                response["payload"]["launchPoints"].ToString());

            if (apps == null || apps.Count == 0) return;

            lock (_mainLock)
            {
                _apps = apps;
            }

            var appNames = new List<string>();
            var appIcons = new List<string>();

            foreach (var app in apps)
            {
                appNames.Add(app.Title);
                appIcons.Add(app.Icon.Replace("http:",
                    string.Format("http://{0}:{1}", _ipAddress, _port)));
            }

            AppNamesChanged?.Invoke(this, new StringArrayEventArgs(appNames.ToArray()));
            AppIconsChanged?.Invoke(this, new StringArrayEventArgs(appIcons.ToArray()));
        }

        private void HandleGetVolumeResponse(JObject response)
        {
            if (!response["payload"]["returnValue"].ToObject<bool>()) return;
            var value = ScaleUp(Convert.ToInt16(response["payload"]["volume"].ToObject<string>()));

            lock (_mainLock)
            {
                _currentVolume = Convert.ToUInt16(value);
                _isMuted = response["payload"]["muted"].ToObject<bool>();
            }

            VolumeValueChanged?.Invoke(this, new UShortEventArgs(Convert.ToUInt16(value)));

            var isMuted = response["payload"]["muted"].ToObject<bool>();
            VolumeMuteStateChanged?.Invoke(this, new UShortEventArgs(isMuted ? (ushort)1 : (ushort)0));
        }

        private void HandleChangeInputResponse(string responseId, JObject response)
        {
            if (!response["payload"]["returnValue"].ToObject<bool>()) return;

            ExternalInput input;
            ushort index;
            lock (_mainLock)
            {
                _currentInput = response["id"].ToObject<string>()
                    .Replace(CHANGE_INPUT_ID_PREFIX, string.Empty);

                input = _externalInputs?.Find(x => x.Id == _currentInput);
                index = input != null ? Convert.ToUInt16(_externalInputs.IndexOf(input)) : (ushort)0;
            }

            if (input == null) return;

            CurrentInputValueChanged?.Invoke(this, new UShortEventArgs(index));
        }

        private void _wsClient_ConnectedChange(object sender, CommunicationsBoolEventArgs args)
        {
            _logger.PrintLine("Connection event received {0}", args.Payload);
            _logger.LogNotice("Connection event received {0}", args.Payload);
            lock (_mainLock)
            {
                try
                {
                    if (args.Payload == 1)
                    {
                        _logger.PrintLine("Received connected event!");
                        _logger.LogNotice("Received connected event!");

                        if (!_isPoweredOn)
                        {
                            _isPoweredOn = true;

                            PowerStateChanged?.Invoke(this, new UShortEventArgs(1));

                            _heartbeatTimer.Reset(0, HEARTBEAT_INTERVAL);
                        }
                        
                        _logger.PrintLine("Processed connected event!");
                        _logger.LogNotice("Processed connected event!");
                    }
                    else
                    {
                        _logger.PrintLine("Received disconnected event!");
                        _logger.LogNotice("Received disconnected event!");


                        if (_isPoweredOn)
                        {
                            _isPoweredOn = false;
                            IsRegistered = false;

                            _heartbeatTimer.Stop();
                            _heartbeatFailedTimer.Stop();

                            _cmdQueue.Clear();

                            if (_inputControls != null)
                            {
                                _inputControls.Dispose();
                            }

                            PowerStateChanged?.Invoke(this, new UShortEventArgs(0));
                        }

                        _logger.PrintLine("Processed disconnected event!");
                        _logger.LogNotice("Processed disconnected event!");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex);
                }
            }
        }

        public void ResetConnection()
        {
            CrestronInvoke.BeginInvoke(x =>
            {
                lock (_mainLock)
                {
                    _heartbeatTimer.Stop();
                    _heartbeatFailedTimer.Stop();

                    _wsClient.Disconnect();

                    using(var ev = new CEvent(false, false))
                    // ReSharper disable once AccessToDisposedClosure
                    using (new CTimer(_ => ev.Set(), (int)RESET_CONNECTION_TIMEOUT))
                    {
                        ev.Wait();
                    }

                    _wsClient.Connect();
                }
            });
        }

        public void SendCommand(Command commandToSend)
        {
            _cmdQueue.Enqueue(commandToSend);
        }

        public void PowerOn()
        {
            _logger.PrintLine("Trying to send power on...");
            lock (_mainLock)
            {
                if (IsPoweredOn)
                {
                    _logger.PrintLine("Already powered on");
                    return;
                }

                for (int i = 0; i < WOL_SEND_ATTEMPTS; i++)
                {
                    WakeOnLanUtility.SendWol(_ipAddress, _macAddress, 1);
                    if (i < WOL_SEND_ATTEMPTS - 1)
                    {
                        CrestronEnvironment.Sleep(WOL_SEND_DELAY);
                    }
                }
            }
            _logger.PrintLine("Sent power on");
        }

        public void PowerOff()
        {
            _logger.PrintLine("Trying to send power off...");
            lock (_mainLock)
            {
                if (!IsPoweredOn)
                {
                    _logger.PrintLine("Already powered off");
                    return;
                }

                SendCommand(new Command(CommandPriorities.Highest, CMD_POWER_OFF));
                ResetHeartbeat(HEARTBEAT_RESET_DELAY);
            }

            _logger.PrintLine("Sent power off");
        }

        public void SetVolume(ushort value)
        {
            if (!IsPoweredOn)
                return;

            var volume = ScaleDown(value);

            SendCommand(new Command(CommandPriorities.Medium, 
                "{\"type\":\"request\",\"id\":\"setVolume\",\"uri\":\"ssap://audio/setVolume\",\"payload\":{\"volume\":" + volume + "}}"));
        }

        public void IncrementVolume()
        {
            if (!IsPoweredOn)
                return;

            SendCommand(new Command(CommandPriorities.Medium, CMD_VOLUME_UP));
        }

        public void DecrementVolume()
        {
            if (!IsPoweredOn)
                return;

            SendCommand(new Command(CommandPriorities.Medium, CMD_VOLUME_DOWN));
        }

        public void SetMute(ushort value)
        {
            if (!IsPoweredOn)
                return;

            SendCommand(new Command(CommandPriorities.Medium, 
                string.Format("{\"type\":\"request\",\"id\":\"volumeMuteOn\",\"uri\":\"ssap://audio/setMute\", \"payload\":{\"mute\": {0}}}", 
                Convert.ToBoolean(value))));
        }

        public void SendKey(string name)
        {
            if (!IsPoweredOn)
                return;

            if (_inputControls != null && _inputControls.IsConnected)
            {
                _inputControls.SendKey(name);
            }
            else
            {
                SendCommand(new Command(CommandPriorities.Medium, CMD_GET_INPUT_SOCKET));
            }
        }

        public void ChangeInput(ushort input)
        {
            if (!IsPoweredOn)
                return;

            if (_externalInputs != null && _externalInputs.Count >= input)
            {
                var selectedInput = _externalInputs[input - 1];
                SendCommand(new Command(CommandPriorities.Medium, 
                    "{\"type\":\"request\",\"id\":\"changeInput_" + selectedInput.Id + 
                    "\",\"uri\":\"ssap://tv/switchInput\", \"payload\":{\"inputId\": \"" + selectedInput.Id + "\"}}"));
            }
            else
            {
                GetInputs();
            }
        }

        public void GetInputs()
        {
            if (!IsPoweredOn)
                return;

            SendCommand(new Command(CommandPriorities.Medium, CMD_GET_EXTERNAL_INPUTS));
        }

        public void LaunchApp(ushort index)
        {
            if (!IsPoweredOn)
                return;

            if (_apps != null && _apps.Count >= index)
            {
                var selectedApp = _apps[index - 1];
                SendCommand(new Command(CommandPriorities.Medium, 
                    "{\"type\":\"request\",\"id\":\"launchApp\",\"uri\":\"ssap://com.webos.applicationManager/launch\", \"payload\": {\"id\": \"" + selectedApp.Id + "\"}}"));
            }
            else
            {
                GetApps();
            }
        }

        public void GetApps()
        {
            if (!IsPoweredOn)
                return;

            SendCommand(new Command(CommandPriorities.Medium, CMD_GET_ALL_APPS));
        }

        public void SendNotification(string value)
        {
            if (!IsPoweredOn)
                return;

            SendCommand(new Command(CommandPriorities.Low, 
                "{\"type\":\"request\",\"id\":\"sendNotification\",\"uri\":\"ssap://system.notifications/createToast\",\"payload\":{\"message\":\"" + value + "\"}}"));
        }
        #endregion

        #region Timers
        private void DisplayGetInfo()
        {
            if (!IsPoweredOn)
                return;

            SendCommand(new Command(CommandPriorities.Highest, CMD_GET_INPUT_SOCKET));
            GetApps();
            GetInputs();
        }
        #endregion

        #region Method Helpers
        private static int ScaleUp(int level)
        {
            var levelScaled = level * (VOLUME_SCALE_MAX / VOLUME_SCALE_PERCENT);
            var rounded = Math.Round(levelScaled);
            return Convert.ToInt32(rounded);
        }

        private static int ScaleDown(int level)
        {
            var levelScaled = level / (VOLUME_SCALE_MAX / VOLUME_SCALE_PERCENT);
            var rounded = Math.Round(levelScaled);
            return Convert.ToInt32(rounded);
        }
        #endregion

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (Disposed) return;

            Disposed = true;

            if (disposing)
            {
                _heartbeatTimer.Stop();
                _heartbeatFailedTimer.Stop();
                _heartbeatTimer.Dispose();
                _heartbeatFailedTimer.Dispose();

                _cmdQueueDequeuer.Stop();
                _cmdQueueDequeuer.Dispose();
                _cmdQueue.Clear();
                
                if(_inputControls != null) _inputControls.Dispose();
                _wsClient.Dispose();
            }
        }
    }
}
