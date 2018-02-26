using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;

namespace LiteNetLibHighLevel
{
    [RequireComponent(typeof(LiteNetLibAssets))]
    public class LiteNetLibGameManager : LiteNetLibManager
    {
        public class GameMsgTypes
        {
            public const short ClientReady = 1;
            public const short ClientCallFunction = 2;
            public const short ServerSpawnSceneObject = 3;
            public const short ServerSpawnObject = 4;
            public const short ServerDestroyObject = 5;
            public const short ServerUpdateSyncField = 6;
            public const short ServerCallFunction = 7;
            public const short ServerUpdateSyncList = 8;
            public const short ServerUpdateTime = 9;
            public const short ServerSyncBehaviour = 10;
            public const short Highest = 10;
        }

        internal readonly Dictionary<long, LiteNetLibPlayer> Players = new Dictionary<long, LiteNetLibPlayer>();

        public float ServerTimeOffset { get; protected set; }
        public float ServerTime
        {
            get
            {
                if (IsServer)
                    return Time.unscaledTime;
                return Time.unscaledTime + ServerTimeOffset;
            }
        }

        private LiteNetLibAssets assets;
        public LiteNetLibAssets Assets
        {
            get
            {
                if (assets == null)
                    assets = GetComponent<LiteNetLibAssets>();
                return assets;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            Players.Clear();
            Assets.ClearRegisteredPrefabs();
            Assets.RegisterPrefabs();
            Assets.RegisterSceneObjects();
        }

        protected override void Update()
        {
            var spawnedObjects = Assets.SpawnedObjects.Values;
            foreach (var spawnedObject in spawnedObjects)
            {
                spawnedObject.NetworkUpdate();
            }
            base.Update();
        }

        public override bool StartServer()
        {
            if (base.StartServer())
            {
                Assets.SpawnSceneObjects();
                return true;
            }
            return false;
        }

        protected override void RegisterServerMessages()
        {
            base.RegisterServerMessages();
            RegisterServerMessage(GameMsgTypes.ClientReady, HandleClientReady);
            RegisterServerMessage(GameMsgTypes.ClientCallFunction, HandleClientCallFunction);
        }

        protected override void RegisterClientMessages()
        {
            base.RegisterClientMessages();
            RegisterClientMessage(GameMsgTypes.ServerSpawnSceneObject, HandleServerSpawnSceneObject);
            RegisterClientMessage(GameMsgTypes.ServerSpawnObject, HandleServerSpawnObject);
            RegisterClientMessage(GameMsgTypes.ServerDestroyObject, HandleServerDestroyObject);
            RegisterClientMessage(GameMsgTypes.ServerUpdateSyncField, HandleServerUpdateSyncField);
            RegisterClientMessage(GameMsgTypes.ServerCallFunction, HandleServerCallFunction);
            RegisterClientMessage(GameMsgTypes.ServerUpdateSyncList, HandleServerUpdateSyncList);
            RegisterClientMessage(GameMsgTypes.ServerUpdateTime, HandleServerUpdateTime);
            RegisterClientMessage(GameMsgTypes.ServerSyncBehaviour, HandleServerSyncBehaviour);
        }

        public override void OnPeerConnected(NetPeer peer)
        {
            base.OnPeerConnected(peer);
            SendServerUpdateTime(peer);
            Players[peer.ConnectId] = new LiteNetLibPlayer(this, peer);
        }

        public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            base.OnPeerDisconnected(peer, disconnectInfo);
            Players[peer.ConnectId].DestoryAllObjects();
            Players.Remove(peer.ConnectId);
        }

        public override void OnClientConnected(NetPeer peer)
        {
            base.OnClientConnected(peer);
            SendClientReady();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            Assets.ClearSpawnedObjects();
            LiteNetLibIdentity.ResetObjectId();
            LiteNetLibAssets.ResetSpawnPositionCounter();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            Assets.ClearSpawnedObjects();
            LiteNetLibIdentity.ResetObjectId();
            LiteNetLibAssets.ResetSpawnPositionCounter();
        }

#region Send messages functions
        internal void SendClientReady()
        {
            if (!IsClientConnected)
                return;
            SendPacket(SendOptions.ReliableOrdered, Client.Peer, GameMsgTypes.ClientReady);
        }

        internal void SendServerUpdateTime()
        {
            if (!IsServer)
                return;
            foreach (var peer in Peers.Values)
            {
                SendServerUpdateTime(peer);
            }
        }

        internal void SendServerUpdateTime(NetPeer peer)
        {
            SendPacket(SendOptions.Sequenced, peer, GameMsgTypes.ServerUpdateTime, (writer) =>
            {
                writer.Put(ServerTime);
            });
        }

        internal void SendServerSpawnSceneObject(LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return;
            foreach (var peer in Peers.Values)
            {
                SendServerSpawnSceneObject(peer, identity);
            }
        }

        internal void SendServerSpawnSceneObject(NetPeer peer, LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return;
            var message = new ServerSpawnSceneObjectMessage();
            message.objectId = identity.ObjectId;
            message.position = identity.transform.position;
            SendPacket(SendOptions.ReliableOrdered, peer, GameMsgTypes.ServerSpawnSceneObject, message);
        }

        internal void SendServerSpawnObject(LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return;
            foreach (var peer in Peers.Values)
            {
                SendServerSpawnObject(peer, identity);
            }
        }

        internal void SendServerSpawnObject(NetPeer peer, LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return;
            var message = new ServerSpawnObjectMessage();
            message.assetId = identity.AssetId;
            message.objectId = identity.ObjectId;
            message.connectId = identity.ConnectId;
            message.position = identity.transform.position;
            SendPacket(SendOptions.ReliableOrdered, peer, GameMsgTypes.ServerSpawnObject, message);
        }

