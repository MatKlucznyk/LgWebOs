using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.CrestronSockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WS_Client;
using XSigUtilityLibrary.Intersystem;
using Guss.Communications.Sockets;
using Guss.ModuleFramework.Logging;

namespace LgWebOs
{
    public class Display
    {
        #region Private Variables
        private readonly WebSocketClient _wsClient;
        private readonly UdpClient _udpClient;
        private readonly ILogger _logger;
        private readonly object _mainLock = new object();
        private readonly CEvent _notificationOkayToSendEvent = new CEvent(false, true);
        private InputControls _inputControls;
        private List<ExternalInput> _externalInputs;
        private List<App> _apps;
        private readonly CTimer _getDisplayInfoTimer;
        private readonly CTimer _heartbeatTimer;
        private readonly CTimer _heartbeatFailedTimer;

        private bool _isInitialized;
        private bool _isRegistered;
        private bool _isPoweredOn;
        private string _currentInput;
        private ushort _port;
        private string _id;
        private string _ipAddress;
        private string _macAddress;
        private string _clientKey;
        private string _keyFilePath;

        private string GetClientKey
        {
            get
            {
                return
                    "{\"type\":\"register\",\"id\":\"register_0\",\"payload\":{\"forcePairing\":false,\"pairingType\":\"PROMPT\",\"manifest\":{\"manifestVersion\":1,\"appVersion\":\"1.1\",\"signed\":{\"created\":\"20140509\",\"appId\":\"com.lge.test\",\"vendorId\":\"com.lge\",\"localizedAppNames\":{\"\":\"LG Remote App\"},\"localizedVendorNames\":{\"\":\"LG Electronics\"},\"permissions\":[\"TEST_SECURE\",\"CONTROL_INPUT_TEXT\",\"CONTROL_MOUSE_AND_KEYBOARD\",\"READ_INSTALLED_APPS\",\"LAUNCH_WEBAPP\",\"READ_LGE_SDX\",\"READ_NOTIFICATIONS\",\"SEARCH\",\"WRITE_SETTINGS\",\"WRITE_NOTIFICATION_ALERT\",\"CONTROL_POWER\",\"READ_CURRENT_CHANNEL\",\"READ_RUNNING_APPS\",\"READ_UPDATE_INFO\",\"UPDATE_FROM_REMOTE_APP\",\"READ_LGE_TV_INPUT_EVENTS\",\"READ_TV_CURRENT_TIME\",\"READ_INPUT_DEVICE_LIST\"],\"serial\":\"2f930e2d2cfe083771f68e4fe7bb07\"},\"permissions\":[\"LAUNCH\",\"READ_INSTALLED_APPS\",\"READ_LGE_SDX\",\"READ_LGE_TV_INPUT_EVENTS\",\"SEARCH\",\"CONTROL_MOUSE_AND_KEYBOARD\",\"LAUNCH_WEBAPP\",\"APP_TO_APP\",\"CLOSE\",\"TEST_OPEN\",\"TEST_PROTECTED\",\"CONTROL_AUDIO\",\"CONTROL_DISPLAY\",\"CONTROL_INPUT_JOYSTICK\",\"CONTROL_INPUT_MEDIA_RECORDING\",\"CONTROL_INPUT_MEDIA_PLAYBACK\",\"CONTROL_INPUT_TV\",\"CONTROL_POWER\",\"READ_APP_STATUS\",\"READ_CURRENT_CHANNEL\",\"READ_INPUT_DEVICE_LIST\",\"READ_NETWORK_STATE\",\"READ_RUNNING_APPS\",\"READ_TV_CHANNEL_LIST\",\"WRITE_NOTIFICATION_TOAST\",\"READ_POWER_STATE\",\"READ_COUNTRY_INFO\"],\"signatures\":[{\"signatureVersion\":1,\"signature\":\"eyJhbGdvcml0aG0iOiJSU0EtU0hBMjU2Iiwia2V5SWQiOiJ0ZXN0LXNpZ25pbmctY2VydCIsInNpZ25hdHVyZVZlcnNpb24iOjF9.hrVRgjCwXVvE2OOSpDZ58hR+59aFNwYDyjQgKk3auukd7pcegmE2CzPCa0bJ0ZsRAcKkCTJrWo5iDzNhMBWRyaMOv5zWSrthlf7G128qvIlpMT0YNY+n/FaOHE73uLrS/g7swl3/qH/BGFG2Hu4RlL48eb3lLKqTt2xKHdCs6Cd4RMfJPYnzgvI4BNrFUKsjkcu+WD4OO2A27Pq1n50cMchmcaXadJhGrOqH5YmHdOCj5NSHzJYrsW0HPlpuAx/ECMeIZYDh6RMqaFM2DXzdKX9NmmyqzJ3o/0lkk/N97gfVRLW5hA29yeAwaCViZNCP8iC9aO0q9fQojoa7NQnAtw==\"}]}}}";
            }
        }
        private string VerifyClientKey
        {
            get
            {
                if (_clientKey != null)
                    return "{\"type\":\"register\",\"id\":\"register_1\",\"payload\":{\"client-key\":\"" + _clientKey + "\"}}";
                else
                    return GetClientKey;
            }
        }
        #endregion

