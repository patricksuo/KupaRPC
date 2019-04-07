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
        private readonly Codec _codec = new Codec(new Ceras.CerasSerializer());

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
            _codec.WriteReuqest(request, out ReadOnlyMemory<byte> tmpBuffer);

            RequestHead expectedHead = new RequestHead()
            {
                RequestID = request.ID,
                ServiceID = request.ServiceID,
                MethodID = request.MethodID,
                PayloadSize = tmpBuffer.Length - Codec.RequestHeadSize,
            };

            byte[] invalidBuffer1 = tmpBuffer.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(invalidBuffer1, -1);

            byte[] invalidBuffer2 = tmpBuffer.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(invalidBuffer2, Codec.MaxPayloadSize + 1);


            var testCases = new[]{
                new { Input = tmpBuffer.Slice(0,1).ToArray(), OK = false, Head = new RequestHead() , Exception = false},
                new { Input = tmpBuffer.Slice(0,Codec.RequestHeadSize-1).ToArray(), OK = false, Head = new RequestHead() ,Exception = false},
                new { Input = tmpBuffer.Slice(0, Codec.RequestHeadSize).ToArray(), OK = true, Head =expectedHead, Exception = false},
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
            ReponseHead reponseHead = new ReponseHead()
            {
                RequestID = 10086,
                ErrorCode = 0,
                PayloadSize = 10,
            };

            _codec.WriteReponseHead(reponseHead, out ReadOnlyMemory<byte> tmpBuffer);


            byte[] invalidBuffer1 = tmpBuffer.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(invalidBuffer1, -1);

            byte[] invalidBuffer2 = tmpBuffer.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(invalidBuffer2, Codec.MaxPayloadSize + 1);



            var testCases = new[]
            {
                new {Input = tmpBuffer.Slice(0,1).ToArray(), OK = false, Head = new ReponseHead(), Exception = false },
                new {Input = tmpBuffer.Slice(0,Codec.ReponseHeadSize-1).ToArray(), OK = false, Head = new ReponseHead(), Exception = false },
                new {Input = tmpBuffer.Slice(0,Codec.ReponseHeadSize).ToArray(), OK = true, Head = reponseHead, Exception = false },
                new {Input = invalidBuffer1, OK = false, Head =  new ReponseHead(), Exception = true },
                new {Input = invalidBuffer2, OK = false, Head =  new ReponseHead(), Exception = true },
            };

            foreach (var tc in testCases)
            {
                ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(tc.Input);
                ReponseHead head = new ReponseHead();
                int errorCode = 0;

                if (tc.Exception)
                {
                    Assert.ThrowsAny<CodecException>(() =>
                    {
                        _codec.TryReadReponseHead(sequence, ref head.PayloadSize, ref head.RequestID, ref errorCode);
                    });
                }
                else
                {
                    bool ok = _codec.TryReadReponseHead(sequence, ref head.PayloadSize, ref head.RequestID, ref errorCode);
                    head.ErrorCode = (ErrorCode)errorCode;

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
            _codec.WriteReuqest(request, out ReadOnlyMemory<byte> tmpBuffer);

            ReadOnlySequence<byte> packet = new ReadOnlySequence<byte>(tmpBuffer.ToArray());

            RequestHead head = new RequestHead();

            Assert.True(_codec.TryReadRequestHead(packet, ref head));
            Assert.Equal(request.ID, head.RequestID);
            Assert.Equal(request.ServiceID, head.ServiceID);
            Assert.Equal(request.MethodID, head.MethodID);
            Assert.Equal(packet.Length - Codec.RequestHeadSize, head.PayloadSize);

            string body = _codec.ReadBody<string>(packet.Slice(Codec.RequestHeadSize, head.PayloadSize));

            Assert.Equal(request.Arg, body);
        }

        [Fact]
        public void ReponseEnd2End()
        {
            string reponsePayload = "hello";
            long requestID = 1000;
            _codec.WriteReponse(requestID, reponsePayload, out ReadOnlyMemory<byte> tmpBuffer);

            ReadOnlySequence<byte> packet = new ReadOnlySequence<byte>(tmpBuffer.ToArray());

            ReponseHead head = new ReponseHead();
            int errorCode = 0;
            Assert.True(_codec.TryReadReponseHead(packet, ref head.PayloadSize, ref head.RequestID, ref errorCode));
            head.ErrorCode = (ErrorCode)errorCode;

            Assert.Equal(packet.Length - Codec.ReponseHeadSize, head.PayloadSize);
            Assert.Equal(requestID, head.RequestID);
            Assert.Equal(ErrorCode.OK, head.ErrorCode);

            string body = _codec.ReadBody<string>(packet.Slice(Codec.ReponseHeadSize, head.PayloadSize));
            Assert.Equal(reponsePayload, body);
        }
    }
}
