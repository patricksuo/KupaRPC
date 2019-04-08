using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;

[assembly: InternalsVisibleTo("KupaRPC.Tests")]


namespace KupaRPC
{
    internal class Emitter
    {
        public static readonly string s_assemblyName = "KupaRpcServiceClients";
        public static readonly string s_moduleName = "KupaRpcServiceClients.dll";
        public static readonly string s_namespace = "KupaRpcServiceClients";


        public static Handler EmmitHandler(string methodName, MethodInfo methodInfo, object service)
        {
            Type returnType = methodInfo.ReturnType;
            Type userReturnType = returnType.GenericTypeArguments[0];
            Type userParamType = methodInfo.GetParameters()[0].ParameterType;

            Type delegateType = typeof(Func<,,>).MakeGenericType(userParamType, typeof(IContext), returnType);

            // step 1 build delegate Func<TArg, IContext, Task<TReply>> for Handler<TArg, TReply>
            Delegate methodCallDelegate = Delegate.CreateDelegate(delegateType, service, methodInfo);

            // step 2 build new statement: `new Handler<TArg, TReply>(name, handler)` 
            Type handlerType = typeof(Handler<,>).MakeGenericType(userParamType, userReturnType);
            ConstructorInfo cotrInfo = handlerType.GetConstructor(new Type[] { typeof(string), delegateType });
            ParameterExpression nameParamExpr = Expression.Parameter(typeof(string));
            ParameterExpression delegateParamExpr = Expression.Parameter(delegateType);
            NewExpression newExpr = Expression.New(cotrInfo, nameParamExpr, delegateParamExpr);

            LambdaExpression newLambda = Expression.Lambda(newExpr, nameParamExpr, delegateParamExpr);

            string code = newLambda.ToString();
            _ = code;

            return (Handler)newLambda.Compile().DynamicInvoke(methodName, methodCallDelegate);
        }

        public static Dictionary<Type, Func<TClient, object>> EmitServiceClients<TClient>(ServerDefine serverDefine)
        {
            Dictionary<Type, Func<TClient, object>> factories = new Dictionary<Type, Func<TClient, object>>();
            Dictionary<Type, Type> service2Impl = new Dictionary<Type, Type>();

            AssemblyName name = new AssemblyName(s_assemblyName);
            AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
            ModuleBuilder mb = ab.DefineDynamicModule(s_moduleName);

            foreach (ServiceDefine service in serverDefine.Services)
            {
                Type implType = EmitServiceClient<TClient>(mb, service);
                service2Impl.Add(service.Type, implType);
            }

            foreach ((Type serviceType, Type implType) in service2Impl)
            {
                factories.Add(serviceType, (TClient client) =>
                {
                    return Activator.CreateInstance(implType, client);
                });
            }

            return factories;
        }

        private static Type EmitServiceClient<TClient>(ModuleBuilder mBuilder, ServiceDefine serviceDef)
        {
            string name = s_namespace + "." + serviceDef.Type.ToString() + "__ServiceClientImpl";
            TypeBuilder builder = mBuilder.DefineType(name,
                TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed,
                typeof(object),
                new Type[] { serviceDef.Type });

            // private readonly TClient _client;
            FieldBuilder fbClient = builder.DefineField("_client", typeof(TClient),
                FieldAttributes.Private | FieldAttributes.InitOnly);

            // ctor
            // public XXXX(TClient client)
            ConstructorBuilder ctorBuilder = builder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                new Type[] { typeof(TClient) });

            // ctor public XXX(TClient client) { _client = client; }
            ILGenerator ctorIL = ctorBuilder.GetILGenerator();

            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Ldarg_1);
            ctorIL.Emit(OpCodes.Stfld, fbClient);
            ctorIL.Emit(OpCodes.Ret);

            foreach (MethodDefine method in serviceDef.Methods)
            {
                Type[] paramsType = method._methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
                MethodBuilder mb = builder.DefineMethod(method.Name,
                    MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    method._methodInfo.ReturnType,
                    paramsType);



                Type requestType = typeof(Request<,>).MakeGenericType(method.RpcParamType, method.RpcReturnType);
                ConstructorInfo requestCtor = requestType.GetConstructor(Array.Empty<Type>());
                ILGenerator methIL = mb.GetILGenerator();

                methIL.UsingNamespace("KupaRPC");

                LocalBuilder localBuilder = methIL.DeclareLocal(requestType);

                // Request<TArg, TReply> request = new Request<TArg, TReply>();
                methIL.Emit(OpCodes.Newobj, requestCtor);

                // set ServiceID
                methIL.Emit(OpCodes.Dup);
                methIL.Emit(OpCodes.Ldc_I4, serviceDef.ID);
                methIL.Emit(OpCodes.Stfld, requestType.GetField("ServiceID"));

                // set MethodID
                methIL.Emit(OpCodes.Dup);
                methIL.Emit(OpCodes.Ldc_I4, method.ID);
                methIL.Emit(OpCodes.Stfld, requestType.GetField("MethodID"));

                // set Arg
                methIL.Emit(OpCodes.Dup);
                methIL.Emit(OpCodes.Ldarg_1);
                methIL.Emit(OpCodes.Stfld, requestType.GetField("Arg"));

                // store request to local variable list
                methIL.Emit(OpCodes.Stloc_0);

                // prepare _client.Send<TArg, TReply>(request, context);
                methIL.Emit(OpCodes.Ldarg_0);
                methIL.Emit(OpCodes.Ldfld, fbClient);
                methIL.Emit(OpCodes.Ldloc_0);
                methIL.Emit(OpCodes.Ldarg_2);
                methIL.Emit(OpCodes.Call,
                    typeof(TClient).GetMethod("Send").MakeGenericMethod(method.RpcParamType, method.RpcReturnType));
                //methIL.Emit(OpCodes.Call, typeof(TClient).GetMethod("Blabla"));
                methIL.Emit(OpCodes.Ret);
            }
            return builder.CreateType();

        }
    }
}
