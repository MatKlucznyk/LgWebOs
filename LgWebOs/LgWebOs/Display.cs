﻿using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharp.SimplSharpExtensions;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.CrestronWebSocketClient;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.Net;
using Crestron.SimplSharp.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LgWebOs
{
    public class Display
    {
        public delegate void PowerState(ushort state);
        public delegate void VolumeValue(ushort value);
        public delegate void VolumeMuteState(ushort state);
        public delegate void CurrentInputValue(ushort valuet);
        public delegate void ExternalInputNames(SimplSharpString xsig);
        public delegate void ExternalInputIcons(SimplSharpString xsig);
        public delegate void AppNames(SimplSharpString xsig);
        public delegate void AppIcons(SimplSharpString xsig);

        public PowerState onPowerState { get; set; }
        public VolumeValue onVolumeValue { get; set; }
        public VolumeMuteState onVolumeMuteState { get; set; }
        public CurrentInputValue onCurrentInputValue { get; set; }
        public ExternalInputNames onExternalInputNames { get; set; }
        public ExternalInputIcons onExternalInputIcons { get; set; }
        public AppNames onAppNames { get; set; }
        public AppIcons onAppIcons { get; set; }


        private WebSocketClient _socketClient;
        private UDPServer _udpServer;
        private CTimer WaitForConnectionTimer;
        private CTimer PollClientTimer;
        private InputControls _inputControls;
        private List<ExternalInput> _externalInputs;

        private bool _isRegistered;
        private bool _isPoweredOn;
        private string _currentInput;
        private bool _debugMode = true;
        private bool _sentConnection;
        private int _pingCount = 0;
        private uint _port;
        private string _id;
        private string _ipAddress;
        private string _macAddress;
        private string _clientKey;
        private string _keyFilePath;
        private string _getClientKey()
        {
            return
                "{\"type\":\"register\",\"id\":\"register_0\",\"payload\":{\"forcePairing\":false,\"pairingType\":\"PROMPT\",\"manifest\":{\"manifestVersion\":1,\"appVersion\":\"1.1\",\"signed\":{\"created\":\"20140509\",\"appId\":\"com.lge.test\",\"vendorId\":\"com.lge\",\"localizedAppNames\":{\"\":\"LG Remote App\"},\"localizedVendorNames\":{\"\":\"LG Electronics\"},\"permissions\":[\"TEST_SECURE\",\"CONTROL_INPUT_TEXT\",\"CONTROL_MOUSE_AND_KEYBOARD\",\"READ_INSTALLED_APPS\",\"READ_LGE_SDX\",\"READ_NOTIFICATIONS\",\"SEARCH\",\"WRITE_SETTINGS\",\"WRITE_NOTIFICATION_ALERT\",\"CONTROL_POWER\",\"READ_CURRENT_CHANNEL\",\"READ_RUNNING_APPS\",\"READ_UPDATE_INFO\",\"UPDATE_FROM_REMOTE_APP\",\"READ_LGE_TV_INPUT_EVENTS\",\"READ_TV_CURRENT_TIME\",\"READ_INPUT_DEVICE_LIST\"],\"serial\":\"2f930e2d2cfe083771f68e4fe7bb07\"},\"permissions\":[\"LAUNCH\",\"READ_LGE_SDX\",\"READ_LGE_TV_INPUT_EVENTS\",\"SEARCH\",\"CONTROL_MOUSE_AND_KEYBOARD\",\"LAUNCH_WEBAPP\",\"APP_TO_APP\",\"CLOSE\",\"TEST_OPEN\",\"TEST_PROTECTED\",\"CONTROL_AUDIO\",\"CONTROL_DISPLAY\",\"CONTROL_INPUT_JOYSTICK\",\"CONTROL_INPUT_MEDIA_RECORDING\",\"CONTROL_INPUT_MEDIA_PLAYBACK\",\"CONTROL_INPUT_TV\",\"CONTROL_POWER\",\"READ_APP_STATUS\",\"READ_CURRENT_CHANNEL\",\"READ_INPUT_DEVICE_LIST\",\"READ_NETWORK_STATE\",\"READ_RUNNING_APPS\",\"READ_TV_CHANNEL_LIST\",\"WRITE_NOTIFICATION_TOAST\",\"READ_POWER_STATE\",\"READ_COUNTRY_INFO\"],\"signatures\":[{\"signatureVersion\":1,\"signature\":\"eyJhbGdvcml0aG0iOiJSU0EtU0hBMjU2Iiwia2V5SWQiOiJ0ZXN0LXNpZ25pbmctY2VydCIsInNpZ25hdHVyZVZlcnNpb24iOjF9.hrVRgjCwXVvE2OOSpDZ58hR+59aFNwYDyjQgKk3auukd7pcegmE2CzPCa0bJ0ZsRAcKkCTJrWo5iDzNhMBWRyaMOv5zWSrthlf7G128qvIlpMT0YNY+n/FaOHE73uLrS/g7swl3/qH/BGFG2Hu4RlL48eb3lLKqTt2xKHdCs6Cd4RMfJPYnzgvI4BNrFUKsjkcu+WD4OO2A27Pq1n50cMchmcaXadJhGrOqH5YmHdOCj5NSHzJYrsW0HPlpuAx/ECMeIZYDh6RMqaFM2DXzdKX9NmmyqzJ3o/0lkk/N97gfVRLW5hA29yeAwaCViZNCP8iC9aO0q9fQojoa7NQnAtw==\"}]}}}";

        }
        private string _verifyClientKey()
        {
            if (_clientKey != null)
                return "{\"type\":\"register\",\"id\":\"register_1\",\"payload\":{\"client-key\":\"" + _clientKey + "\"}}";
            else
                return string.Empty;
        }

        public void Initialize(string id, string ipAddress, ushort port, string macAddress)
        {

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

            _socketClient = new WebSocketClient();
            _socketClient.URL = "ws://" + _ipAddress;
            _socketClient.Port = _port;
            _socketClient.KeepAlive = true;

            _socketClient.ConnectionCallBack = SocketConnectionCallBack;
            _socketClient.DisconnectCallBack = SocketDisconnectCallBack;
            _socketClient.SendCallBack = SocketSendCallBack;
            _socketClient.ReceiveCallBack = SocketRecieveCallBack;

            _udpServer = new UDPServer(_ipAddress, 40000, 1000);
            _udpServer.EthernetAdapterToBindTo = EthernetAdapterType.EthernetLANAdapter;
            _udpServer.EnableUDPServer();
            WaitForConnectionTimer = new CTimer(PollConnection, this, 5000, 5000);
        }

        private void Connect()
        {
            try
            {
                _socketClient.ConnectAsync();
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
            if (_isPoweredOn == false)
            {
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
                _pingCount = 0;
                WaitForConnectionTimer = new CTimer(PollConnection, this, 5000, 5000);
            }
        }

        public void PowerOff()
        {
            if (_isPoweredOn)
            {
                SendRequest("{\"type\":\"request\",\"id\":\"powerOff\",\"uri\":\"ssap://system/turnOff\"}");
            }
        }

        public void SetVolume(ushort value)
        {
            var volume = ScaleDown(value);

            if (_socketClient.Connected)
            {
                SendRequest(string.Format("{\"type\":\"request\",\"id\":\"setVolume\",\"uri\":\"ssap://audio/setVolume\",\"payload\":{\"volume\":{0}}}", volume));
            }
        }

        public void IncrementVolume()
        {
            if (_socketClient.Connected)
            {
                SendRequest("{\"type\":\"request\",\"id\":\"volumeUp\",\"uri\":\"ssap://audio/volumeUp\"}");
            }
        }

        public void DecrementVolume()
        {
            if (_socketClient.Connected)
            {
                SendRequest("{\"type\":\"request\",\"id\":\"volumeDown\",\"uri\":\"ssap://audio/volumeDown\"}");
            }
        }

        public void SetMute(ushort value)
        {
            if (_socketClient.Connected)
            {
                if (value == 1)
                {
                    SendRequest("{\"type\":\"request\",\"id\":\"volumeMuteOn\",\"uri\":\"ssap://audio/setMute\", \"payload\":{\"mute\": true}}");
                }
                else
                {
                    SendRequest("{\"type\":\"request\",\"id\":\"volumeMuteOff\",\"uri\":\"ssap://audio/setMute\", \"payload\":{\"mute\": false}}");
                }
            }
        }

        public void SendKey(string name)
        {
            if (_isPoweredOn && _inputControls != null)
            {
                if (_inputControls._socketClient.Connected)
                {
                    _inputControls.SendKey(name);
                }
            }
        }

        public void ChangeInput(ushort input)
        {
            if (_socketClient.Connected && _externalInputs != null)
            {
                if(_externalInputs.Count >= input)
                    SendRequest("{\"type\":\"request\",\"id\":\"changeInput_" + _externalInputs[input - 1].id + "\",\"uri\":\"ssap://tv/switchInput\", \"payload\":{\"inputId\": \"" + _externalInputs[input - 1].id + "\"}}");
            }
        }

        public void GetInputs()
        {
            if (_socketClient.Connected)
            {
                SendRequest("{\"type\":\"request\",\"id\":\"getExternalInputs\",\"uri\":\"ssap://tv/getExternalInputList\"}");
            }
        }

        private void PollConnection(object o)
        {
            if (_pingCount >= 2 && !_sentConnection)
            {
                _isPoweredOn = false;
                WaitForConnectionTimer.Stop();
                WaitForConnectionTimer.Dispose();
            }
            else
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.Port = Convert.ToInt16(_port);
                        client.KeepAlive = false;
                        client.TimeoutEnabled = true;
                        client.Timeout = 4;
                        client.AllowAutoRedirect = false;
                        client.MaximumAutomaticRedirections = 200;

                        HttpClientRequest request = new HttpClientRequest();
                        request.Url.Parse(string.Format("http://{0}", _ipAddress));
                        request.KeepAlive = false ;

                        request.RequestType = Crestron.SimplSharp.Net.Http.RequestType.Get;

                        HttpClientResponse response = client.Dispatch(request);

                        if (response.ContentString.Contains("Hello world") && !_sentConnection)
                        {
                            _sentConnection = true;
                            WaitForConnectionTimer.Stop();
                            WaitForConnectionTimer.Dispose();
                            Connect();
                        }
                    }
                }
                catch (Exception e)
                {
                    if (_debugMode)
                    {
                        ErrorLog.Exception(string.Format("LgWebOs.Display.TestConnection ID={0} Exeption Occured", _id), e);
                    }
                }

                _pingCount++;
            }
        }

        private int SocketConnectionCallBack(WebSocketClient.WEBSOCKET_RESULT_CODES resultCode)
        {
            if (resultCode == WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS)
            {
                _isPoweredOn = true;

                if (onPowerState != null)
                    onPowerState(1);

                CTimer WaitForDisplayServerTimer = new CTimer(DisplayServerReady, 2500);
            }
            else if (resultCode != WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS)
            {
                _sentConnection = false;
            }
            return 0;
        }

        private void DisplayServerReady(object o)
        {
            PollClientTimer = new CTimer(PollClient, this, 0, 100);

            if (_clientKey == null)
            {
                SendRequest(_getClientKey());
            }
            else
            {
                SendRequest(_verifyClientKey());
            }
        }

        private int SocketDisconnectCallBack(WebSocketClient.WEBSOCKET_RESULT_CODES resultCode, object obj)
        {
            if (resultCode == WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS)
            {
                _sentConnection = false;
                _isPoweredOn = false;

                if (onPowerState != null)
                    onPowerState(0);
            }

            return 0;
        }

        private int SocketSendCallBack(WebSocketClient.WEBSOCKET_RESULT_CODES resultCode)
        {
            if (resultCode == WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS)
            {
            }

            return 0;
        }

        private int SocketRecieveCallBack(byte[] data, uint length, WebSocketClient.WEBSOCKET_PACKET_TYPES opCode, WebSocketClient.WEBSOCKET_RESULT_CODES resultCode)
        {
            if (resultCode == WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS)
            {
                var sData = Encoding.ASCII.GetString(data, 0, data.Length);

                JObject response = JObject.Parse(sData);

                
                if (response["type"] != null)
                {
                    if (CleanJson(response["type"].ToString()) == "registered")
                    {
                        _isRegistered = true;

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

                                    SendRequest(_verifyClientKey());
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "register_1")
                            {
                                SendRequest("{\"type\":\"request\",\"id\":\"getInputSocket\",\"uri\":\"ssap://com.webos.service.networkinput/getPointerInputSocket\"}");
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
                                    _inputControls.PollConnectionTimer.Stop();
                                    _inputControls.PollConnectionTimer.Dispose();
                                    PollClientTimer.Stop();
                                    PollClientTimer.Dispose();
                                    _inputControls._socketClient.DisconnectAsync(this);
                                    _socketClient.DisconnectAsync(this);
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "getInputSocket")
                            {
                                _inputControls = new InputControls(_ipAddress, _port, CleanJson(response["payload"]["socketPath"].ToString()));
                            }
                            else if (CleanJson(response["id"].ToString()).Contains("changeInput_"))
                            {
                                if(CleanJson(response["payload"]["returnValue"].ToString()) == "true")
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
                                    inputIcons.Add(input.icon);
                                }

                                if (onExternalInputNames != null)
                                {
                                    var encodedBytes = XSig.GetBytes(1, inputNames.ToArray());
                                    onExternalInputNames(Encoding.GetEncoding(28591).GetString(encodedBytes, 0, encodedBytes.Length));
                                }

                                if (onExternalInputIcons != null)
                                {
                                    var encodedBytes = XSig.GetBytes(1, inputIcons.ToArray());
                                    onExternalInputIcons(Encoding.GetEncoding(28591).GetString(encodedBytes, 0, encodedBytes.Length));
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "setVolume")
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    SendRequest("{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}");
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "volumeUp")
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    SendRequest("{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}");
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "volumeDown")
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    SendRequest("{\"type\":\"request\",\"id\":\"getVolume\",\"uri\":\"ssap://audio/getVolume\"}");
                                }
                            }
                            else if (CleanJson(response["id"].ToString()) == "getVolume")
                            {
                                if (CleanJson(response["payload"]["returnValue"].ToString()) == "true")
                                {
                                    var value = ScaleUp(Convert.ToInt16(CleanJson(response["payload"]["volume"].ToString())));

                                    if (onVolumeValue != null)
                                        onVolumeValue(Convert.ToUInt16(value));
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
            return 0;
        }

        private void PollClient(object o)
        {
            try
            {
                if (_socketClient.Connected)
                {
                    var value = _socketClient.ReceiveAsync();
                }
                else
                {
                    _inputControls.PollConnectionTimer.Stop();
                    _inputControls.PollConnectionTimer.Dispose();
                    PollClientTimer.Stop();
                    PollClientTimer.Dispose();
                    _inputControls._socketClient.DisconnectAsync(this);
                    _socketClient.DisconnectAsync(this);
                }
            }
            catch (SocketException e)
            {
                if (_debugMode)
                {
                    ErrorLog.Exception(string.Format("LgWebOs.Display.CheckForData ID={0} SocketExeption Occured", _id), e);
                }
            }
        }

        private void SendRequest(string request)
        {
            if (_socketClient.Connected)
            {
                var data = Encoding.ASCII.GetBytes(request);

                _socketClient.SendAsync(data, Convert.ToUInt16(data.Length), WebSocketClient.WEBSOCKET_PACKET_TYPES.LWS_WS_OPCODE_07__TEXT_FRAME);
            }
        }

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
    }
}
