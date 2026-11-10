using System;
using System.Net.Sockets;
using Perpetuum.Log;

namespace Perpetuum.Network
{
    public static class SocketExtensions
    {
        [UsedImplicitly]
        public static void SetKeepAlive(this Socket socket, bool state, TimeSpan time, TimeSpan interval)
        {
            try
            {
                if (state)
                {
                    // TCP keepalive timers use whole seconds. Round up to preserve the minimum delay.
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime,
                        checked((int)Math.Ceiling(time.TotalSeconds)));
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval,
                        checked((int)Math.Ceiling(interval.TotalSeconds)));
                }

                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, state);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }
        }

        public static void SetKeepAlive(this Socket socket, bool state, uint time, uint interval)
        {
            socket.SetKeepAlive(state, TimeSpan.FromMilliseconds(time), TimeSpan.FromMilliseconds(interval));
        }

    }
}