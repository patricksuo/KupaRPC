using System;
using System.Collections.Generic;
using System.Text;
using Ceras;

namespace KupaRPC
{
    public enum ErrorCode : int
    {
        OK = 0,
        UnknowAPI = 1,
        ReadArgError = 2,
        ServerInternalError = 3,
    }

    public struct RequestHead
    {
        public int PayloadSize;
        public long RequestID;
        public ushort ServiceID;
        public ushort MethodID;
    }

    public struct ReponseHead
    {
        public int PayloadSize;
        public long RequestID;
        public ErrorCode ErrorCode;
    }

    public abstract class Protocol
    {
        //  payload size + request ID + Service ID + Method ID
        public const int RequestHeadSize = sizeof(uint) + sizeof(long) + sizeof(ushort) + sizeof(ushort);

        // payload size + request ID + error code
        public const int ReponseHeadSize = sizeof(uint) + sizeof(long) + sizeof(int);

        public const int MaxPayloadSize = 128 * 1024 * 1024; // 128 MB

        public abstract Codec NewCodec();
    }

    public class CerasProtocol : Protocol
    {
        private readonly IEnumerable<ServiceDefine> _serviceDefines;

        internal CerasProtocol(IEnumerable<ServiceDefine> serviceDefines)
        {
            _serviceDefines = serviceDefines;
        }

        public override Codec NewCodec()
        {
            SerializerConfig config = new SerializerConfig();
            foreach (ServiceDefine service in _serviceDefines)
            {
                foreach (MethodDefine method in service.Methods.Values)
                {
                    if (!config.KnownTypes.Contains(method.Parameter.ParameterType))
                    {
                        config.KnownTypes.Add(method.Parameter.ParameterType);
                    }
                    if (!config.KnownTypes.Contains(method.ReturnType))
                    {
                        config.KnownTypes.Add(method.ReturnType);
                    }
                }
            }

            CerasSerializer serializer = new CerasSerializer(config);
            Codec codec = new Codec(serializer);
            return codec;
        }


    }
}
