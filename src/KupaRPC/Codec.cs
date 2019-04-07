using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Ceras;

namespace KupaRPC
{
    public class Codec
    {
        private readonly CerasSerializer _serializer;

        public Codec(CerasSerializer serializer)
        {
            _serializer = serializer;
        }

        internal static Codec New(IEnumerable<ServiceDefine> serviceDefines)
        {
            SerializerConfig config = new SerializerConfig();
            foreach (ServiceDefine service in serviceDefines)
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

            Ceras.CerasSerializer serializer = new Ceras.CerasSerializer(config);
            Codec codec = new Codec(serializer);
            return codec;
        }

        private byte[] _readBuffer = new byte[Protocol.RequestHeadSize];
        private byte[] _writeBuffer = new byte[Protocol.RequestHeadSize + 128];

        public bool TryReadRequestHead(in ReadOnlySequence<byte> buffer, ref RequestHead head)
        {
            if (buffer.Length < Protocol.RequestHeadSize)
            {
                return false;
            }

            buffer.Slice(0, Protocol.RequestHeadSize).CopyTo(_readBuffer);

            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(_readBuffer);
            head.PayloadSize = BinaryPrimitives.ReadInt32LittleEndian(span);
            if (head.PayloadSize < 0 || head.PayloadSize > Protocol.MaxPayloadSize)
            {
                ThrowHelper.ThrowInvalidBodySizeException();
            }
            span = span.Slice(sizeof(int));

            head.RequestID = BinaryPrimitives.ReadInt64LittleEndian(span);
            span = span.Slice(sizeof(long));

            head.ServiceID = BinaryPrimitives.ReadUInt16LittleEndian(span);
            span = span.Slice(sizeof(ushort));

            head.MethodID = BinaryPrimitives.ReadUInt16LittleEndian(span);
            return true;
        }

        public bool TryReadReponseHead(in ReadOnlySequence<byte> buffer, ref int payloadSize, ref long requestID, ref int errorCode)
        {
            if (buffer.Length < Protocol.ReponseHeadSize)
            {
                return false;
            }


            buffer.Slice(0, Protocol.ReponseHeadSize).CopyTo(_readBuffer);

            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(_readBuffer);
            payloadSize = BinaryPrimitives.ReadInt32LittleEndian(span);
            if (payloadSize < 0 || payloadSize > Protocol.MaxPayloadSize)
            {
                ThrowHelper.ThrowInvalidBodySizeException();
            }
            span = span.Slice(sizeof(int));

            requestID = BinaryPrimitives.ReadInt64LittleEndian(span);
            span = span.Slice(sizeof(long));

            errorCode = BinaryPrimitives.ReadInt32LittleEndian(span);

            return true;
        }

        public T ReadBody<T>(in ReadOnlySequence<byte> body)
        {
            if (body.IsSingleSegment && MemoryMarshal.TryGetArray(body.First, out ArraySegment<byte> segment))
            {
            }
            else if (body.Length <= _readBuffer.Length)
            {
                body.CopyTo(_readBuffer);
                segment = new ArraySegment<byte>(_readBuffer, 0, (int)body.Length);
            }
            else
            {
                _readBuffer = body.ToArray();
                segment = new ArraySegment<byte>(_readBuffer, 0, (int)body.Length);
            }

            int offset = segment.Offset;
            T val = default;
            _serializer.Deserialize(ref val, segment.Array, ref offset);
            return val;
        }

        public void WriteReuqest<TArg, TReply>(Request<TArg, TReply> request, out ReadOnlyMemory<byte> tmpBuffer)
        {
            // write body
            int size = _serializer.Serialize(request.Arg, ref _writeBuffer, Protocol.RequestHeadSize);
            if (size < 0 || size > Protocol.MaxPayloadSize)
            {
                ThrowHelper.ThrowInvalidBodySizeException();
            }

            Span<byte> span = _writeBuffer;
            BinaryPrimitives.WriteInt32LittleEndian(span, size);
            span = span.Slice(sizeof(int));

            BinaryPrimitives.WriteInt64LittleEndian(span, request.ID);
            span = span.Slice(sizeof(long));

            BinaryPrimitives.WriteUInt16LittleEndian(span, request.ServiceID);
            span = span.Slice(sizeof(ushort));

            BinaryPrimitives.WriteUInt16LittleEndian(span, request.MethodID);

            tmpBuffer = new ReadOnlyMemory<byte>(_writeBuffer, 0, Protocol.RequestHeadSize + size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteReponseHead(int payloadSize, long requestID, ErrorCode errorCode)
        {
            Span<byte> span = _writeBuffer;
            BinaryPrimitives.WriteInt32LittleEndian(span, payloadSize);
            span = span.Slice(sizeof(int));

            BinaryPrimitives.WriteInt64LittleEndian(span, requestID);
            span = span.Slice(sizeof(long));

            BinaryPrimitives.WriteInt32LittleEndian(span, (int)errorCode);
        }

        public void WriteReponseHead(in ReponseHead head, out ReadOnlyMemory<byte> tmpBuffer)
        {
            WriteReponseHead(head.PayloadSize, head.RequestID, head.ErrorCode);
            tmpBuffer = new ReadOnlyMemory<byte>(_writeBuffer, 0, Protocol.ReponseHeadSize);
        }

        public void WriteReponse<T>(long requestID, T reponse, out ReadOnlyMemory<byte> tmpBuffer)
        {
            int size = _serializer.Serialize(reponse, ref _writeBuffer, Protocol.ReponseHeadSize);

            if (size < 0 || size > Protocol.MaxPayloadSize)
            {
                ThrowHelper.ThrowInvalidBodySizeException();
            }

            WriteReponseHead(size, requestID, ErrorCode.OK);

            tmpBuffer = new ReadOnlyMemory<byte>(_writeBuffer, 0, size + Protocol.ReponseHeadSize);
        }
    }
}