        #region Delegates
        public delegate void PowerState(ushort state);
        public delegate void VolumeValue(ushort value);
        public delegate void VolumeMuteState(ushort state);
        public delegate void CurrentInputValue(ushort valuet);
        public delegate void InputCount(ushort count);
        public delegate void ExternalInputNames(SimplSharpString xsig);
        public delegate void ExternalInputIcons(SimplSharpString xsig);
        public delegate void AppCount(ushort count);
        public delegate void AppNames(SimplSharpString xsig);
        public delegate void AppIcons(SimplSharpString xsig);

        public PowerState onPowerState { get; set; }
        public VolumeValue onVolumeValue { get; set; }
        public VolumeMuteState onVolumeMuteState { get; set; }
        public CurrentInputValue onCurrentInputValue { get; set; }
        public InputCount onInputCount { get; set; }
        public ExternalInputNames onExternalInputNames { get; set; }
        public ExternalInputIcons onExternalInputIcons { get; set; }
        public AppCount onAppCount { get; set; }
        public AppNames onAppNames { get; set; }
        public AppIcons onAppIcons { get; set; }
        #endregion

        #region Public Variables
        public ushort DebugMode { get { return Convert.ToUInt16(_logger.DebugLevel); } set { _logger.DebugLevel = (DebugLevels)value; } }
        #endregion

        public Display()
        {
            _logger = new Logger("LgWebOs");

            _getDisplayInfoTimer = new CTimer(DisplayGetInfo, Timeout.Infinite);
            _heartbeatFailedTimer = new CTimer(x => ResetConnection(), Timeout.Infinite);
            _heartbeatTimer = new CTimer(x =>
                {
                    _wsClient.SendCommand(VerifyClientKey);
                    _heartbeatFailedTimer.Reset(1000);
                }, Timeout.Infinite);
            

            _wsClient = new WebSocketClient(_logger);
            _udpClient = new UdpClient(_logger)
            {
                Port = 40000
            };

            _wsClient.ConnectedChange += new Guss.Communications.BoolEventHandler(_wsClient_ConnectedChange);
            _wsClient.ResponseReceived += new Guss.Communications.StringEventHandler(_wsClient_ResponseReceived);
        }

        #region General Methods
        public void Initialize(string id, string ipAddress, ushort port, string macAddress)
        {
            lock (_mainLock)
            {
                if (_isInitialized)
                    return;

                _id = id;
                _ipAddress = ipAddress;
                _port = port;
                _macAddress = Regex.Replace(macAddress, "[-|:]", "");

                var currentDirectory = Directory.GetApplicationDirectory().Contains("\\") ? Directory.GetApplicationDirectory().Split('\\') : Directory.GetApplicationDirectory().Split('/');

                _keyFilePath = string.Format(@"{0}User{0}{1}{0}lgWebOsDisplay_{2}", Directory.GetApplicationDirectory().Contains("\\") ? "\\" : "/", currentDirectory[2], _id);

                if (File.Exists(_keyFilePath))
                {
                    using (StreamReader reader = new StreamReader(File.OpenRead(_keyFilePath)))
                    {
                        _clientKey = reader.ReadToEnd();
                    }
                }

                _wsClient.IpAddress = "ws://" + ipAddress;
                _wsClient.Port = port;

                _udpClient.IpAddress = ipAddress;
                _udpClient.Connect();

                _isInitialized = true;

                if (onPowerState != null)
                {
                    onPowerState(0);
                }

                _wsClient.Connect();
            }
        }

