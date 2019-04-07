using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KupaRPC
{
    public class ServerClient : IDisposable
    {
        private readonly Server _server;
        private readonly IDuplexPipe _transport;
        private readonly PipeReader _input;
        private readonly PipeWriter _output;
        private readonly Codec _codec;
        private readonly SemaphoreSlim _sendMutex = new SemaphoreSlim(1);
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellation;

        public ServerClient(CancellationToken token, ILogger logger, Server server, IDuplexPipe transport, Codec codec)
        {
            _server = server;
            _transport = transport;
            _codec = codec;
            _input = _transport.Input;
            _output = _transport.Output;
            _logger = logger;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        }

        public async Task Serve()
        {

            RequestHead reqHead = new RequestHead();
            CancellationToken token = _cancellation.Token;

            try
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    ReadResult result = await _input.ReadAsync(token);
                    if (result.IsCompleted || result.IsCanceled)
                    {
                        return;
                    }
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    if (!_codec.TryReadRequestHead(in buffer, ref reqHead))
                    {
                        _input.AdvanceTo(buffer.Start, buffer.End);
                        continue;
                    }

                    if (buffer.Length < Protocol.RequestHeadSize + reqHead.PayloadSize)
                    {
                        _input.AdvanceTo(buffer.Start, buffer.End);
                        continue;
                    }

                    ReadOnlySequence<byte> body = buffer.Slice(Protocol.RequestHeadSize, reqHead.PayloadSize);
                    if (!_server.TryGetHandler(reqHead.ServiceID, reqHead.MethodID, out Handler handler))
                    {
                        _input.AdvanceTo(body.End);
                        _ = SendErrorHead(reqHead.RequestID, ErrorCode.UnknowAPI);
                        continue;
                    }

                    object arg = null;
                    try
                    {
                        arg = handler.ReadArg(_codec, body);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "ServeLoop: read arguments exception");
                        _ = SendErrorHead(reqHead.RequestID, ErrorCode.ReadArgError);
                        _input.AdvanceTo(body.End);
                        continue;
                    }

                    _ = ServeOneRequest(handler, reqHead.RequestID, arg);
                    _input.AdvanceTo(body.End);
                }
            }
            catch(OperationCanceledException)
            { }
            catch(Exception e)
            {
                _logger.LogError(e, "ServeLoop: exception");
            }
            finally
            {
                Stop();
            }
            
        }

        private async Task SendErrorHead(long requestID, ErrorCode errorCode)
        {
            ReponseHead reponseHeader = new ReponseHead()
            {
                RequestID = requestID,
                ErrorCode = errorCode,
            };
            await _sendMutex.WaitAsync();
            try
            {
                _codec.WriteReponseHead(in reponseHeader, out ReadOnlyMemory<byte> reponseBuffer);
                await _output.WriteAsync(reponseBuffer);
            }
            finally
            {
                _sendMutex.Release();
            }
        }

        private async Task ServeOneRequest(Handler handler, long requestID, object arg)
        {
            object reply;
            try
            {
                reply = await handler.Process(arg, ServerContext.Default);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "handler {0} Process exception. requestID is {1}", handler.Name, requestID);
                _ = SendErrorHead(requestID, ErrorCode.ServerInternalError);
                return;
            }

            await _sendMutex.WaitAsync();
            try
            {
                handler.WriteReply(requestID, _codec, reply, out ReadOnlyMemory<byte> tmpBuffer);
                await _output.WriteAsync(tmpBuffer);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "handler {0} Reply exception. requestID is {1}", handler.Name, requestID);
                Stop();
            }
            finally
            {
                _sendMutex.Release();
            }
        }

        private void Stop()
        {
            try
            {
                lock (_cancellation)
                {
                    if (!_cancellation.IsCancellationRequested)
                    {
                        _cancellation.Cancel();
                    }
                }
            }
            catch (Exception) { }
        }

        public void Dispose()
        {
            _cancellation.Dispose();
        }
    }


}
