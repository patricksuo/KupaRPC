using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace KupaRPC.Tests
{
    public class RegisterHelperTests
    {
        [Service(1)]
        public interface IFooService
        {
            [Method(1)]
            Task<int> Add(int a, int b);

            [Method(2)]
            ValueTask<int> Multiply(int a, int b);

            [Method(3)]
            Task Ping();

            [Method(4)]
            ValueTask Pong();

            [Method(5)]
            Task<Dictionary<string, string>> Blabla();
        }

        [Service(2)]
        public interface IBarService
        {
            [Method(1)]
            Task<int> Add(int a, int b);

            [Method(2)]
            ValueTask<int> Multiply(int a, int b);

            [Method(3)]
            Task Ping();

            [Method(4)]
            ValueTask Pong();

            [Method(5)]
            Task<Dictionary<string, string>> Blabla();
        }

        [Fact]
        public void Test1()
        {
            RegisterHelper register = new RegisterHelper();
            register.AddService(typeof(IFooService));
            register.AddService(typeof(IBarService));
            register.Finish();
        }
    }
}
