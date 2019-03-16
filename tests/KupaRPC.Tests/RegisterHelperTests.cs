using System;
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
            Task<int> Add(ValueTuple<int, int> arg, IContext context);

            [Method(2)]
            Task<int> Multiply(ValueTuple<int, int> arg, IContext context);

        }

        [Service(2)]
        public interface IBarService
        {
            [Method(1)]
            Task<int> Add(ValueTuple<int, int> arg, IContext context);

            [Method(2)]
            Task<int> Multiply(ValueTuple<int, int> arg, IContext context);
        }

        [Fact]
        public void RegisterSaneTest()
        {
            RegisterHelper register = new RegisterHelper();
            register.AddService(typeof(IFooService));
            register.AddService(typeof(IBarService));
        }
    }
}
