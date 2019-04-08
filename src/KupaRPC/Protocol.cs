using System;
using System.Buffers;
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

    public abstract class Codec
    {
        public abstract bool TryReadRequestHead(in ReadOnlySequence<byte> buffer, ref RequestHead head);

        public abstract bool TryReadReponseHead(in ReadOnlySequence<byte> buffer, ref ReponseHead head);

        public abstract T ReadBody<T>(in ReadOnlySequence<byte> body);

        public abstract void WriteRequest<T>(T body, long requestID, ushort serviceID, ushort methodID, out ReadOnlyMemory<byte> tmpBuffer);

        public abstract void WriteReponse<T>(T body, long requestID, out ReadOnlyMemory<byte> tmpBuffer);
        public abstract void WriteErrorReponse(ErrorCode errorCode, long requestID, out ReadOnlyMemory<byte> tmpBuffer);
    }
}
