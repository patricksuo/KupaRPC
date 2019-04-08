using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.Buffers;
using System.Linq;
using System.Buffers.Binary;

namespace KupaRPC.Tests
{
    public class CodecTest
    {
        private readonly Codec _codec = new CerasCodec(new Ceras.CerasSerializer());

        [Fact]
        public void TryReadRequestHeadTest()
        {
            Request<string, string> request = new Request<string, string>
            {
                ID = 10086,
                MethodID = 100,
                ServiceID = 99,
                Arg = "hello"
            };
            _codec.WriteRequest(request.Arg, request.ID, request.ServiceID, request.MethodID, out ReadOnlyMemory<byte> tmpBuffer);

            RequestHead expectedHead = new RequestHead()
            {
                RequestID = request.ID,
                ServiceID = request.ServiceID,
                MethodID = request.MethodID,
                PayloadSize = tmpBuffer.Length - Protocol.RequestHeadSize,
            };

            byte[] invalidBuffer1 = tmpBuffer.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(invalidBuffer1, -1);

            byte[] invalidBuffer2 = tmpBuffer.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(invalidBuffer2, Protocol.MaxPayloadSize + 1);


            var testCases = new[]{
                new { Input = tmpBuffer.Slice(0,1).ToArray(), OK = false, Head = new RequestHead() , Exception = false},
                new { Input = tmpBuffer.Slice(0, Protocol.RequestHeadSize-1).ToArray(), OK = false, Head = new RequestHead() ,Exception = false},
                new { Input = tmpBuffer.Slice(0, Protocol.RequestHeadSize).ToArray(), OK = true, Head =expectedHead, Exception = false},
                new { Input = tmpBuffer.ToArray(), OK = true, Head =expectedHead, Exception = false},
                new { Input = invalidBuffer1, OK = false, Head =new RequestHead(), Exception = true},
                new { Input = invalidBuffer2, OK = false, Head =new RequestHead(), Exception = true},
            };

            foreach (var tc in testCases)
            {
                ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(tc.Input);

                RequestHead head = new RequestHead();

                if (tc.Exception)
                {
                    Assert.ThrowsAny<CodecException>(() =>
                    {
                        _codec.TryReadRequestHead(sequence, ref head);
                    });
                }
                else
                {
                    bool ok = _codec.TryReadRequestHead(sequence, ref head);

                    Assert.Equal(tc.OK, ok);
                    if (ok)
                    {
                        Assert.StrictEqual(tc.Head, head);
                    }
                }
            }
        }

        [Fact]
        public void TryReadReponseHeadTest()
        {
            const long requestID = 10086;

            _codec.WriteReponse("hello", requestID, out ReadOnlyMemory<byte> tmpBuffer);

            ReponseHead reponseHead = new ReponseHead()
            {
                RequestID = requestID,
                ErrorCode = ErrorCode.OK,
                PayloadSize = tmpBuffer.Length - Protocol.ReponseHeadSize,
            };

            byte[] invalidBuffer1 = tmpBuffer.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(invalidBuffer1, -1);

            byte[] invalidBuffer2 = tmpBuffer.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(invalidBuffer2, Protocol.MaxPayloadSize + 1);



            var testCases = new[]
            {
                new {Input = tmpBuffer.Slice(0,1).ToArray(), OK = false, Head = new ReponseHead(), Exception = false },
                new {Input = tmpBuffer.Slice(0,Protocol.ReponseHeadSize-1).ToArray(), OK = false, Head = new ReponseHead(), Exception = false },
                new {Input = tmpBuffer.Slice(0,Protocol.ReponseHeadSize).ToArray(), OK = true, Head = reponseHead, Exception = false },
                new {Input = invalidBuffer1, OK = false, Head =  new ReponseHead(), Exception = true },
                new {Input = invalidBuffer2, OK = false, Head =  new ReponseHead(), Exception = true },
            };

            foreach (var tc in testCases)
            {
                ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(tc.Input);
                ReponseHead head = new ReponseHead();

                if (tc.Exception)
                {
                    Assert.ThrowsAny<CodecException>(() =>
                    {
                        _codec.TryReadReponseHead(sequence, ref head);
                    });
                }
                else
                {
                    bool ok = _codec.TryReadReponseHead(sequence, ref head);

                    Assert.Equal(tc.OK, ok);
                    if (ok)
                    {
                        Assert.StrictEqual(tc.Head, head);
                    }
                }
            }
        }


        [Fact]
        public void RequestEnd2End()
        {
            Request<string, string> request = new Request<string, string>
            {
                ID = 10086,
                MethodID = 100,
                ServiceID = 99,
                Arg = "hello"
            };
            _codec.WriteRequest(request.Arg, request.ID, request.ServiceID, request.MethodID, out ReadOnlyMemory<byte> tmpBuffer);

            ReadOnlySequence<byte> packet = new ReadOnlySequence<byte>(tmpBuffer.ToArray());

            RequestHead head = new RequestHead();

            Assert.True(_codec.TryReadRequestHead(packet, ref head));
            Assert.Equal(request.ID, head.RequestID);
            Assert.Equal(request.ServiceID, head.ServiceID);
            Assert.Equal(request.MethodID, head.MethodID);
            Assert.Equal(packet.Length - Protocol.RequestHeadSize, head.PayloadSize);

            string body = _codec.ReadBody<string>(packet.Slice(Protocol.RequestHeadSize, head.PayloadSize));

            Assert.Equal(request.Arg, body);
        }

        [Fact]
        public void ReponseEnd2End()
        {
            string reponsePayload = "hello";
            long requestID = 1000;
            _codec.WriteReponse(reponsePayload, requestID, out ReadOnlyMemory<byte> tmpBuffer);

            ReadOnlySequence<byte> packet = new ReadOnlySequence<byte>(tmpBuffer.ToArray());

            ReponseHead head = new ReponseHead();
            Assert.True(_codec.TryReadReponseHead(packet, ref head));

            Assert.Equal(packet.Length - Protocol.ReponseHeadSize, head.PayloadSize);
            Assert.Equal(requestID, head.RequestID);
            Assert.Equal(ErrorCode.OK, head.ErrorCode);

            string body = _codec.ReadBody<string>(packet.Slice(Protocol.ReponseHeadSize, head.PayloadSize));
            Assert.Equal(reponsePayload, body);
        }
    }
}
