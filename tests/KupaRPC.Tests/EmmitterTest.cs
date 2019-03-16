using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace KupaRPC.Tests
{
    public class EmmitterTest
    {
        [Service(99)]
        public interface IFooService
        {
            [Method(88)]
            Task<string> Echo(string message, IContext context =null);
        }

        public class FooService : IFooService
        {
            public Task<string> Echo(string message, IContext context)
            {
                return Task.FromResult(message);
            }
        }

        public class MockClient
        {
            private readonly Dictionary<Type, object> _results;

            public MockClient(Dictionary<Type, object> results)
            {
                _results = results;
            }

            public Task<string> Blabla(Request<string, string> request, IContext icontext)
            {
                return Task.FromResult("hello");
            }

            public Task<TReply> Send<TArg, TReply>(Request<TArg, TReply> request, IContext icontext)
            {
                if (_results != null && _results.TryGetValue(typeof(TReply), out object result))
                {
                    return Task.FromResult((TReply)result);
                }
                return Task.FromResult<TReply>(default);
            }
        }

        [Fact]
        public void EmmitHandlerTest()
        {
            FooService service = new FooService();
            MethodInfo mi = typeof(IFooService).GetMethod("Echo");

            string handlerName = "FooService.Echo";
            Type paramType = mi.GetParameters()[0].ParameterType;
            Type userReturnType = typeof(string);
            Type returnType = mi.ReturnType;

            string message = "hello";

            Handler handler = Emitter.EmmitHandler(handlerName, mi, service);
            Task<object> task = handler.Process(message, ServerContext.Default);
            task.Wait();
            Assert.True(task.Result is string);
            Assert.True(task.Result as string == message);
        }

        [Fact]
        public void EmitServiceClientsTest()
        {
            RegisterHelper register = new RegisterHelper();
            register.AddService(typeof(IFooService));

            string message = "hello world";
            MockClient client = new MockClient(new Dictionary<Type, object>()
            {
                {message.GetType(), message},
            });

            Dictionary<Type, Func<MockClient, object>> factories = Emitter.EmitServiceClients<MockClient>(register.ServerDefine);

            Func<MockClient, object> factory = factories[typeof(IFooService)];

            IFooService serviceClient = factory(client) as IFooService;

            Task<string> task = serviceClient.Echo(message);
            task.Wait();
            Assert.True(task.Result == message);
        }
    }
}
