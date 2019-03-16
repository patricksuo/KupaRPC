using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace KupaRPC
{
    public interface IContext
    {
    }

    public class ClientContext : IContext
    {
        internal CancellationToken _token = CancellationToken.None;
        internal Dictionary<string, string> _values;

        public static ClientContext Default { get; private set; } = new ClientContext();


        public ClientContext WithCancel(CancellationToken token)
        {
            _token = token;
            return this;
        }

        public ClientContext WithValue(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(nameof(key));
            }

            if (_values == null)
            {
                _values = new Dictionary<string, string>();
            }

            if (!_values.TryAdd(key, value))
            {
                _values[key] = value;
            }
            return this;
        }
    }

    public class ServerContext : IContext
    {
        public static ServerContext Default { get; private set; } = new ServerContext();

        public CancellationToken CancellationToken => CancellationToken.None;

        public bool TryGetValue(string key, out string value)
        {
            value = null;
            return false;
        }
    }
}
