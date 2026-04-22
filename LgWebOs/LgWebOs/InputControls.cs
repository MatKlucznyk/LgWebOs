using System;
using Avg.Communications.Sockets;
using Avg.ModuleFramework.Logging;

namespace LgWebOs
{
    internal class InputControls : IDisposable
    {
        private readonly WebSocketClient _socketClient;

        internal bool IsConnected => _socketClient?.IsConnected ?? false;

        internal InputControls(string ipAddress, ushort port, string path, ILogger logger)
        {
            _socketClient = new WebSocketClient(new Logger("LgWebOs -- Input Controls")
            {
                DebugLevel = logger.DebugLevel
            });

            path = path.Replace("ws:", string.Empty);
            _socketClient.Connect("ws://" + ipAddress + path, port);
        }

        internal void SendKey(string key)
        {
            key = string.Format("type:button\nname:{0}\n\n", key.ToUpper());

            _socketClient.SendCommand(key);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_socketClient?.Disposed ?? true)
                return;

            if (disposing)
            {
                _socketClient?.Dispose();
            }
        }
    }
}