using LiteNetLib.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public partial class LiteNetLibGameManager
    {
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
                if (syncingStatesByObjectId.Value.StateType == GameStateSyncData.STATE_TYPE_NONE)
                    continue;
                // Writer sync state
                uint objectId = syncingStatesByObjectId.Key;
                LiteNetLibIdentity identity;
                switch (syncingStatesByObjectId.Value.StateType)
                {
                    case GameStateSyncData.STATE_TYPE_SPAWN:
                        // NOTE: Temporary avoid null ref exception, will find cause of issues later
                        if (Assets.TryGetSpawnedObject(objectId, out identity) && identity != null && identity.transform != null)
                        {
                            writer.Put(GameStateSyncData.STATE_TYPE_SPAWN);
                            WriteSpawnGameState(writer, player, identity, syncingStatesByObjectId.Value, tick);
                            // TODO: Move this to somewhere else
                            if (player.ConnectionId == ClientConnectionId)
                            {
                                // Simulate object spawning if it is a host
                                identity.OnServerSubscribingAdded();
                            }
                            ++stateCount;
                        }
                        break;
                    case GameStateSyncData.STATE_TYPE_SYNC:
                        // NOTE: Temporary avoid null ref exception, will find cause of issues later
                        if (Assets.TryGetSpawnedObject(objectId, out identity) && identity != null && identity.transform != null)
                        {
                            writer.Put(GameStateSyncData.STATE_TYPE_SYNC);
                            WriteSyncGameState(writer, objectId, syncingStatesByObjectId.Value, tick);
                            ++stateCount;
                        }
                        break;
                    case GameStateSyncData.STATE_TYPE_DESTROY:
                        writer.Put(GameStateSyncData.STATE_TYPE_DESTROY);
                        WriteDestroyGameState(writer, objectId, syncingStatesByObjectId.Value);
                        // TODO: Move this to somewhere else
                        if (player.ConnectionId == ClientConnectionId)
                        {
                            // Simulate object destroying if it is a host
                            // NOTE: Temporary avoid null ref exception, will find cause of issues later
                            if (Assets.TryGetSpawnedObject(objectId, out identity) && identity != null && identity.transform != null)
                                identity.OnServerSubscribingRemoved();
                        }
                        ++stateCount;
                        break;
                }
                // Reset syncing state, so next time it won't being synced
                syncingStatesByObjectId.Value.Reset();
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
                if (syncingStatesByObjectId.Value.StateType == GameStateSyncData.STATE_TYPE_NONE)
                    continue;
                // Writer sync state
                uint objectId = syncingStatesByObjectId.Key;
                switch (syncingStatesByObjectId.Value.StateType)
                {
                    case GameStateSyncData.STATE_TYPE_SYNC:
                        WriteSyncGameState(writer, objectId, syncingStatesByObjectId.Value, tick);
                        ++stateCount;
                        break;
                }
                // Reset syncing state, so next time it won't being synced
                syncingStatesByObjectId.Value.Reset();
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
                byte stateType = reader.GetByte();
                switch (stateType)
                {
                    case GameStateSyncData.STATE_TYPE_SPAWN:
                        if (!ReadSpawnGameState(reader, tick))
                            return;
                        break;
                    case GameStateSyncData.STATE_TYPE_SYNC:
                        if (!ReadSyncGameState(reader, tick))
                            return;
                        break;
                    case GameStateSyncData.STATE_TYPE_DESTROY:
                        if (!ReadDestroyGameState(reader))
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

        private void WriteSpawnGameState(NetDataWriter writer, LiteNetLibPlayer player, LiteNetLibIdentity identity, GameStateSyncData syncData, uint tick)
        {
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
            syncData.SyncElements.Clear();
            foreach (LiteNetLibSyncElement syncElement in identity.SyncElements.Values)
            {
                if (!syncElement.CanSyncFromServer(player))
                    continue;
                syncData.SyncElements.Add(syncElement);
            }
            WriteSyncElements(writer, syncData.SyncElements, tick, true);
            syncData.SyncElements.Clear();
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

        private void WriteSyncGameState(NetDataWriter writer, uint objectId, GameStateSyncData syncData, uint tick)
        {
            writer.PutPackedUInt(objectId);
            WriteSyncElements(writer, syncData.SyncElements, tick, false);
            syncData.SyncElements.Clear();
        }

        private bool ReadSyncGameState(NetDataReader reader, uint tick)
        {
            uint objectId = reader.GetPackedUInt();
            if (!Assets.TryGetSpawnedObject(objectId, out LiteNetLibIdentity identity))
                return false;
            return ReadSyncElements(reader, identity, tick, false);
        }

        private void WriteDestroyGameState(NetDataWriter writer, uint objectId, GameStateSyncData syncData)
        {
            writer.PutPackedUInt(objectId);
            writer.Put(syncData.DestroyReasons);
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
    }
}
