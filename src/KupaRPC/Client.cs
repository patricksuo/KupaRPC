using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace KupaRPC
{
    public class Client
    {
        public void Register(Type serviceInterface)
        {
            throw new NotImplementedException();
        }

        public TService Get<TService>() 
        {
            throw new NotImplementedException();
        }

        public Task ConnectAsync(string host, int port)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync()
        {
            throw new NotImplementedException();
        }
    }
}
