using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibManager
{
    public partial class LiteNetLibGameManager
    {
        private ClientPrefabMessageQueue clientPrefabMessages;
        private bool replayingClientPrefabMessage;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int clientSpawnCount;
        private long clientSpawnTicks;
        private long clientMaxSpawnTicks;

        private void RecordClientSpawn(long started)
        {
            if (!IsClient || IsServer) return;
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
            ++clientSpawnCount;
            clientSpawnTicks += elapsed;
            clientMaxSpawnTicks = Math.Max(clientMaxSpawnTicks, elapsed);
            if (clientSpawnCount == 1 || elapsed * 1000d / System.Diagnostics.Stopwatch.Frequency >= 16)
                Debug.Log($"[Client Prefabs] Spawn {clientSpawnCount}: {elapsed * 1000d / System.Diagnostics.Stopwatch.Frequency:F1} ms, mode {(Assets.UsesClientPrefabLoading ? "on-demand" : "preload")}.");
        }
#endif
        private readonly HashSet<uint> canceledClientSpawns = new HashSet<uint>();
        private PendingClientSpawn pendingClientSpawn;

        private sealed class PendingClientSpawn
        {
            internal uint objectId;
            internal bool canDiscard = true;
            internal bool obsolete;
            internal CancellationTokenSource cancellation;
        }

        internal void ClearClientPrefabMessages()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (clientSpawnCount > 0)
                Debug.Log($"[Client Prefabs] Spawn session: {clientSpawnCount} spawns, total {clientSpawnTicks * 1000d / System.Diagnostics.Stopwatch.Frequency:F1} ms, maximum {clientMaxSpawnTicks * 1000d / System.Diagnostics.Stopwatch.Frequency:F1} ms.");
            clientSpawnCount = 0;
            clientSpawnTicks = clientMaxSpawnTicks = 0;
#endif
            clientPrefabMessages?.Clear();
            canceledClientSpawns.Clear();
        }

        public override void RegisterClientMessage(ushort type, MessageHandlerDelegate handler)
        {
            base.RegisterClientMessage(type, message =>
            {
                if (!CanQueueDuringClientPrefabLoad(type) || !TryQueueClientPrefabMessage(message, handler))
                    handler(message);
            });
        }

        protected virtual bool CanQueueDuringClientPrefabLoad(ushort type)
        {
            return type != GameMsgTypes.Ping && type != GameMsgTypes.Pong &&
                type != GameMsgTypes.ServerSceneChange && type != GameMsgTypes.Disconnect && type != GameMsgTypes.ServerError;
        }

        private bool TryQueueClientPrefabMessage(MessageHandlerData message, MessageHandlerDelegate handler = null)
        {
#if !DISABLE_ADDRESSABLES
            if (replayingClientPrefabMessage || Assets == null || !Assets.UsesClientPrefabLoading)
                return false;
            bool baseline = message.MessageType == GameMsgTypes.SyncBaseLine;
            if (baseline && (clientPrefabMessages == null || !clientPrefabMessages.IsProcessing) &&
                CanReadClientBaselineImmediately(message.Reader)) return false;
            if (!baseline && (clientPrefabMessages == null || !clientPrefabMessages.IsProcessing))
                return false;
            if (clientPrefabMessages == null)
                clientPrefabMessages = new ClientPrefabMessageQueue(FailClientPrefabLoading);
            int bytes = message.Reader.AvailableBytes;
            if (bytes > ClientPrefabMessageQueue.MaxBytes)
            {
                FailClientPrefabLoading(new InvalidOperationException("Client prefab loading packet exceeds the message buffer limit."));
                return true;
            }
            // Transport readers are recycled immediately after the receive callback returns.
            var payload = new byte[bytes];
            Buffer.BlockCopy(message.Reader.RawData, message.Reader.Position, payload, 0, bytes);
            var ownedMessage = new MessageHandlerData(message.MessageType, message.TransportHandler,
                message.ConnectionId, new NetDataReader(payload));
            var obsolete = baseline ? ObserveClientDespawn(ownedMessage.Reader) : null;
            clientPrefabMessages.Enqueue(bytes, token => ReadQueuedClientPrefabMessage(ownedMessage, handler, token));
            if (obsolete != null && ReferenceEquals(pendingClientSpawn, obsolete))
            {
                obsolete.obsolete = true;
                obsolete.cancellation.Cancel();
            }
            return true;
#else
            return false;
#endif
        }

#if !DISABLE_ADDRESSABLES
        private bool CanReadClientBaselineImmediately(NetDataReader reader)
        {
            int position = reader.Position;
            try
            {
                reader.GetPackedUInt();
                if (reader.GetUShort() != 1) return false;
                var type = (GameStateSyncType)reader.GetByte();
                if (type == GameStateSyncType.Data || type == GameStateSyncType.Destroy) return true;
                if (type != GameStateSyncType.Spawn) return false;
                bool sceneObject = reader.GetBool();
                int hash = reader.GetPackedInt();
                return sceneObject || Assets.GuidToPrefabs.TryGetValue(hash, out var prefab) && prefab != null;
            }
            catch (Exception) { return false; }
            finally { reader.SetPosition(position); }
        }

        private PendingClientSpawn ObserveClientDespawn(NetDataReader reader)
        {
            var pending = pendingClientSpawn;
            if (pending == null) return null;
            int position = reader.Position;
            bool destroysPending = false;
            try
            {
                reader.GetPackedUInt();
                ushort count = reader.GetUShort();
                for (int i = 0; i < count; ++i)
                {
                    if ((GameStateSyncType)reader.GetByte() != GameStateSyncType.Destroy)
                    {
                        // Mixed baselines have no per-record lengths. Preserve decoding in arrival order.
                        if (count > 1) pending.canDiscard = false;
                        return null;
                    }
                    uint id = reader.GetPackedUInt();
                    reader.GetByte();
                    destroysPending |= id == pending.objectId;
                }
                if (destroysPending && pending.canDiscard)
                {
                    canceledClientSpawns.Add(pending.objectId);
                    for (int i = _pendingRpcs.Count - 1; i >= 0; --i)
                        if (_pendingRpcs[i].info.objectId == pending.objectId) RemovePendingRpcAt(i);
                    return pending;
                }
            }
            catch (Exception) { pending.canDiscard = false; } // The ordered decoder reports malformed packets.
            finally { reader.SetPosition(position); }
            return null;
        }

        private bool IsCanceledClientObjectMessage(MessageHandlerData message)
        {
            if (canceledClientSpawns.Count == 0) return false;
            int position = message.Reader.Position;
            try
            {
                if (message.MessageType == GameMsgTypes.RPC)
                    return canceledClientSpawns.Contains(LiteNetLibElementInfo.DeserializeInfo(message.Reader).objectId);
                if (message.MessageType == GameMsgTypes.ServerSetObjectOwner)
                    return canceledClientSpawns.Contains(message.ReadMessage<ServerSetObjectOwner>().objectId);
            }
            finally { message.Reader.SetPosition(position); }
            return false;
        }

        private void FailClientPrefabLoading(Exception ex)
        {
            Debug.LogError($"[Client Prefabs] Unable to finish network prefab loading. Disconnecting to avoid an incomplete game state.\n{ex}");
            if (IsClient && !IsServer) StopClient();
        }

        private async UniTask ReadQueuedClientPrefabMessage(MessageHandlerData message, MessageHandlerDelegate handler, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (IsCanceledClientObjectMessage(message)) return;
            if (message.MessageType == GameMsgTypes.SyncBaseLine)
            {
                await ReadGameStateWithClientPrefabs(message.Reader, token);
                return;
            }
            replayingClientPrefabMessage = true;
            try
            {
                if (handler != null) handler(message);
                else if (message.MessageType == GameMsgTypes.SyncDelta) HandleServerSyncDelta(message);
                else if (message.MessageType == GameMsgTypes.RPC) HandleServerCallFunction(message);
                else if (message.MessageType == GameMsgTypes.ServerSetObjectOwner) HandleServerSetObjectOwner(message);
            }
            finally { replayingClientPrefabMessage = false; }
        }

        private async UniTask ReadGameStateWithClientPrefabs(NetDataReader reader, CancellationToken token)
        {
            uint tick = reader.GetPackedUInt();
            ushort count = reader.GetUShort();
            if (count == 1 && canceledClientSpawns.Count > 0)
            {
                int position = reader.Position;
                if ((GameStateSyncType)reader.GetByte() == GameStateSyncType.Data &&
                    canceledClientSpawns.Contains(reader.GetPackedUInt())) return;
                reader.SetPosition(position);
            }
            long batchStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            int batchRecords = 0;
            for (int i = 0; i < count; ++i)
            {
                token.ThrowIfCancellationRequested();
                var type = (GameStateSyncType)reader.GetByte();
                switch (type)
                {
                    case GameStateSyncType.Spawn:
                        int start = reader.Position;
                        bool sceneObject = reader.GetBool();
                        int hash = reader.GetPackedInt();
                        bool needsLoad = !sceneObject && (!Assets.GuidToPrefabs.TryGetValue(hash, out var prefab) || prefab == null);
                        uint objectId = 0;
                        if (needsLoad)
                        {
                            for (int axis = 0; axis < 6; ++axis) reader.GetFloat();
                            objectId = reader.GetPackedUInt();
                        }
                        reader.SetPosition(start);
                        if (needsLoad)
                        {
                            var pending = count == 1 ? new PendingClientSpawn
                            {
                                objectId = objectId,
                                cancellation = CancellationTokenSource.CreateLinkedTokenSource(token),
                            } : null;
                            pendingClientSpawn = pending;
                            try
                            {
                                await Assets.EnsureClientPrefabAsync(hash, pending?.cancellation.Token ?? token)
                                    .Timeout(TimeSpan.FromSeconds(30), delayType: DelayType.Realtime);
                            }
                            catch (OperationCanceledException) when (!token.IsCancellationRequested && pending != null && pending.obsolete)
                            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                Debug.Log($"[Client Prefabs] Canceled obsolete spawn {objectId} before instantiation.");
#endif
                                return;
                            }
                            finally
                            {
                                if (ReferenceEquals(pendingClientSpawn, pending)) pendingClientSpawn = null;
                                pending?.cancellation.Dispose();
                            }
                        }
                        token.ThrowIfCancellationRequested();
                        if (!ReadSpawnGameState(reader, tick))
                            throw new InvalidOperationException("Unable to decode client spawn baseline.");
                        break;
                    case GameStateSyncType.Destroy:
                        int destroyStart = reader.Position;
                        uint destroyedId = reader.GetPackedUInt();
                        if (canceledClientSpawns.Contains(destroyedId) && !Assets.SpawnedObjects.ContainsKey(destroyedId))
                            reader.GetByte(); // No instance was created for this obsolete spawn.
                        else
                        {
                            reader.SetPosition(destroyStart);
                            if (!ReadDestroyGameState(reader))
                                throw new InvalidOperationException("Unable to decode client despawn baseline.");
                        }
                        canceledClientSpawns.Remove(destroyedId);
                        Assets.ChangingOwnerObjects.Remove(destroyedId);
                        break;
                    case GameStateSyncType.Data:
                        if (!ReadSyncGameState(reader, tick))
                            throw new InvalidOperationException("Unable to decode client sync baseline.");
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown client baseline state: {type}.");
                }
                // Large spawn batches must not monopolize a client frame. Packet order is preserved by the queue.
                if (i + 1 < count && (++batchRecords >= 16 ||
                    (System.Diagnostics.Stopwatch.GetTimestamp() - batchStarted) * 1000d / System.Diagnostics.Stopwatch.Frequency >= 4))
                {
                    await UniTask.DelayFrame(1, PlayerLoopTiming.Update, token);
                    batchStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    batchRecords = 0;
                }
            }
        }
#endif
    }
}
