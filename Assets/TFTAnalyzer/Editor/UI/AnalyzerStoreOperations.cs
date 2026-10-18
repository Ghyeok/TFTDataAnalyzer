using System;
using System.Threading;
using System.Threading.Tasks;

namespace TFTAnalyzer.Editor.UI
{
    internal sealed class AnalyzerStoreOperations
    {
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        // Shared across windows and bindings; closing one view cannot release an active operation.
        internal static AnalyzerStoreOperations Shared { get; } = new AnalyzerStoreOperations();
        internal async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken token)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            await _gate.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                return await operation();
            }
            finally { _gate.Release(); }
        }
    }
}
