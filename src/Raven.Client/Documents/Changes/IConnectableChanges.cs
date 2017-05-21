using System;
using System.Threading.Tasks;

namespace Raven.Client.Documents.Changes
{
    public interface IConnectableChanges<TChanges> : IDisposable
        where TChanges : IDatabaseChanges
    {
        bool Connected { get; }

        Task<TChanges> EnsureConnectedNow();

        event EventHandler ConnectionStatusChanged;

        event Action<Exception> OnError;
    }
}
