using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Pipelines.Sockets.Unofficial;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KupaRPC
{
    public class ClientFactory
    {
        private ILoggerFactory _loggerFactory = new NullLoggerFactory();

        private readonly object _syncObj = new object();
        private bool _finish = false;
        private readonly RegisterHelper _registerHelper = new RegisterHelper();
        private Dictionary<Type, Func<Client, object>> _serviceClientFactories = null;


        public ClientFactory Register(Type serviceInterface)
        {
            lock (_syncObj)
            {
                if (_finish)
                {
                    throw new Exception($"`{nameof(Register)}` should be used before `{nameof(Finish)}`");
                }
                _registerHelper.AddService(serviceInterface);
            }

            return this;
        }

        public ClientFactory UseLoggerFactory(ILoggerFactory loggerFactory)
        {
            lock (_syncObj)
            {
                if (_finish)
                {
                    throw new Exception($"`{nameof(UseLoggerFactory)}` should be used before `{nameof(Finish)}`");
                }

                _loggerFactory = loggerFactory;
                return this;
            }
        }

        public ClientFactory Finish()
        {
            lock (_syncObj)
            {
                _finish = true;

                _serviceClientFactories = Emitter.EmitServiceClients<Client>(_registerHelper.ServerDefine);

            }
            return this;
        }

        private Codec NewCodec()
        {
            Ceras.SerializerConfig config = new Ceras.SerializerConfig();
            foreach (ServiceDefine service in _registerHelper.ServerDefine.Services.Values)
            {
                foreach (MethodDefine method in service.Methods.Values)
                {
                    if (!config.KnownTypes.Contains(method.Parameter.ParameterType))
                    {
                        config.KnownTypes.Add(method.Parameter.ParameterType);
                    }
                    if (!config.KnownTypes.Contains(method.ReturnType))
                    {
                        config.KnownTypes.Add(method.ReturnType);
                    }
                }
            }

            Ceras.CerasSerializer serializer = new Ceras.CerasSerializer(config);
            Codec codec = new Codec(serializer);
            return codec;
        }

        public async Task<Client> ConnectAsync(string host, int port)
        {
            lock (_syncObj)
            {
                if (!_finish)
                {
                    throw new Exception($"`{nameof(ConnectAsync)}` should be used after `{nameof(Finish)}`");
                }
            }

            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host);
            foreach (IPAddress address in addresses)
            {
                IPEndPoint endpoint = new IPEndPoint(address, port);
                SocketConnection conn = await SocketConnection.ConnectAsync(endpoint);
                return new Client(this, _loggerFactory, conn, NewCodec());
            }

            throw new Exception($"GetHostAddress failed: {host}");
        }

        internal TService GetServiceClient<TService>(Client client)
        {
            if (!_serviceClientFactories.TryGetValue(typeof(TService), out Func<Client, object> factory))
            {
                throw new ArgumentException($"unknow service {typeof(TService).ToString()}");
            }

            return (TService)factory(client);
        }
    }
}
