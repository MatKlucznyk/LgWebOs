using System;
using System.Text;
using Guss.Communications.Sockets;
using Guss.ModuleFramework.Logging;

namespace LgWebOs
{
    internal class InputControls
    {
        private bool _disposed;
        internal WebSocketClient _socketClient;

        internal InputControls(string ipAddress, ushort port, string path, ILogger logger)
        {
            _socketClient = new WebSocketClient(logger);

            path = path.Replace("ws:", string.Empty);
            _socketClient.Connect("ws://" + ipAddress + path, port);
        }

        internal void SendKey(string key)
        {
            key = string.Format("type:button\nname:{0}\n\n", key.ToUpper());

            var data = Encoding.ASCII.GetBytes(key);

            _socketClient.SendCommand(key);
        }

        internal void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if(disposing)
            {
                if(_socketClient != null)
                    _socketClient.Dispose();
            }
        }
    }
}