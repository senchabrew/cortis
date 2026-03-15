using System;

namespace Cortis
{
    public sealed class Binder : IDisposable
    {
        readonly IDisposable[] _subscriptions;

        public Binder(params IDisposable[] subscriptions)
        {
            _subscriptions = subscriptions;
        }

        public void Dispose()
        {
            foreach (var sub in _subscriptions)
                sub?.Dispose();
        }
    }
}
