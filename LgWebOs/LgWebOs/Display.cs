using System;
using System.Text;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronWebSocketClient;

namespace LgWebOs
{
    public class Display
    {
        private WebSocketClient socketClient;

        public void Connect(string ipAddress, ushort port)
        {
            socketClient = new WebSocketClient();
            socketClient.Host = ipAddress;
            socketClient.Port = port;

            socketClient.ConnectionCallBack = SocketConnectionCallBack;
            socketClient.DisconnectCallBack = SocketDisconnectCallBack;
            socketClient.SendCallBack = SocketSendCallBack;
            socketClient.ReceiveCallBack = SocketRecieveCallBack;
            
        }

        private int SocketConnectionCallBack(WebSocketClient.WEBSOCKET_RESULT_CODES resultCode)
        {
            return 0;
        }

        private int SocketDisconnectCallBack(WebSocketClient.WEBSOCKET_RESULT_CODES resultCode, object obj)
        {
            return 0;
        }

        private int SocketSendCallBack(WebSocketClient.WEBSOCKET_RESULT_CODES resultCode)
        {

            return 0;
        }

        private int SocketRecieveCallBack(byte[] data, uint length, WebSocketClient.WEBSOCKET_PACKET_TYPES opCode, WebSocketClient.WEBSOCKET_RESULT_CODES resultCode)
        {

            return 0;
        }
    }
}
