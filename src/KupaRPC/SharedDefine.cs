using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;


namespace KupaRPC
{
    public interface IPendingRequest
    {
        void OnResult(Codec codec, in ReadOnlySequence<byte> body);
        void OnError(Exception e);
    }

    public class Request<TArg, TReply> : TaskCompletionSource<TReply>, IPendingRequest
    {
        public long ID;
        public ushort ServiceID;
        public ushort MethodID;

        public TArg Arg;

        public Request() : base(TaskCreationOptions.RunContinuationsAsynchronously)
        {
        }

        public void OnError(Exception e)
        {
            TrySetException(e);
        }


        public void OnResult(Codec codec, in ReadOnlySequence<byte> body)
        {
            try
            {
                TReply result = codec.ReadBody<TReply>(body);
                TrySetResult(result);
            }
            catch (Exception e)
            {
                TrySetException(e);
            }
        }
    }

    public abstract class Handler
    {
        public readonly string Name;

        public Handler(string name)
        {
            Name = name;
        }

        public abstract object ReadArg(Codec codec,in ReadOnlySequence<byte> body);
        public abstract Task<object> Process(object arg, ServerContext context);
        public abstract void WriteReply(long requestID, Codec codec, object reply, out ReadOnlyMemory<byte> tmpBuffer);
    }

    public class Handler<TArg, TReply> : Handler
    {
        private readonly Func<TArg, IContext, Task<TReply>> _handler;

        public Handler(string name, Func<TArg, IContext, Task<TReply>> handler):base(name)
        {
            _handler = handler;
        }

        public override object ReadArg(Codec codec,in ReadOnlySequence<byte> body)
        {
            return codec.ReadBody<TArg>(in body);
        }


        public override async Task<object> Process(object arg, ServerContext context)
        {
            TReply reply = await _handler((TArg)arg, context);
            return reply;
        }

        public override void WriteReply(long requestID, Codec codec, object reply, out ReadOnlyMemory<byte> tmpBuffer)
        {
            codec.WriteReponse((TReply)reply, requestID, out tmpBuffer);
        }
    }

    public struct Reponse
    {
        public long ReqeustID;
        public int ErrorCode;
        public byte[] Payload;
    }
}
