#if false
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace KupaRPC
{
    [Service(1)]
    public interface IFooService
    {
        [Method(1)]
        Task<bool> Pong(long unixTimestamp, IContext context=null);

        [Method(2)]
        Task<bool> Ping(long unixTimestamp, IContext context = null);

    }


    public class FooServiceClient : IFooService
    {
        readonly private Client _client;

        public FooServiceClient(Client client)
        {
            _client = client;
        }

        public Task<bool> Ping(long unixTimestamp, IContext context = null)
        {
            Request<long, bool> request = new Request<long, bool>
            {
                ServiceID = 1,
                MethodID = 1,
                Arg = unixTimestamp,
            };

            return _client.Send(request, context);
        }

        public Task<bool> Pong(long unixTimestamp, IContext context = null)
        {
            Request<long, bool> request = new Request<long, bool>
            {
                ServiceID = 1,
                MethodID = 1,
                Arg = unixTimestamp,
            };

            return _client.Send(request, context);
        }
    }
}
#endif
