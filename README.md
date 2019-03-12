### KupaRPC
easy-to-use RPC for [.Net Core](http://github.com/dotnet/). Inspired by  [net/rpc](https://golang.org/pkg/net/rpc/)

**status** : under construction

### Sample
```csharp
using System;
using System.Threading.Tasks;
using KupaRPC;

namespace Helloworld
{

    public class Args
    {
        public int A;
        public int B;
    }

    [Service(1024)]
    public interface IArith
    {
        [Method(1)]
        Task<int> Multiply(Args args);

        [Method(2)]
        ValueTask<int> Multiply2(Args args);
    }



    public class Arith : IArith
    {
        public Task<int> Multiply(Args args)
        {
            return Task.FromResult(args.A * args.B);
        }

        public ValueTask<int> Multiply2(Args args)
        {
            return new ValueTask<int>(args.A * args.B);
        }
    }


    class Program
    {
        static async Task Main(string[] args)
        {
            string host = "127.0.0.1";
            int port = 8080;

            Server server = new Server();
            server.Register(typeof(IArith), new Arith());
            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    await server.ServeAsync(host, port);
                }
                catch (Exception e)
                {
                    Console.WriteLine("RPC Server exit with exception {0}", e.ToString());
                }
            }, TaskCreationOptions.LongRunning);

            Client client = new Client();
            client.Register(typeof(IArith));
            await client.ConnectAsync(host, port);

            IArith service = client.Get<IArith>();

            Args arg = new Args
            {
                A = 9,
                B = 9,
            };
            int result = await service.Multiply(arg);
            Console.WriteLine($"{arg.A} * {arg.B} = {result}");

            int result2 = await service.Multiply2(arg);
            Console.WriteLine($"{arg.A} * {arg.B} = {result2}");

            await client.StopAsync();
        }
    }
}

```