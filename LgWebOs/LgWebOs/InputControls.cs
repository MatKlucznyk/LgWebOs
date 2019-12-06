using System;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronWebSocketClient;
using Crestron.SimplSharp.CrestronSockets;

namespace LgWebOs
{
    internal class InputControls
    {
        internal WebSocketClient _socketClient;
        internal CTimer PollConnectionTimer;

        internal InputControls(string ipAddress, uint port, string path)
        {
            _socketClient = new WebSocketClient();

            path = path.Replace("ws:", string.Empty);
            _socketClient.Port = port;
            _socketClient.URL = string.Format("ws://{0}{1}", ipAddress, path);
            _socketClient.KeepAlive = true;

            _socketClient.ConnectionCallBack = ConnectionCallBack;
            _socketClient.SendCallBack = SendCallBack;
            _socketClient.ReceiveCallBack = ReceiveCallBack;
            _socketClient.ConnectAsync();
        }

        internal void SendKey(string key)
        {
            key = string.Format("type:button\nname:{0}\n\n", key.ToUpper());

            if (_socketClient.Connected)
            {
                var data = Encoding.ASCII.GetBytes(key);

                _socketClient.SendAsync(data, Convert.ToUInt16(data.Length), WebSocketClient.WEBSOCKET_PACKET_TYPES.LWS_WS_OPCODE_07__TEXT_FRAME);
            }
        }

        private int ConnectionCallBack(WebSocketClient.WEBSOCKET_RESULT_CODES resultCode)
        {
            if (resultCode == WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS)
            {
                PollConnectionTimer = new CTimer(PollConnection, this, 1000, 1000);
            }

            return 0;
        }

        private int SendCallBack(WebSocketClient.WEBSOCKET_RESULT_CODES resultCode)
        {
            if (resultCode == WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS)
            {
                _socketClient.ReceiveAsync();
            }

            return 0;
        }

        private int ReceiveCallBack(byte[] data, uint dataLength, WebSocketClient.WEBSOCKET_PACKET_TYPES opcode, WebSocketClient.WEBSOCKET_RESULT_CODES resultCode)
        {
            if (resultCode == WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS && opcode == WebSocketClient.WEBSOCKET_PACKET_TYPES.LWS_WS_OPCODE_07__TEXT_FRAME)
            {
                var sData = Encoding.ASCII.GetString(data, 0, Convert.ToInt16(dataLength));
            }

            return 0;
        }

        private void PollConnection(object o)
        {
            if (!_socketClient.Connected)
            {
                _socketClient.Dispose();
            }
        }
    }
}