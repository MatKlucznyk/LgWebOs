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

namespace LgWebOs
{
    public class Display
    {
        #region Private Variables
        private WsClient _mainClient;
        private UDPServer _udpServer;
        private InputControls _inputControls;
        private List<ExternalInput> _externalInputs;
        private List<App> _apps;

        private bool _isPoweredOn;
        private string _currentInput;
        private bool _debugMode = false;
        private bool _isBusy;
        private ushort _port;
        private string _id;
        private string _ipAddress;
        private string _macAddress;
        private string _clientKey;
        private string _keyFilePath;

        private string _getClientKey()
        {
            return
                "{\"type\":\"register\",\"id\":\"register_0\",\"payload\":{\"forcePairing\":false,\"pairingType\":\"PROMPT\",\"manifest\":{\"manifestVersion\":1,\"appVersion\":\"1.1\",\"signed\":{\"created\":\"20140509\",\"appId\":\"com.lge.test\",\"vendorId\":\"com.lge\",\"localizedAppNames\":{\"\":\"LG Remote App\"},\"localizedVendorNames\":{\"\":\"LG Electronics\"},\"permissions\":[\"TEST_SECURE\",\"CONTROL_INPUT_TEXT\",\"CONTROL_MOUSE_AND_KEYBOARD\",\"READ_INSTALLED_APPS\",\"LAUNCH_WEBAPP\",\"READ_LGE_SDX\",\"READ_NOTIFICATIONS\",\"SEARCH\",\"WRITE_SETTINGS\",\"WRITE_NOTIFICATION_ALERT\",\"CONTROL_POWER\",\"READ_CURRENT_CHANNEL\",\"READ_RUNNING_APPS\",\"READ_UPDATE_INFO\",\"UPDATE_FROM_REMOTE_APP\",\"READ_LGE_TV_INPUT_EVENTS\",\"READ_TV_CURRENT_TIME\",\"READ_INPUT_DEVICE_LIST\"],\"serial\":\"2f930e2d2cfe083771f68e4fe7bb07\"},\"permissions\":[\"LAUNCH\",\"READ_INSTALLED_APPS\",\"READ_LGE_SDX\",\"READ_LGE_TV_INPUT_EVENTS\",\"SEARCH\",\"CONTROL_MOUSE_AND_KEYBOARD\",\"LAUNCH_WEBAPP\",\"APP_TO_APP\",\"CLOSE\",\"TEST_OPEN\",\"TEST_PROTECTED\",\"CONTROL_AUDIO\",\"CONTROL_DISPLAY\",\"CONTROL_INPUT_JOYSTICK\",\"CONTROL_INPUT_MEDIA_RECORDING\",\"CONTROL_INPUT_MEDIA_PLAYBACK\",\"CONTROL_INPUT_TV\",\"CONTROL_POWER\",\"READ_APP_STATUS\",\"READ_CURRENT_CHANNEL\",\"READ_INPUT_DEVICE_LIST\",\"READ_NETWORK_STATE\",\"READ_RUNNING_APPS\",\"READ_TV_CHANNEL_LIST\",\"WRITE_NOTIFICATION_TOAST\",\"READ_POWER_STATE\",\"READ_COUNTRY_INFO\"],\"signatures\":[{\"signatureVersion\":1,\"signature\":\"eyJhbGdvcml0aG0iOiJSU0EtU0hBMjU2Iiwia2V5SWQiOiJ0ZXN0LXNpZ25pbmctY2VydCIsInNpZ25hdHVyZVZlcnNpb24iOjF9.hrVRgjCwXVvE2OOSpDZ58hR+59aFNwYDyjQgKk3auukd7pcegmE2CzPCa0bJ0ZsRAcKkCTJrWo5iDzNhMBWRyaMOv5zWSrthlf7G128qvIlpMT0YNY+n/FaOHE73uLrS/g7swl3/qH/BGFG2Hu4RlL48eb3lLKqTt2xKHdCs6Cd4RMfJPYnzgvI4BNrFUKsjkcu+WD4OO2A27Pq1n50cMchmcaXadJhGrOqH5YmHdOCj5NSHzJYrsW0HPlpuAx/ECMeIZYDh6RMqaFM2DXzdKX9NmmyqzJ3o/0lkk/N97gfVRLW5hA29yeAwaCViZNCP8iC9aO0q9fQojoa7NQnAtw==\"}]}}}";

        }
        private string _verifyClientKey()
        {
            if (_clientKey != null)
                return "{\"type\":\"register\",\"id\":\"register_1\",\"payload\":{\"client-key\":\"" + _clientKey + "\"}}";
            else
                return string.Empty;
        }
        #endregion

