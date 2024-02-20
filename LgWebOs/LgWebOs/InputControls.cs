using System;
using Guss.Communications.Sockets;
using Guss.ModuleFramework.Logging;

namespace LgWebOs
{
    internal class InputControls : IDisposable
    {
        private bool _disposed;
        internal WebSocketClient SocketClient;

        internal InputControls(string ipAddress, ushort port, string path, ILogger logger)
        {
            SocketClient = new WebSocketClient(logger);

            path = path.Replace("ws:", string.Empty);
            SocketClient.Connect("ws://" + ipAddress + path, port);
        }

        internal void SendKey(string key)
        {
            key = string.Format("type:button\nname:{0}\n\n", key.ToUpper());

            SocketClient.SendCommand(key);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;

            if(disposing)
            {
                if(SocketClient != null)
                    SocketClient.Dispose();
            }
        }
    }
}