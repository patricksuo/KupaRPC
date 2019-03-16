using System;
using System.Collections.Generic;
using System.Text;

namespace KupaRPC
{
    public class CodecException:Exception
    {
        public CodecException(string message) : base(message) { }
    }

    public class ServerException : Exception
    {
        public readonly int ErrorCode;
        public ServerException(int code):base()
        {
            ErrorCode = code;
        }

        public override string Message => $"Server Error: {ErrorCode}";
    }

    public static class ThrowHelper
    {
        static readonly CodecException s_invalidBodySize = new CodecException("invalid body size");

        public static void ThrowInvalidBodySizeException()
        {
            throw s_invalidBodySize;
        }
    }
}