        #region Delegates
        public delegate void Busy(ushort state);
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

        public Busy onBusy { get; set; }
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
        public ushort DebugMode { get { return Convert.ToUInt16(_debugMode); } set { _debugMode = Convert.ToBoolean(value); } }
        #endregion

        #region General Methods
        public void Initialize(string id, string ipAddress, ushort port, string macAddress)
        {
            _isBusy = true;
            if (onBusy != null)
                onBusy(1);

            _id = id;
            _ipAddress = ipAddress;
            _port = port;
            _macAddress = Regex.Replace(macAddress, "[-|:]", ""); 

            var currentDirectory = Directory.GetApplicationDirectory().Split('\\');

            _keyFilePath = string.Format(@"\User\{0}\lgWebOsDisplay_{1}", currentDirectory[2], _id);

            if (File.Exists(_keyFilePath))
            {
                using (StreamReader reader = new StreamReader(File.OpenRead(_keyFilePath)))
                {
                    _clientKey = reader.ReadToEnd().Replace("\r\n", string.Empty);
                }
            }

            _mainClient = new WsClient();

            _mainClient.ID = "LgWebOs - " + _id;
            _mainClient.AutoReconnect = 1;

            if (_debugMode)
            {
                _mainClient.DebugMode = 1;
            }

            _mainClient.ConnectionStatusChange += new EventHandler<ConnectionEventArgs>(_mainClient_ConnectionStatusChange);
            _mainClient.ReceiveDataChange += new EventHandler<ReceiveDataEventArgs>(_mainClient_ReceiveDataChange);

            _udpServer = new UDPServer(_ipAddress, 40000, 1000);
            _udpServer.EthernetAdapterToBindTo = EthernetAdapterType.EthernetLANAdapter;
            _udpServer.EnableUDPServer();

            _isBusy = false;
            if (onBusy != null)
                onBusy(0);

            Connect();
        }