        private void _wsClient_ResponseReceived(object sender, Guss.Communications.CommunicationsStringEventArgs args)
        {
            try
            {
                _logger.PrintLine("Response received -->{0}<--", args.Payload);
                var response = JObject.Parse(args.Payload);

                if (response["type"] != null)
                {
                    if (response["type"].ToObject<string>() == "registered")
                    {
                        if (response["id"] != null)
                        {
                            if (response["id"].ToObject<string>() == "register_0")
                            {
                                if (response["payload"]["client-key"] != null)
                                {
                                    _heartbeatFailedTimer.Stop();
                                    _clientKey = response["payload"]["client-key"].ToObject<string>();

                                    using (StreamWriter writer = new StreamWriter(File.Create(_keyFilePath)))
                                    {
                                        writer.Write(_clientKey);
                                    }

                                    _heartbeatTimer.Reset(0);
                                }
                            }
                            else if (response["id"].ToObject<string>() == "register_1")
                            {
                                if (!_isRegistered)
                                {
                                    _isRegistered = true;
                                    _heartbeatTimer.Reset(100, 5000);
                                    _getDisplayInfoTimer.Reset(500);
                                }
                                else
                                {
                                    _heartbeatFailedTimer.Stop();
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Invalid register response -->{0}<--", args.Payload);
                            }
                        }
                    }
                    else if (response["type"].ToObject<string>() == "response")
                    {
                        if (response["id"] != null)
                        {
                            if (response["id"].ToObject<string>() == "powerOff")
                            {
                                if (response["payload"] != null)
                                {
                                    if (response["payload"]["returnValue"].ToObject<bool>())
                                    {
                                        ResetConnection();
                                    }
                                }
                            }
                            else if (response["id"].ToObject<string>() == "getInputSocket")
                            {
                                _inputControls = new InputControls(_ipAddress, _port, response["payload"]["socketPath"].ToObject<string>(), _logger);
                                _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}");
                            }
                            else if (response["id"].ToObject<string>().Contains("changeInput_"))
                            {
                                if (response["payload"]["returnValue"].ToObject<bool>())
                                {
                                    _currentInput = response["id"].ToObject<string>().Replace("changeInput_", string.Empty);

                                    if (onCurrentInputValue != null)
                                    {
                                        var input = _externalInputs.Find(x => x.id == _currentInput);
                                        if (input != null)
                                        {
                                            onCurrentInputValue(Convert.ToUInt16(_externalInputs.IndexOf(input)));
                                        }
                                    }
                                }
                            }
                            else if (response["id"].ToObject<string>() == "getExternalInputs")
                            {
                                _externalInputs = JsonConvert.DeserializeObject<List<ExternalInput>>(response["payload"]["devices"].ToString());

                                var inputNames = new List<string>();
                                var inputIcons = new List<string>();

                                foreach (var input in _externalInputs)
                                {
                                    inputNames.Add(input.label);
                                    inputIcons.Add(input.icon.Replace("http:", string.Format("http://{0}:{1}", _ipAddress, _port)));
                                }

                                if (onInputCount != null)
                                {
                                    onInputCount(Convert.ToUInt16(_externalInputs.Count));
                                }

                                if (onExternalInputNames != null)
                                {
                                    foreach (var inputName in inputNames)
                                    {
                                        var encodedBytes = XSigHelpers.GetBytes(inputNames.IndexOf(inputName) + 1, inputName);
                                        onExternalInputNames(Encoding.GetEncoding(28591).GetString(encodedBytes, 0, encodedBytes.Length));
                                    }
                                }

                                if (onExternalInputIcons != null)
                                {
                                    foreach (var inputIcon in inputIcons)
                                    {
                                        var encodedBytes = XSigHelpers.GetBytes(inputIcons.IndexOf(inputIcon) + 1, inputIcon);
                                        onExternalInputIcons(Encoding.GetEncoding(28591).GetString(encodedBytes, 0, encodedBytes.Length));
                                    }
                                }
                            }
                            else if (response["id"].ToObject<string>() == "getAllApps")
                            {
                                _apps = JsonConvert.DeserializeObject<List<App>>(response["payload"]["launchPoints"].ToString());

                                var appNames = new List<string>();
                                var appIcons = new List<string>();

                                foreach (var input in _apps)
                                {
                                    appNames.Add(input.title);
                                    appIcons.Add(input.icon.Replace("http:", string.Format("http://{0}:{1}", _ipAddress, _port)));
                                }

                                if (onAppCount != null)
                                {
                                    onAppCount(Convert.ToUInt16(_apps.Count));
                                }

                                if (onAppNames != null)
                                {
                                    foreach (var appName in appNames)
                                    {
                                        var encodedBytes = XSigHelpers.GetBytes(appNames.IndexOf(appName) + 1, appName);
                                        onAppNames(Encoding.GetEncoding(28591).GetString(encodedBytes, 0, encodedBytes.Length));
                                    }
                                }

                                if (onAppIcons != null)
                                {
                                    foreach (var appIcon in appIcons)
                                    {
                                        var encodedBytes = XSigHelpers.GetBytes(appIcons.IndexOf(appIcon) + 1, appIcon);
                                        onAppIcons(Encoding.GetEncoding(28591).GetString(encodedBytes, 0, encodedBytes.Length));
                                    }
                                }
                            }
                            else if (response["id"].ToObject<string>() == "setVolume")
                            {
                                if (response["payload"]["returnValue"].ToObject<bool>())
                                {
                                    _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}");
                                }
                            }
                            else if (response["id"].ToObject<string>() == "volumeUp")
                            {
                                if (response["payload"]["returnValue"].ToObject<bool>())
                                {
                                    _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}");
                                }
                            }
                            else if (response["id"].ToObject<string>() == "volumeDown")
                            {
                                if (response["payload"]["returnValue"].ToObject<bool>())
                                {
                                    _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}");
                                }
                            }
                            else if (response["id"].ToObject<string>() == "getVolume")
                            {
                                if (response["payload"]["returnValue"].ToObject<bool>())
                                {
                                    var value = ScaleUp(Convert.ToInt16(response["payload"]["volume"].ToObject<string>()));

                                    if (onVolumeValue != null)
                                    {
                                        onVolumeValue(Convert.ToUInt16(value));
                                    }

                                    if (onVolumeMuteState != null)
                                    {
                                        if (response["payload"]["muted"].ToObject<bool>())
                                        {
                                            onVolumeMuteState(1);
                                        }
                                        else if (!response["payload"]["muted"].ToObject<bool>())
                                        {
                                            onVolumeMuteState(0);
                                        }
                                    }
                                }
                            }
                            else if (response["id"].ToObject<string>() == "volumeMuteOn")
                            {
                                if (response["payload"]["returnValue"].ToObject<bool>())
                                {
                                    if (onVolumeMuteState != null)
                                    {
                                        onVolumeMuteState(1);
                                    }
                                }
                            }
                            else if (response["id"].ToObject<string>() == "volumeMuteOff")
                            {
                                if (response["payload"]["returnValue"].ToObject<bool>())
                                {
                                    if (onVolumeMuteState != null)
                                    {
                                        onVolumeMuteState(0);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
            }
        }

        private void _wsClient_ConnectedChange(object sender, Guss.Communications.CommunicationsBoolEventArgs args)
        {
            lock (_mainLock)
            {
                if (args.Payload == 1)
                {
                    _isPoweredOn = true;

                    if (onPowerState != null)
                    {
                        onPowerState(1);
                    }
                    _wsClient.SendCommand(VerifyClientKey);
                    _heartbeatFailedTimer.Reset(30000);
                }
                else
                {
                    _heartbeatFailedTimer.Stop();
                    _heartbeatTimer.Stop();
                    _getDisplayInfoTimer.Stop();
                    if (_inputControls != null)
                    {
                        _inputControls.Dispose();
                        _inputControls = null;
                    }

                    _isPoweredOn = false;
                    _isRegistered = false;

                    if (onPowerState != null)
                    {
                        onPowerState(0);
                    }
                }
            }
        }

        private void ResetConnection()
        {
            lock (_mainLock)
            {
                _heartbeatFailedTimer.Stop();
                _heartbeatTimer.Stop();
                _getDisplayInfoTimer.Stop();
                if (_inputControls != null)
                {
                    _inputControls.Dispose();
                    _inputControls = null;
                }

                _wsClient.Disconnect();
                _wsClient.Connect();
            }
        }

        public void PowerOn()
        {
            lock (_mainLock)
            {
                if (_isPoweredOn)
                    return;

                var wolPacket = new byte[1024];
                var wolPacketIndex = 0;

                for (var i = 0; i < 6; i++)
                {
                    wolPacket[wolPacketIndex] = 255;
                    wolPacketIndex++;
                }

                for (var i = 0; i < 16; i++)
                {
                    for (var m = 0; m < _macAddress.Length; m += 2)
                    {
                        var mb = _macAddress.Substring(m, 2);
                        wolPacket[wolPacketIndex] = byte.Parse(mb, NumberStyles.HexNumber);
                        wolPacketIndex++;
                    }
                }

                _udpClient.SendCommand(wolPacket);
                CrestronEnvironment.Sleep(10);
                _udpClient.SendCommand(wolPacket);
            }
        }

        public void PowerOff()
        {
            lock (_mainLock)
            {
                if (!_isPoweredOn)
                    return;

                _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"powerOff\",\"uri\":\"ssap://system/turnOff\"}");
            }
        }

        public void SetVolume(ushort value)
        {
            if (!_isPoweredOn)
                return;

            var volume = ScaleDown(value);

            _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"setVolume\",\"uri\":\"ssap://audio/setVolume\",\"payload\":{\"volume\":" + volume + "}}");
        }

        public void IncrementVolume()
        {
            if (!_isPoweredOn)
                return;

            _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"volumeUp\",\"uri\":\"ssap://audio/volumeUp\"}");
        }

