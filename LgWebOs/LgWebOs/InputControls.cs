﻿using System.Text;
using WS_Client;

namespace LgWebOs
{
    internal class InputControls
    {
        //internal WsClient _socketClient;

        internal AltWsClient _socketTestClient;

        internal InputControls(string ipAddress, ushort port, string path, string id)
        {
            //_socketClient = new WsClient();
            _socketTestClient = new AltWsClient();

            path = path.Replace("ws:", string.Empty);

            //_socketClient.AutoReconnect = 0;
           // _socketClient.ID = "LgWebOs - InputControls - " + id;
            //_socketClient.Connect("ws://" + ipAddress, port, path);

            _socketTestClient.Connect("ws://" + ipAddress, port, path);
        }

        internal void SendKey(string key)
        {
            key = string.Format("type:button\nname:{0}\n\n", key.ToUpper());

            /*if (_socketClient.IsConnected == 1)
            {
                var data = Encoding.ASCII.GetBytes(key);

                _socketClient.SendData(key);
            }*/

            if (_socketTestClient.IsConnected)
            {
                var data = Encoding.ASCII.GetBytes(key);

                _socketTestClient.SendData(key);
            }
        }
    }
}