using System;
using System.Threading;
using System.Threading.Tasks;

namespace KupaRPC
{
    public class Server
    {
        public void Register(ushort serviceID, Type serviceInterface, object service)
        {
            throw new NotImplementedException();
        }

        public Task ServeAsync(string host, int port)
        {
            throw new NotImplementedException();
        }

    }
}
