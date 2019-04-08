using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("KupaRPC.Tests")]


namespace KupaRPC
{

    public class ServerDefine
    {
        internal Dictionary<ushort, ServiceDefine> _serviceDict = new Dictionary<ushort, ServiceDefine>();
        public IEnumerable<ServiceDefine> Services => _serviceDict.Values;
    }

    public class ServiceDefine
    {
        public ushort ID { get; internal set; }
        public Type Type { get; internal set; }
        internal Dictionary<ushort, MethodDefine> _methodDict = new Dictionary<ushort, MethodDefine>();
        public IEnumerable<MethodDefine> Methods => _methodDict.Values;
    }

    public class MethodDefine
    {
        public ushort ID { get; internal set; }
        public string Name { get; internal set; }
        internal MethodInfo _methodInfo;
        public Type RpcParamType { get; internal set; }
        public Type RpcReturnType { get; internal set; }
    }

    internal class RegisterHelper
    {
        public ServerDefine ServerDefine { get; } = new ServerDefine();

        public ServiceDefine AddService(Type serviceType)
        {
            ServiceDefine service = new ServiceDefine()
            {
                Type = serviceType
            };


            if (!serviceType.IsInterface)
            {
                throw new ArgumentException($"{serviceType.ToString()} should be interface");
            }

            if (serviceType.ContainsGenericParameters)
            {
                throw new ArgumentException($"{serviceType.ToString()} contains generic parameters which is not supported");
            }

            ServiceAttribute attribute = serviceType.GetCustomAttribute<ServiceAttribute>();
            if (attribute == null)
            {
                throw new ArgumentException($"{serviceType.ToString()} should have {nameof(ServiceAttribute)} attribute");
            }

            service.ID = attribute.ServiceID;

            if (ServerDefine._serviceDict.TryGetValue(service.ID, out ServiceDefine conflictedService))
            {
                throw new ArgumentException($"{serviceType.ToString()} has conflicted ID({service.ID}) with {conflictedService.Type.ToString()}");
            }

            CollectMethods(service);

            ServerDefine._serviceDict.Add(service.ID, service);

            return service;
        }

        private void CollectMethods(ServiceDefine service)
        {
            foreach (MethodInfo mi in service.Type.GetMethods())
            {
                MethodAttribute attribute = mi.GetCustomAttribute<MethodAttribute>();
                if (attribute == null)
                {
                    throw new ArgumentException($"in {service.Type.Name} method {mi.Name} should have {nameof(MethodAttribute)} attribute");
                }

                ushort methodID = attribute.MethodID;

                if (service._methodDict.TryGetValue(methodID, out MethodDefine conflictedMethod))
                {
                    throw new ArgumentException($"in {service.Type.Name} method {mi.Name} have conficted method ID({methodID}) with {conflictedMethod.Name}");
                }


                if (mi.ContainsGenericParameters)
                {
                    throw new ArgumentException($"in {service.Type.Name} method {mi.Name} contains generic parameters which is not supported");
                }

                ParameterInfo[] parameters = mi.GetParameters();
                if (parameters.Length != 2)
                {
                    throw new ArgumentException($"in {service.Type.Name} method {mi.Name} illegal parameter num");
                }

                ParameterInfo pi = parameters[0];
                if (pi.IsIn || pi.IsOut || pi.IsRetval)
                {
                    throw new ArgumentException($"in {service.Type.Name} method {mi.Name} parameter modifier is not supported");
                }

                ParameterInfo ctxPi = parameters[1];
                if (ctxPi.IsIn || ctxPi.IsOut || ctxPi.IsRetval || ctxPi.ParameterType != typeof(IContext))
                {
                    throw new ArgumentException($"in {service.Type.Name} method {mi.Name} illegal context parameter");
                }

                if (!mi.ReturnType.IsGenericType ||
                mi.ReturnType.GetGenericTypeDefinition() != typeof(Task<>) ||
                mi.ReturnType.GetGenericArguments().Length != 1)
                {
                    throw new ArgumentException($"in {service.Type.Name} method {mi.Name} has illegal return type {mi.ReturnType}");
                }

                Type returnType = mi.ReturnType.GetGenericArguments()[0];


                MethodDefine methodDefine = new MethodDefine()
                {
                    ID = methodID,
                    Name = mi.Name,
                    _methodInfo = mi,
                    RpcParamType = pi.ParameterType,
                    RpcReturnType = returnType,
                };
                service._methodDict.Add(methodID, methodDefine);
            }

            if (service._methodDict.Count == 0)
            {
                throw new ArgumentException($"{service.Type.Name} does not have any method");
            }
        }
    }
}
