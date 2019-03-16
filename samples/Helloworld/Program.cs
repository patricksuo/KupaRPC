using System;
using System.Threading.Tasks;
using KupaRPC;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

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
        Task<int> Multiply(Args args, IContext context = null);

        [Method(2)]
        Task<int> Add(Args args, IContext context = null);
    }



    public class Arith : IArith
    {
        public Task<int> Multiply(Args args, IContext context = null)
        {
            return Task.FromResult(args.A * args.B);
        }

        public Task<int> Add(Args args, IContext context = null)
        {
            return Task.FromResult(args.A + args.B);
        }
    }


    class Program
    {
        static async Task Main(string[] args)
        {
            string host = "127.0.0.1";
            int port = 8080;


            Server server = new Server();
            server.Register<IArith>(new Arith());
            _ = Task.Run(async () =>
            {
                try
                {
                    await server.ServeAsync(host, port);
                }
                catch (Exception e)
                {
                    Console.WriteLine("RPC Server exit with exception {0}", e.ToString());
                }
            });

            ClientFactory clientFactory = new ClientFactory();
            clientFactory.Register(typeof(IArith));
            clientFactory.Finish();
            Client client = await clientFactory.ConnectAsync(host, port);

            IArith service = client.Get<IArith>();

            Args arg = new Args
            {
                A = 9,
                B = 9,
            };
            int result = await service.Multiply(arg);
            Console.WriteLine($"{arg.A} * {arg.B} = {result}");

            int result2 = await service.Add(arg);
            Console.WriteLine($"{arg.A} + {arg.B} = {result2}");
            await client.StopAsync();
        }
    }
}