        void _mainClient_ReceiveDataChange(object sender, ReceiveDataEventArgs e)
        {
            try
            {
                JObject response = JObject.Parse(e.Data);

                if (response["type"] != null)
                {
                    if (CleanJson(response["type"].ToString()) == "registered")
                    {
                        if (response["id"] != null)
                        {
                            if (CleanJson(response["id"].ToString()) == "register_0")
                            {
                                if (response["payload"]["client-key"] != null)
                                {
                                    _clientKey = CleanJson(response["payload"]["client-key"].ToString());

                                    using (StreamWriter writer = new StreamWriter(File.Create(_keyFilePath)))
                                    {
                                        writer.WriteLine(_clientKey);
                                    }

                                    _mainClient.SendData(_verifyClientKey());
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "register_1")
                            {
                                CTimer waitToGetInfo = new CTimer(DisplayGetInfo, 2500);
                            }
                        }
                    }
                    else if (CleanJson(response["type"].ToString()) == "response")
                    {
                        if (response["id"] != null)
                        {
                            if (CleanJson(response["id"].ToString()) == "powerOff" && response["payload"] != null)
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    _mainClient.Disconnect();
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "getInputSocket")
                            {
                                _inputControls = new InputControls(_ipAddress, _port, CleanJson(response["payload"]["socketPath"].ToString()), _id);
                                _mainClient.SendData("{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}");
                            }
                            else if (CleanJson(response["id"].ToString()).Contains("changeInput_"))
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    _currentInput = CleanJson(response["id"].ToString()).Replace("changeInput_", string.Empty);

                                    if (onCurrentInputValue != null)
                                    {
                                        ExternalInput input = _externalInputs.Find(x => x.id == _currentInput);
                                        onCurrentInputValue(Convert.ToUInt16(_externalInputs.IndexOf(input)));
                                    }
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "getExternalInputs")
                            {
                                _externalInputs = JsonConvert.DeserializeObject<List<ExternalInput>>(response["payload"]["devices"].ToString());

                                List<string> inputNames = new List<string>();
                                List<string> inputIcons = new List<string>();

                                foreach (var input in _externalInputs)
                                {
                                    inputNames.Add(input.label);
                                    inputIcons.Add(input.icon.Replace("http:", string.Format("http://{0}:{1}", _ipAddress, _port)));
                                }

                                if (onInputCount != null)
                                    onInputCount(Convert.ToUInt16(_externalInputs.Count));

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
                            else if (CleanJson(response["id"].ToString()) == "getAllApps")
                            {
                                _apps = JsonConvert.DeserializeObject<List<App>>(response["payload"]["launchPoints"].ToString());

                                List<string> appNames = new List<string>();
                                List<string> appIcons = new List<string>();

                                foreach (var input in _apps)
                                {
                                    appNames.Add(input.title);
                                    appIcons.Add(input.icon.Replace("http:", string.Format("http://{0}:{1}", _ipAddress, _port)));
                                }

                                if (onAppCount != null)
                                    onAppCount(Convert.ToUInt16(_apps.Count));

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
                            else if (CleanJson(response["id"].ToString()) == "setVolume")
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    _mainClient.SendData("{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}");
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "volumeUp")
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    _mainClient.SendData("{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}");
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "volumeDown")
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    _mainClient.SendData("{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}");
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "getVolume")
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    var value = ScaleUp(Convert.ToInt16(CleanJson(response["payload"]["volume"].ToString())));

                                    if (onVolumeValue != null)
                                        onVolumeValue(Convert.ToUInt16(value));
                                    if (onVolumeMuteState != null)
                                    {
                                        if (CleanJson(response["payload"]["muted"].ToString()) == "true")
                                        {
                                            onVolumeMuteState(1);
                                        }
                                        else if (CleanJson(response["payload"]["muted"].ToString()) == "false")
                                        {
                                            onVolumeMuteState(0);
                                        }
                                    }
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "volumeMuteOn")
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    if (onVolumeMuteState != null)
                                        onVolumeMuteState(1);
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "volumeMuteOff")
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    if (onVolumeMuteState != null)
                                        onVolumeMuteState(0);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_debugMode)
                {
                    ErrorLog.Exception(string.Format("LgWebOs.Display._mainClient_ReceiveDataChange ID={0} Exeption Occured", _id), ex);
                }
            }
        }

        void _mainClient_ConnectionStatusChange(object sender, ConnectionEventArgs e)
        {
            if (e.IsConnected)
            {
                _isBusy = false;
                if (onBusy != null)
                    onBusy(0);

                _isPoweredOn = true;

                if (onPowerState != null)
                    onPowerState(1);

                CTimer WaitForDisplayServerTimer = new CTimer(DisplayServerReady, 500);
            }
            else
            {
                if (_inputControls != null)
                {
                    _inputControls._socketClient.Disconnect();
                }

                _isBusy = false;
                if (onBusy != null)
                    onBusy(0);

                _isPoweredOn = false;

                if (onPowerState != null)
                    onPowerState(0);
            }
        }

        private void Connect()
        {
            try
            {
                if (!_isBusy)
                {
                    _isBusy = true;
                    if (onBusy != null)
                        onBusy(1);
                }

                _mainClient.Connect("ws://" + _ipAddress, _port, string.Empty);
            }
            catch (Exception e)
            {
                if (_debugMode)
                {
                    ErrorLog.Exception(string.Format("LgWebOs.Display.Connect ID={0} Exeption Occured", _id), e);
                }
            }
        }

        public void PowerOn()
        {
            if (_isPoweredOn == false && _isBusy == false)
            {
                _isBusy = true;
                if (onBusy != null)
                    onBusy(1);

                byte[] wolPacket = new byte[1024];
                int wolPacketIndex = 0;

                for (int i = 0; i < 6; i++)
                {
                    wolPacket[wolPacketIndex] = 255;
                    wolPacketIndex++;
                }

                for (int i = 0; i < 16; i++)
                {
                    for (int m = 0; m < _macAddress.Length; m += 2)
                    {
                        var mb = _macAddress.Substring(m, 2);
                        wolPacket[wolPacketIndex] = byte.Parse(mb, NumberStyles.HexNumber);
                        wolPacketIndex++;
                    }
                }

                _udpServer.SendData(wolPacket, wolPacket.Length);
            }
        }

        public void PowerOff()
        {
            if (_isPoweredOn && _isBusy == false)
            {
                _isBusy = true;
                if (onBusy != null)
                    onBusy(1);

                _mainClient.SendData("{\"type\":\"request\",\"id\":\"powerOff\",\"uri\":\"ssap://system/turnOff\"}");
            }
        }

        public void SetVolume(ushort value)
        {
            var volume = ScaleDown(value);

            if (_mainClient.IsConnected == 1)
            {
                _mainClient.SendData("{\"type\":\"request\",\"id\":\"setVolume\",\"uri\":\"ssap://audio/setVolume\",\"payload\":{\"volume\":" + volume + "}}");
            }
        }

        public void IncrementVolume()
        {
            if (_mainClient.IsConnected == 1)
            {
                _mainClient.SendData("{\"type\":\"request\",\"id\":\"volumeUp\",\"uri\":\"ssap://audio/volumeUp\"}");
            }
        }

        public void DecrementVolume()
        {
            if (_mainClient.IsConnected == 1)
            {
                _mainClient.SendData("{\"type\":\"request\",\"id\":\"volumeDown\",\"uri\":\"ssap://audio/volumeDown\"}");
            }
        }

        public void SetMute(ushort value)
        {
            if (_mainClient.IsConnected == 1)
            {
                if (value == 1)
                {
                    _mainClient.SendData("{\"type\":\"request\",\"id\":\"volumeMuteOn\",\"uri\":\"ssap://audio/setMute\", \"payload\":{\"mute\": true}}");
                }
                else
                {
                    _mainClient.SendData("{\"type\":\"request\",\"id\":\"volumeMuteOff\",\"uri\":\"ssap://audio/setMute\", \"payload\":{\"mute\": false}}");
                }
            }
        }

        public void SendKey(string name)
        {
            if (_isPoweredOn && _inputControls != null)
            {
                if (_inputControls._socketClient.IsConnected == 1)
                {
                    _inputControls.SendKey(name);
                }
                else
                {
                    _mainClient.SendData("{\"type\":\"request\",\"id\":\"getInputSocket\",\"uri\":\"ssap://com.webos.service.networkinput/getPointerInputSocket\"}");
                }
            }
            else if (_isPoweredOn)
            {
                _mainClient.SendData("{\"type\":\"request\",\"id\":\"getInputSocket\",\"uri\":\"ssap://com.webos.service.networkinput/getPointerInputSocket\"}");
            }
        }

        public void ChangeInput(ushort input)
        {
            if (_mainClient.IsConnected == 1 && _externalInputs != null)
            {
                if(_externalInputs.Count >= input)
                    _mainClient.SendData("{\"type\":\"request\",\"id\":\"changeInput_" + _externalInputs[input - 1].id + "\",\"uri\":\"ssap://tv/switchInput\", \"payload\":{\"inputId\": \"" + _externalInputs[input - 1].id + "\"}}");
            }
        }

        public void GetInputs()
        {
            if (_mainClient.IsConnected == 1)
            {
                _mainClient.SendData("{\"type\":\"request\",\"id\":\"getExternalInputs\",\"uri\":\"ssap://tv/getExternalInputList\"}");
            }
        }

        public void LaunchApp(ushort index)
        {
            if (_mainClient.IsConnected == 1 && _apps != null)
            {
                if (_apps.Count >= index)
                    _mainClient.SendData("{\"type\":\"request\",\"id\":\"launchApp\",\"uri\":\"ssap://com.webos.applicationManager/launch\", \"payload\": {\"id\": \"" + _apps[index - 1].id + "\"}}");
            }
        }

        public void GetApps()
        {
            if (_mainClient.IsConnected == 1)
            {
                _mainClient.SendData("{\"type\":\"request\",\"id\":\"getAllApps\",\"uri\":\"ssap://com.webos.applicationManager/listLaunchPoints\"}");
            }
        }

        public void SendNotification(string value)
        {
            if (_mainClient.IsConnected == 1)
            {
                _mainClient.SendData("{\"type\":\"request\",\"id\":\"sendNotification\",\"uri\":\"ssap://system.notifications/createToast\",\"payload\":{\"message\":\"" + value + "\"}}");
            }
        }
        #endregion

        #region Timers
        /// <summary>
        /// Waits 2.5 seconds on connection to send required handshake.
        /// </summary>
        /// <param name="o"></param>
        private void DisplayServerReady(object o)
        {
            if (_clientKey == null)
            {
                _mainClient.SendData(_getClientKey());
            }
            else
            {
                _mainClient.SendData(_verifyClientKey());
            }

            _isBusy = false;
            if (onBusy != null)
                onBusy(0);
        }

        private void DisplayGetInfo(object o)
        {
            _mainClient.SendData("{\"type\":\"request\",\"id\":\"getInputSocket\",\"uri\":\"ssap://com.webos.service.networkinput/getPointerInputSocket\"}");
        }
        #endregion

        #region Method Helpers
        private string CleanJson(string value)
        {
            var cleaned = value.Replace("\"", string.Empty);

            return cleaned;
        }

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