        public void DecrementVolume()
        {
            if (!_isPoweredOn)
                return;

            _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"volumeDown\",\"uri\":\"ssap://audio/volumeDown\"}");
        }

        public void SetMute(ushort value)
        {
            if (!_isPoweredOn)
                return;

            _wsClient.SendCommand(string.Format("{\"type\":\"request\",\"id\":\"volumeMuteOn\",\"uri\":\"ssap://audio/setMute\", \"payload\":{\"mute\": {0}}}", Convert.ToBoolean(value)));
        }

        public void SendKey(string name)
        {
            if (!_isPoweredOn)
                return;

            if (_inputControls != null)
            {
                if (_inputControls._socketClient.IsConnected)
                {
                    _inputControls.SendKey(name);
                }
                else
                {
                    _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"getInputSocket\",\"uri\":\"ssap://com.webos.service.networkinput/getPointerInputSocket\"}");
                }
            }
            else
            {
                _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"getInputSocket\",\"uri\":\"ssap://com.webos.service.networkinput/getPointerInputSocket\"}");
            }
        }

        public void ChangeInput(ushort input)
        {
            if (!_isPoweredOn)
                return;

            if (_externalInputs != null)
            {
                if (_externalInputs.Count < input)
                    return;

                _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"changeInput_" + _externalInputs[input - 1].id + "\",\"uri\":\"ssap://tv/switchInput\", \"payload\":{\"inputId\": \"" + _externalInputs[input - 1].id + "\"}}");
            }
            else
            {
                GetInputs();
            }
        }

        public void GetInputs()
        {
            if (!_isPoweredOn)
                return;

            _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"getExternalInputs\",\"uri\":\"ssap://tv/getExternalInputList\"}");
        }

        public void LaunchApp(ushort index)
        {
            if (!_isPoweredOn)
                return;

            if (_apps != null)
            {
                if (_apps.Count < index)
                    return;

                _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"launchApp\",\"uri\":\"ssap://com.webos.applicationManager/launch\", \"payload\": {\"id\": \"" + _apps[index - 1].id + "\"}}");
            }
            else
            {
                GetApps();
            }
        }

        public void GetApps()
        {
            if (!_isPoweredOn)
                return;

            _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"getAllApps\",\"uri\":\"ssap://com.webos.applicationManager/listLaunchPoints\"}");
        }

        public void SendNotification(string value)
        {
            if (!_isPoweredOn)
                return;

            _notificationOkayToSendEvent.Wait();
            _notificationOkayToSendEvent.Reset();
            _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"sendNotification\",\"uri\":\"ssap://system.notifications/createToast\",\"payload\":{\"message\":\"" + value + "\"}}");
            new CTimer(x => { _notificationOkayToSendEvent.Set(); }, 500);
        }
        #endregion

        #region Timers
        /// <summary>
        /// Waits 2.5 seconds on connection to send required handshake.
        /// </summary>
        /// <param name="o"></param>
        private void DisplayGetInfo(object o)
        {
            if (!_isPoweredOn)
                return;

            _wsClient.SendCommand("{\"type\":\"request\",\"id\":\"getInputSocket\",\"uri\":\"ssap://com.webos.service.networkinput/getPointerInputSocket\"}");
            GetApps();
            GetInputs();
        }
        #endregion

        #region Method Helpers
        private static int ScaleUp(int level)
        {
            int scaleLevel = level;
            double levelScaled = (scaleLevel * (65535.0 /100));
            double rounded = Math.Round(levelScaled);
            return Convert.ToInt32(rounded);
        }

        private static int ScaleDown(int level)
        {
            int scaleLevel = level;
            double levelScaled = (level / (65535.0 / 100.0));
            double rounded = Math.Round(levelScaled);
            return Convert.ToInt32(rounded);
        }
        #endregion
    }
}
