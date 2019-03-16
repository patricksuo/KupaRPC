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

    internal class ServerDefine
    {
        public Dictionary<ushort, ServiceDefine> Services = new Dictionary<ushort, ServiceDefine>();
    }

    internal class ServiceDefine
    {
        public ushort ID;
        public Type Type;
        public Dictionary<ushort, MethodDefine> Methods = new Dictionary<ushort, MethodDefine>();
    }

    internal class MethodDefine
    {
        public ushort ID;
        public string Name;
        public MethodInfo MethodInfo;
        public ParameterInfo Parameter;
        public Type ReturnType;
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

            if (ServerDefine.Services.TryGetValue(service.ID, out ServiceDefine conflictedService))
            {
                throw new ArgumentException($"{serviceType.ToString()} has conflicted ID({service.ID}) with {conflictedService.Type.ToString()}");
            }

            CollectMethods(service);

            ServerDefine.Services.Add(service.ID, service);

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

                if (service.Methods.TryGetValue(methodID, out MethodDefine conflictedMethod))
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
                    MethodInfo = mi,
                    Parameter = pi,
                    ReturnType = returnType,
                };
                service.Methods.Add(methodID, methodDefine);
            }

            if (service.Methods.Count == 0)
            {
                throw new ArgumentException($"{service.Type.Name} does not have any method");
            }
        }
    }
}
