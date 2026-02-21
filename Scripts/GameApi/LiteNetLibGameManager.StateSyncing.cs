using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public partial class LiteNetLibGameManager
    {
        private const ushort MAX_UNRELIABLE_PACKET_SIZE = 1200;
        protected readonly List<LiteNetLibSyncElement> _updatingClientSyncElements = new List<LiteNetLibSyncElement>();
        protected readonly List<LiteNetLibSyncElement> _updatingServerSyncElements = new List<LiteNetLibSyncElement>();
        protected readonly NetDataWriter _gameStatesWriter = new NetDataWriter(true, 1024);
        protected readonly NetDataWriter _syncElementWriter = new NetDataWriter(true, 1024);
        protected readonly List<PendingRpcData> _pendingRpcs = new List<PendingRpcData>();
        protected float _latestClientBaseLineSyncTime = 0f;
        protected float _latestServerBaseLineSyncTime = 0f;

        protected virtual void HandleServerSyncStates(MessageHandlerData messageHandler)
        {
            if (IsServer)
                return;
            ReadGameStateFromServer(messageHandler.Reader);
        }

        protected virtual void HandleServerSyncElement(MessageHandlerData messageHandler)
        {
            if (IsServer)
                return;
            ReadSyncElement(messageHandler.Reader);
        }

        protected virtual void HandleClientSyncStates(MessageHandlerData messageHandler)
        {
            ReadGameStateFromClient(messageHandler.Reader);
        }

        protected virtual void HandleClientSyncElement(MessageHandlerData messageHandler)
        {
            ReadSyncElement(messageHandler.Reader);
        }

        private void WriteSyncElement(NetDataWriter writer, LiteNetLibSyncElement syncElement)
        {
            uint tick = Tick;
            writer.PutPackedUInt(tick);
            writer.PutPackedUInt(syncElement.ObjectId);
            writer.PutPackedInt(syncElement.ElementId);
            if (safeGameStatePacket)
            {
                // Reserve position for data length
                int posBeforeWriteDataLen = writer.Length;
                int dataLength = 0;
                writer.Put(dataLength);
                int posAfterWriteDataLen = writer.Length;
                // Write sync data
                syncElement.WriteSyncData(false, tick, false, writer);
                dataLength = writer.Length - posAfterWriteDataLen;
                // Put data length
                int posAfterWriteData = writer.Length;
                writer.SetPosition(posBeforeWriteDataLen);
                writer.Put(dataLength);
                writer.SetPosition(posAfterWriteData);
            }
            else
            {
                syncElement.WriteSyncData(false, tick, false, writer);
            }
        }

        private bool ReadSyncElement(NetDataReader reader)
        {
            uint tick = reader.GetPackedUInt();
            uint objectId = reader.GetPackedUInt();
            if (!Assets.TryGetSpawnedObject(objectId, out LiteNetLibIdentity identity))
                return false;
            int elementId = reader.GetPackedInt();
            if (safeGameStatePacket)
            {
                int dataLength = reader.GetInt();
                int positionBeforeRead = reader.Position;

                if (identity.TryGetSyncElement(elementId, out LiteNetLibSyncElement element))
                {
                    try
                    {
                        element.ReadSyncData(false, tick, false, reader);
                    }
                    catch
                    {
                        if (LogWarn) Logging.LogWarning(LogTag, $"Unable to read game state properly, sync element not found.");
                        reader.SetPosition(positionBeforeRead);
                        reader.SkipBytes(dataLength);
                    }
                }
                else
                {
                    if (LogWarn) Logging.LogWarning(LogTag, $"Unable to read game state properly, sync element not found.");
                    reader.SetPosition(positionBeforeRead);
                    reader.SkipBytes(dataLength);
                }
            }
            else
            {
                if (identity.TryGetSyncElement(elementId, out LiteNetLibSyncElement element))
                {
                    try
                    {
                        element.ReadSyncData(false, tick, false, reader);
                    }
                    catch
                    {
                        if (LogError) Logging.LogError(LogTag, $"Unable to read game state properly, sync element not found.");
                        return false;
                    }
                }
                else
                {
                    if (LogError) Logging.LogError(LogTag, $"Unable to read game state properly, sync element not found.");
                    return false;
                }
            }
            return true;
        }

        private int WriteGameStateFromServer(NetDataWriter writer, LiteNetLibPlayer player, Dictionary<uint, GameStateSyncData> syncingStatesByObjectIds)
        {
            uint tick = Tick;
            writer.PutPackedUInt(tick);
            // Reserve position for state length
            int posBeforeWriteStateCount = writer.Length;
            int stateCount = 0;
            writer.Put(stateCount);
            foreach (var syncingStatesByObjectId in syncingStatesByObjectIds)
            {
                uint objectId = syncingStatesByObjectId.Key;
                GameStateSyncData syncData = syncingStatesByObjectId.Value;
                if (syncData.StateType == GameStateSyncType.None)
                    continue;
                // Writer sync state
                switch (syncData.StateType)
                {
                    case GameStateSyncType.Spawn:
                        // NOTE: Temporary avoid null ref exception, will find cause of issues later
                        writer.Put((byte)GameStateSyncType.Spawn);
                        WriteSpawnGameState(writer, player, syncData, tick);
                        // TODO: Move this to somewhere else
                        if (player.ConnectionId == ClientConnectionId)
                        {
                            // Simulate object spawning if it is a host
                            syncData.Identity.OnServerSubscribingAdded();
                        }
                        ++stateCount;
                        break;
                    case GameStateSyncType.Destroy:
                        writer.Put((byte)GameStateSyncType.Destroy);
                        WriteDestroyGameState(writer, objectId, syncData.DestroyReasons);
                        // TODO: Move this to somewhere else
                        if (player.ConnectionId == ClientConnectionId)
                        {
                            // Simulate object destroying if it is a host
                            syncData.Identity.OnServerSubscribingRemoved();
                        }
                        ++stateCount;
                        break;
                    case GameStateSyncType.Data:
                        // NOTE: Temporary avoid null ref exception, will find cause of issues later
                        writer.Put((byte)GameStateSyncType.Data);
                        WriteSyncGameState(writer, objectId, syncData.SyncBaseLineElements, tick);
                        ++stateCount;
                        break;
                }
                // Reset syncing state, so next time it won't being synced
                syncData.Reset();
            }
            int posAfterWriteStates = writer.Length;
            writer.SetPosition(posBeforeWriteStateCount);
            writer.Put(stateCount);
            writer.SetPosition(posAfterWriteStates);
            return stateCount;
        }

        private int WriteGameStateFromClient(NetDataWriter writer, byte syncChannelId, Dictionary<uint, GameStateSyncData> syncingStatesByObjectIds)
        {
            uint tick = Tick;
            writer.PutPackedUInt(tick);
            // Reserve position for state length
            int posBeforeWriteStateCount = writer.Length;
            int stateCount = 0;
            writer.Put(stateCount);
            foreach (var syncingStatesByObjectId in syncingStatesByObjectIds)
            {
                uint objectId = syncingStatesByObjectId.Key;
                GameStateSyncData syncData = syncingStatesByObjectId.Value;
                if (syncData.StateType == GameStateSyncType.None)
                    continue;
                // Writer sync state
                switch (syncingStatesByObjectId.Value.StateType)
                {
                    case GameStateSyncType.Data:
                        WriteSyncGameState(writer, objectId, syncData.SyncBaseLineElements, tick);
                        ++stateCount;
                        break;
                }
                // Reset syncing state, so next time it won't being synced
                syncData.Reset();
            }
            int posAfterWriteStates = writer.Length;
            writer.SetPosition(posBeforeWriteStateCount);
            writer.Put(stateCount);
            writer.SetPosition(posAfterWriteStates);
            return stateCount;
        }

        private void ReadGameStateFromServer(NetDataReader reader)
        {
            uint tick = reader.GetPackedUInt();
            int stateCount = reader.GetInt();
            for (int i = 0; i < stateCount; ++i)
            {
                GameStateSyncType stateType = (GameStateSyncType)reader.GetByte();
                switch (stateType)
                {
                    case GameStateSyncType.Spawn:
                        if (!ReadSpawnGameState(reader, tick))
                            return;
                        break;
                    case GameStateSyncType.Destroy:
                        if (!ReadDestroyGameState(reader))
                            return;
                        break;
                    case GameStateSyncType.Data:
                        if (!ReadSyncGameState(reader, tick))
                            return;
                        break;
                }
            }
        }

        private void ReadGameStateFromClient(NetDataReader reader)
        {
            uint tick = reader.GetPackedUInt();
            int stateCount = reader.GetInt();
            for (int i = 0; i < stateCount; ++i)
            {
                if (!ReadSyncGameState(reader, tick))
                    return;
            }
        }

        private void WriteSpawnGameState(NetDataWriter writer, LiteNetLibPlayer player, GameStateSyncData syncData, uint tick)
        {
            LiteNetLibIdentity identity = syncData.Identity;
            writer.Put(identity.IsSceneObject);
            if (identity.IsSceneObject)
                writer.PutPackedInt(identity.HashSceneObjectId);
            else
                writer.PutPackedInt(identity.HashAssetId);
            writer.Put(identity.transform.position.x);
            writer.Put(identity.transform.position.y);
            writer.Put(identity.transform.position.z);
            writer.Put(identity.transform.eulerAngles.x);
            writer.Put(identity.transform.eulerAngles.y);
            writer.Put(identity.transform.eulerAngles.z);
            writer.PutPackedUInt(identity.ObjectId);
            writer.PutPackedLong(identity.ConnectionId);
            syncData.SyncBaseLineElements.Clear();
            foreach (LiteNetLibSyncElement syncElement in identity.SyncElements.Values)
            {
                if (!syncElement.CanSyncFromServer(player))
                    continue;
                syncData.SyncBaseLineElements.Add(syncElement);
            }
            WriteSyncElements(writer, syncData.SyncBaseLineElements, tick, true);
            syncData.SyncBaseLineElements.Clear();
        }

        private bool ReadSpawnGameState(NetDataReader reader, uint tick)
        {
            bool isSceneObject = reader.GetBool();
            int hashSceneObjectId = 0;
            int hashAssetId = 0;
            if (isSceneObject)
                hashSceneObjectId = reader.GetPackedInt();
            else
                hashAssetId = reader.GetPackedInt();
            float positionX = reader.GetFloat();
            float positionY = reader.GetFloat();
            float positionZ = reader.GetFloat();
            float angleX = reader.GetFloat();
            float angleY = reader.GetFloat();
            float angleZ = reader.GetFloat();
            uint objectId = reader.GetPackedUInt();
            long connectionId = reader.GetPackedLong();
            LiteNetLibIdentity identity;
            if (isSceneObject)
            {
                identity = Assets.NetworkSpawnScene(objectId, hashSceneObjectId,
                    new Vector3(positionX, positionY, positionZ),
                    Quaternion.Euler(angleX, angleY, angleZ),
                    connectionId);
            }
            else
            {
                identity = Assets.NetworkSpawn(hashAssetId,
                    new Vector3(positionX, positionY, positionZ),
                    Quaternion.Euler(angleX, angleY, angleZ),
                    objectId, connectionId);
            }
            if (ReadSyncElements(reader, identity, tick, true))
            {
                // Proceed pending RPCs
                PendingRpcData pendingRpc;
                for (int i = 0; i < _pendingRpcs.Count; ++i)
                {
                    pendingRpc = _pendingRpcs[i];
                    if (pendingRpc.info.objectId == objectId)
                    {
                        identity.ProcessRPC(pendingRpc.info, pendingRpc.reader, true);
                        _pendingRpcs.RemoveAt(i);
                        i--;
                    }
                }
                return true;
            }
            return false;
        }

        private void WriteSyncGameState(NetDataWriter writer, uint objectId, HashSet<LiteNetLibSyncElement> syncElements, uint tick)
        {
            writer.PutPackedUInt(objectId);
            WriteSyncElements(writer, syncElements, tick, false);
            syncElements.Clear();
        }

        private bool ReadSyncGameState(NetDataReader reader, uint tick)
        {
            uint objectId = reader.GetPackedUInt();
            if (!Assets.TryGetSpawnedObject(objectId, out LiteNetLibIdentity identity))
                return false;
            return ReadSyncElements(reader, identity, tick, false);
        }

        private void WriteDestroyGameState(NetDataWriter writer, uint objectId, byte destroyReasons)
        {
            writer.PutPackedUInt(objectId);
            writer.Put(destroyReasons);
        }

        private bool ReadDestroyGameState(NetDataReader reader)
        {
            uint objectId = reader.GetPackedUInt();
            byte destroyReasons = reader.GetByte();
            Assets.NetworkDestroy(objectId, destroyReasons);
            return true;
        }

        private void WriteSyncElements(NetDataWriter writer, ICollection<LiteNetLibSyncElement> elements, uint tick, bool initial)
        {
            writer.PutPackedInt(elements.Count);
            if (elements.Count == 0)
                return;
            foreach (var syncElement in elements)
            {
                // Write element info
                writer.PutPackedInt(syncElement.ElementId);
                if (safeGameStatePacket)
                {
                    // Reserve position for data length
                    int posBeforeWriteDataLen = writer.Length;
                    int dataLength = 0;
                    writer.Put(dataLength);
                    int posAfterWriteDataLen = writer.Length;
                    // Write sync data
                    syncElement.WriteSyncData(true, tick, initial, writer);
                    dataLength = writer.Length - posAfterWriteDataLen;
                    // Put data length
                    int posAfterWriteData = writer.Length;
                    writer.SetPosition(posBeforeWriteDataLen);
                    writer.Put(dataLength);
                    writer.SetPosition(posAfterWriteData);
                }
                else
                {
                    syncElement.WriteSyncData(true, tick, initial, writer);
                }
            }
        }

        private bool ReadSyncElements(NetDataReader reader, LiteNetLibIdentity identity, uint tick, bool initial)
        {
            int elementsCount = reader.GetPackedInt();
            if (elementsCount == 0)
                return true;
            for (int i = 0; i < elementsCount; ++i)
            {
                int elementId = reader.GetPackedInt();
                if (safeGameStatePacket)
                {
                    int dataLength = reader.GetInt();
                    int positionBeforeRead = reader.Position;

                    if (identity.TryGetSyncElement(elementId, out LiteNetLibSyncElement element))
                    {
                        try
                        {
                            element.ReadSyncData(true, tick, initial, reader);
                        }
                        catch
                        {
                            if (LogWarn) Logging.LogWarning(LogTag, $"Unable to read game state properly, sync element not found.");
                            reader.SetPosition(positionBeforeRead);
                            reader.SkipBytes(dataLength);
                        }
                    }
                    else
                    {
                        if (LogWarn) Logging.LogWarning(LogTag, $"Unable to read game state properly, sync element not found.");
                        reader.SetPosition(positionBeforeRead);
                        reader.SkipBytes(dataLength);
                    }
                }
                else
                {
                    if (identity.TryGetSyncElement(elementId, out LiteNetLibSyncElement element))
                    {
                        try
                        {
                            element.ReadSyncData(true, tick, initial, reader);
                        }
                        catch
                        {
                            if (LogError) Logging.LogError(LogTag, $"Unable to read game state properly, sync element not found.");
                            return false;
                        }
                    }
                    else
                    {
                        if (LogError) Logging.LogError(LogTag, $"Unable to read game state properly, sync element not found.");
                        return false;
                    }
                }
            }
            return true;
        }

        private void ProceedServerGameStateSync(uint tick)
        {
            LiteNetLibPlayer tempPlayer;

            float currentTime = Time.unscaledTime;
            bool syncBaseLine = currentTime - _latestServerBaseLineSyncTime > baseLineSyncInterval;
            _latestServerBaseLineSyncTime = currentTime;

            if (syncBaseLine && _updatingServerSyncElements.Count > 0)
            {
                // Filter which elements can be synced
                foreach (long connectionId in Server.ConnectionIds)
                {
                    if (!Players.TryGetValue(connectionId, out tempPlayer) || !tempPlayer.IsReady)
                        continue;

                    foreach (LiteNetLibSyncElement syncElement in _updatingServerSyncElements)
                    {
                        if (!syncElement.CanSyncFromServer(tempPlayer))
                            continue;
                        if (syncBaseLine || !syncElement.CanSyncDelta())
                            tempPlayer.SyncingStates.AppendDataSyncState(syncElement, true);
                    }
                }
            }

            foreach (long connectionId in Server.ConnectionIds)
            {
                if (!Players.TryGetValue(connectionId, out tempPlayer) || !tempPlayer.IsReady)
                    continue;
                SyncGameStateToClient(tempPlayer);
                SyncDeltaDataToClient(tempPlayer);
            }

            if (_updatingServerSyncElements.Count > 0)
            {
                if (!syncBaseLine)
                {

                }
                for (int i = _updatingServerSyncElements.Count - 1; i >= 0; --i)
                {
                    _updatingServerSyncElements[i].Synced(tick, syncBaseLine);
                }
            }
        }

        private void ProceedClientGameStateSync(uint tick)
        {
            // Client always sync baseline to server
            if (_updatingClientSyncElements.Count == 0)
                return;

            foreach (LiteNetLibSyncElement syncElement in _updatingClientSyncElements)
            {
                if (!syncElement.CanSyncFromOwnerClient())
                    continue;
                ClientSyncingStates.AppendDataSyncState(syncElement, true);
            }
            SyncGameStateToServer();

            for (int i = _updatingClientSyncElements.Count - 1; i >= 0; --i)
            {
                _updatingClientSyncElements[i].Synced(tick, true);
            }
        }

        private void SyncGameStateToClient(LiteNetLibPlayer player)
        {
            if (player.SyncingStates.States.Count == 0)
                return;
            foreach (var syncingStatesByChannelId in player.SyncingStates.States)
            {
                int statesCount = syncingStatesByChannelId.Value.Count;
                // No states to be synced, skip
                if (statesCount == 0)
                    continue;
                _gameStatesWriter.Reset();
                _gameStatesWriter.PutPackedUShort(GameMsgTypes.SyncBaseLine);
                byte syncChannelId = syncingStatesByChannelId.Key;
                int stateCount = WriteGameStateFromServer(_gameStatesWriter, player, syncingStatesByChannelId.Value);
                if (stateCount > 0)
                {
                    // Send data to client
                    ServerSendMessage(player.ConnectionId, syncChannelId, DeliveryMethod.ReliableOrdered, _gameStatesWriter);
                }
                syncingStatesByChannelId.Value.Clear();
            }
        }

        private void SyncDeltaDataToClient(LiteNetLibPlayer player)
        {
            if (player.SyncingStates.States.Count == 0)
                return;
            foreach (var syncingStatesByChannelId in player.SyncingStates.States)
            {
                int statesCount = syncingStatesByChannelId.Value.Count;
                // No states to be synced, skip
                if (statesCount == 0)
                    continue;
                _gameStatesWriter.Reset();
                _gameStatesWriter.PutPackedUShort(GameMsgTypes.SyncDelta);
                byte syncChannelId = syncingStatesByChannelId.Key;
                int stateCount = WriteGameStateFromServer(_gameStatesWriter, player, syncingStatesByChannelId.Value);
                if (stateCount > 0)
                {
                    // Send data to client
                    ServerSendMessage(player.ConnectionId, syncChannelId, DeliveryMethod.Unreliable, _gameStatesWriter);
                }
                syncingStatesByChannelId.Value.Clear();
            }
        }

        private void SyncGameStateToServer()
        {
            if (ClientSyncingStates.States.Count == 0)
                return;
            foreach (var syncingStatesByChannelId in ClientSyncingStates.States)
            {
                int statesCount = syncingStatesByChannelId.Value.Count;
                // No states to be synced, skip
                if (statesCount == 0)
                    continue;
                _gameStatesWriter.Reset();
                _gameStatesWriter.PutPackedUShort(GameMsgTypes.SyncBaseLine);
                byte syncChannelId = syncingStatesByChannelId.Key;
                int stateCount = WriteGameStateFromClient(_gameStatesWriter, syncChannelId, syncingStatesByChannelId.Value);
                if (stateCount > 0)
                {
                    // Send data to server
                    ClientSendMessage(syncChannelId, DeliveryMethod.ReliableOrdered, _gameStatesWriter);
                }
                syncingStatesByChannelId.Value.Clear();
            }
        }

        internal void RegisterServerSyncElement(LiteNetLibSyncElement element)
        {
            if (!_updatingServerSyncElements.Contains(element))
                _updatingServerSyncElements.Add(element);
        }

        internal void UnregisterServerSyncElement(LiteNetLibSyncElement element)
        {
            _updatingServerSyncElements.Remove(element);
        }

        internal void RegisterClientSyncElement(LiteNetLibSyncElement element)
        {
            if (!_updatingClientSyncElements.Contains(element))
                _updatingClientSyncElements.Add(element);
        }

        internal void UnregisterClientSyncElement(LiteNetLibSyncElement element)
        {
            _updatingClientSyncElements.Remove(element);
        }
    }
}