        internal void SendServerSpawnObjectWithData(NetPeer peer, LiteNetLibIdentity identity)
        {
            if (identity == null)
                return;

            if (Assets.SceneObjects.ContainsKey(identity.ObjectId))
                SendServerSpawnSceneObject(peer, identity);
            else
                SendServerSpawnObject(peer, identity);
            identity.SendInitSyncFields(peer);
            identity.SendInitSyncLists(peer);
        }

        internal void SendServerDestroyObject(uint objectId)
        {
            if (!IsServer)
                return;
            foreach (var peer in Peers.Values)
            {
                SendServerDestroyObject(peer, objectId);
            }
        }

        internal void SendServerDestroyObject(NetPeer peer, uint objectId)
        {
            if (!IsServer)
                return;
            var message = new ServerDestroyObjectMessage();
            message.objectId = objectId;
            SendPacket(SendOptions.ReliableOrdered, peer, GameMsgTypes.ServerDestroyObject, message);
        }
#endregion

        protected virtual void SpawnPlayer(NetPeer peer)
        {
            if (Assets.PlayerPrefab == null)
                return;
            var spawnedObject = Assets.NetworkSpawn(Assets.PlayerPrefab.AssetId, Assets.GetPlayerSpawnPosition(), 0, peer.ConnectId);
            spawnedObject.SendInitSyncFields(peer);
            spawnedObject.SendInitSyncLists(peer);
        }

        protected virtual void HandleClientReady(LiteNetLibMessageHandler messageHandler)
        {
            var peer = messageHandler.peer;
            var player = Players[peer.ConnectId];
            player.IsReady = true;
            var spawnedObjects = Assets.SpawnedObjects.Values;
            foreach (var spawnedObject in spawnedObjects)
            {
                spawnedObject.RebuildSubscribers(true);
            }
            SpawnPlayer(peer);
        }

        protected virtual void HandleClientCallFunction(LiteNetLibMessageHandler messageHandler)
        {
            var reader = messageHandler.reader;
            FunctionReceivers receivers = (FunctionReceivers)reader.GetByte();
            long connectId = 0;
            if (receivers == FunctionReceivers.Target)
                connectId = reader.GetLong();
            var info = LiteNetLibElementInfo.DeserializeInfo(reader);
            LiteNetLibIdentity identity;
            if (Assets.SpawnedObjects.TryGetValue(info.objectId, out identity))
            {
                if (receivers == FunctionReceivers.Server)
                    identity.ProcessNetFunction(info, reader, true);
                else
                {
                    var netFunction = identity.ProcessNetFunction(info, reader, false);
                    if (receivers == FunctionReceivers.Target)
                        netFunction.Call(connectId);
                    else
                        netFunction.Call(receivers);
                }
            }
        }

        protected virtual void HandleServerSpawnSceneObject(LiteNetLibMessageHandler messageHandler)
        {
            // Object spawned at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            var message = messageHandler.ReadMessage<ServerSpawnSceneObjectMessage>();
            Assets.NetworkSpawnScene(message.objectId, message.position);
        }

        protected virtual void HandleServerSpawnObject(LiteNetLibMessageHandler messageHandler)
        {
            // Object spawned at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            var message = messageHandler.ReadMessage<ServerSpawnObjectMessage>();
            Assets.NetworkSpawn(message.assetId, message.position, message.objectId, message.connectId);
        }

        protected virtual void HandleServerDestroyObject(LiteNetLibMessageHandler messageHandler)
        {
            // Object spawned at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            var message = messageHandler.ReadMessage<ServerDestroyObjectMessage>();
            Assets.NetworkDestroy(message.objectId);
        }

        protected virtual void HandleServerUpdateSyncField(LiteNetLibMessageHandler messageHandler)
        {
            // Field updated at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            var reader = messageHandler.reader;
            var info = LiteNetLibElementInfo.DeserializeInfo(reader);
            LiteNetLibIdentity identity;
            if (Assets.SpawnedObjects.TryGetValue(info.objectId, out identity))
                identity.ProcessSyncField(info, reader);
        }

        protected virtual void HandleServerCallFunction(LiteNetLibMessageHandler messageHandler)
        {
            var reader = messageHandler.reader;
            var info = LiteNetLibElementInfo.DeserializeInfo(reader);
            LiteNetLibIdentity identity;
            if (Assets.SpawnedObjects.TryGetValue(info.objectId, out identity) && identity.ConnectId == messageHandler.peer.ConnectId)
                identity.ProcessNetFunction(info, reader, true);
        }

        protected virtual void HandleServerUpdateSyncList(LiteNetLibMessageHandler messageHandler)
        {
            // List updated at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            var reader = messageHandler.reader;
            var info = LiteNetLibElementInfo.DeserializeInfo(reader);
            LiteNetLibIdentity identity;
            if (Assets.SpawnedObjects.TryGetValue(info.objectId, out identity))
                identity.ProcessSyncList(info, reader);
        }

        protected virtual void HandleServerUpdateTime(LiteNetLibMessageHandler messageHandler)
        {
            // Server time updated at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            float time = messageHandler.reader.GetFloat();
            ServerTimeOffset = time - Time.unscaledTime;
        }

        protected virtual void HandleServerSyncBehaviour(LiteNetLibMessageHandler messageHandler)
        {
            // Behaviour sync from server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            var reader = messageHandler.reader;
            var objectId = reader.GetUInt();
            var behaviourIndex = reader.GetUShort();
            LiteNetLibIdentity identity;
            if (Assets.SpawnedObjects.TryGetValue(objectId, out identity))
                identity.ProcessSyncBehaviour(behaviourIndex, reader);
        }
    }
}
