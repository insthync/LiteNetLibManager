using System;
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

        internal void ClearClientPrefabMessages() { clientPrefabMessages?.Clear(); }

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
            clientPrefabMessages.Enqueue(bytes, token => ReadQueuedClientPrefabMessage(ownedMessage, handler, token));
            return true;
#else
            return false;
#endif
        }

#if !DISABLE_ADDRESSABLES
        private void FailClientPrefabLoading(Exception ex)
        {
            Debug.LogError($"[Client Prefabs] Unable to finish network prefab loading. Disconnecting to avoid an incomplete game state.\n{ex}");
            if (IsClient && !IsServer) StopClient();
        }

        private async UniTask ReadQueuedClientPrefabMessage(MessageHandlerData message, MessageHandlerDelegate handler, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
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
                        reader.SetPosition(start);
                        if (!sceneObject)
                            await Assets.EnsureClientPrefabAsync(hash, token).Timeout(TimeSpan.FromSeconds(30));
                        token.ThrowIfCancellationRequested();
                        if (!ReadSpawnGameState(reader, tick))
                            throw new InvalidOperationException("Unable to decode client spawn baseline.");
                        break;
                    case GameStateSyncType.Destroy:
                        if (!ReadDestroyGameState(reader))
                            throw new InvalidOperationException("Unable to decode client despawn baseline.");
                        break;
                    case GameStateSyncType.Data:
                        if (!ReadSyncGameState(reader, tick))
                            throw new InvalidOperationException("Unable to decode client sync baseline.");
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown client baseline state: {type}.");
                }
            }
        }
#endif
    }
}
