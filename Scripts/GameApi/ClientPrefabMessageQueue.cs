using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace LiteNetLibManager
{
    // Owns copied packet payloads while a client waits for a prefab's sync schema.
    internal sealed class ClientPrefabMessageQueue
    {
        private struct Entry
        {
            public int bytes;
            public Func<CancellationToken, UniTask> read;
        }

        private readonly Queue<Entry> entries = new Queue<Entry>();
        private readonly Action<Exception> failed;
        private CancellationTokenSource cancellation;
        private int version;
        private int count;
        internal const int MaxMessages = 4096;
        internal const int MaxBytes = 8 * 1024 * 1024;
        internal bool IsProcessing { get; private set; }
        internal int PendingBytes { get; private set; }

        internal ClientPrefabMessageQueue(Action<Exception> failed) { this.failed = failed; }

        internal void Enqueue(int bytes, Func<CancellationToken, UniTask> read)
        {
            if (bytes < 0 || bytes > MaxBytes || count >= MaxMessages || PendingBytes > MaxBytes - bytes)
            {
                Clear();
                failed(new InvalidOperationException("Client prefab loading message buffer limit reached."));
                return;
            }
            entries.Enqueue(new Entry { bytes = bytes, read = read });
            PendingBytes += bytes;
            ++count;
            if (IsProcessing) return;
            cancellation = new CancellationTokenSource();
            IsProcessing = true;
            Drain(version, cancellation.Token).Forget();
        }

        internal void Clear()
        {
            ++version;
            entries.Clear();
            PendingBytes = 0;
            count = 0;
            IsProcessing = false;
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
        }

        private async UniTask Drain(int generation, CancellationToken token)
        {
            try
            {
                int processed = 0;
                while (generation == version && entries.Count > 0)
                {
                    Entry entry = entries.Dequeue();
                    await entry.read(token);
                    if (generation != version) return;
                    PendingBytes -= entry.bytes;
                    --count;
                    if (++processed % 16 == 0 && entries.Count > 0)
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (generation == version)
                {
                    Clear();
                    failed(ex);
                }
            }
            finally
            {
                if (generation == version)
                {
                    IsProcessing = false;
                    cancellation?.Dispose();
                    cancellation = null;
                }
            }
        }
    }
}
