using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Pipelines.Sockets.Unofficial;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KupaRPC
{
    public class Server : SocketServer
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly TaskCompletionSource<bool> _serveComplete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly RegisterHelper _registerHelper = new RegisterHelper();
        private readonly Dictionary<uint, Handler> _handlers = new Dictionary<uint, Handler>();

        public Server(ILoggerFactory loggerFactory = null)
        {
            if (loggerFactory == null)
            {
                loggerFactory = new NullLoggerFactory();
            }
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger("KupaRPC");
        }

        public void Register<T>(T service)
        {
            ServiceDefine serviceDefine = _registerHelper.AddService(typeof(T));

            foreach (MethodDefine methodDefine in serviceDefine.Methods.Values)
            {
                string name = $"{serviceDefine.Type.Name}.{methodDefine.Name}";
                Handler handler = Emitter.EmmitHandler(name, methodDefine.MethodInfo, service);
                uint key = HandlerKey(serviceDefine.ID, methodDefine.ID);
                _handlers.Add(key, handler);
            }
        }

        public async Task ServeAsync(string host, int port)
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host);
            foreach (IPAddress address in addresses)
            {
                IPEndPoint endPoint = new IPEndPoint(address, port);
                Listen(endPoint);
                await _serveComplete.Task;
                return;
            }
            throw new Exception($"Dns Resolve failed for {host}");
        }

        protected override Task OnClientConnectedAsync(in ClientConnection client)
        {
            ILogger logger = _loggerFactory.CreateLogger($"KupaRPC|{client.RemoteEndPoint.ToString()}");
            ServerClient serverClient = new ServerClient(_cancellation.Token, logger, this, client.Transport, Codec.New(_registerHelper.ServerDefine.Services.Values));
            return serverClient.Serve();
        }

        protected override void OnServerFaulted(Exception exception)
        {
            _logger.LogError(exception, "Server Faulted");
            _serveComplete.TrySetException(exception);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HandlerKey(ushort serviceID, ushort methodID)
        {
            return ((uint)serviceID << 8) | methodID;
        }

        internal bool TryGetHandler(ushort serviceID, ushort methodID, out Handler handler)
        {
            return _handlers.TryGetValue(HandlerKey(serviceID, methodID), out handler);
        }
    }
}
