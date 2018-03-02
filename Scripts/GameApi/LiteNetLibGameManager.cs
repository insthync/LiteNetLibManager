using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [RequireComponent(typeof(LiteNetLibAssets))]
    public class LiteNetLibGameManager : LiteNetLibManager
    {
        public class GameMsgTypes
        {
            public const short ClientReady = 1;
            public const short ClientNotReady = 2;
            public const short ClientCallFunction = 3;
            public const short ServerSpawnSceneObject = 4;
            public const short ServerSpawnObject = 5;
            public const short ServerDestroyObject = 6;
            public const short ServerUpdateSyncField = 7;
            public const short ServerCallFunction = 8;
            public const short ServerUpdateSyncList = 9;
            public const short ServerUpdateTime = 10;
            public const short ServerSyncBehaviour = 11;
            public const short ServerError = 12;
            public const short Highest = 12;
        }

        internal readonly Dictionary<long, LiteNetLibPlayer> Players = new Dictionary<long, LiteNetLibPlayer>();

        public bool clientReadyOnConnect;

        public float ServerTimeOffset { get; protected set; }
        public float ServerTime
        {
            get
            {
                if (IsServer)
                    return Time.realtimeSinceStartup;
                return Time.realtimeSinceStartup + ServerTimeOffset;
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
            RegisterServerMessage(GameMsgTypes.ClientNotReady, HandleClientNotReady);
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
            RegisterClientMessage(GameMsgTypes.ServerError, HandleServerError);
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
            var player = Players[peer.ConnectId];
            player.ClearSubscribing(false);
            player.DestroyAllObjects();
            Players.Remove(peer.ConnectId);
        }

        public override void OnClientConnected(NetPeer peer)
        {
            base.OnClientConnected(peer);
            if (clientReadyOnConnect)
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
        public void SendClientReady()
        {
            if (!IsClientConnected)
                return;
            SendPacket(SendOptions.ReliableUnordered, Client.Peer, GameMsgTypes.ClientReady, SerializeClientReadyExtra);
        }

        public void SendClientNotReady()
        {
            if (!IsClientConnected)
                return;
            SendPacket(SendOptions.ReliableUnordered, Client.Peer, GameMsgTypes.ClientNotReady);
        }

        public void SendServerUpdateTime()
        {
            if (!IsServer)
                return;
            foreach (var peer in Peers.Values)
            {
                SendServerUpdateTime(peer);
            }
        }

        public void SendServerUpdateTime(NetPeer peer)
        {
            if (!IsServer)
                return;
            var message = new ServerTimeMessage();
            message.serverTime = ServerTime;
            SendPacket(SendOptions.Sequenced, peer, GameMsgTypes.ServerUpdateTime, message);
        }

        public void SendServerSpawnSceneObject(LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return;
            foreach (var peer in Peers.Values)
            {
                SendServerSpawnSceneObject(peer, identity);
            }
        }

        public void SendServerSpawnSceneObject(NetPeer peer, LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return;
            var message = new ServerSpawnSceneObjectMessage();
            message.objectId = identity.ObjectId;
            message.position = identity.transform.position;
            SendPacket(SendOptions.ReliableOrdered, peer, GameMsgTypes.ServerSpawnSceneObject, message);
        }

        public void SendServerSpawnObject(LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return;
            foreach (var peer in Peers.Values)
            {
                SendServerSpawnObject(peer, identity);
            }
        }

        public void SendServerSpawnObject(NetPeer peer, LiteNetLibIdentity identity)
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

        public void SendServerSpawnObjectWithData(NetPeer peer, LiteNetLibIdentity identity)
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

        public void SendServerDestroyObject(uint objectId)
        {
            if (!IsServer)
                return;
            foreach (var peer in Peers.Values)
            {
                SendServerDestroyObject(peer, objectId);
            }
        }

        public void SendServerDestroyObject(NetPeer peer, uint objectId)
        {
            if (!IsServer)
                return;
            var message = new ServerDestroyObjectMessage();
            message.objectId = objectId;
            SendPacket(SendOptions.ReliableOrdered, peer, GameMsgTypes.ServerDestroyObject, message);
        }

        public void SendServerError(bool shouldDisconnect, string errorMessage)
        {
            if (!IsServer)
                return;
            foreach (var peer in Peers.Values)
            {
                SendServerError(peer, shouldDisconnect, errorMessage);
            }
        }

        public void SendServerError(NetPeer peer, bool shouldDisconnect, string errorMessage)
        {
            if (!IsServer)
                return;
            var message = new ServerErrorMessage();
            message.shouldDisconnect = shouldDisconnect;
            message.errorMessage = errorMessage;
            SendPacket(SendOptions.ReliableOrdered, peer, GameMsgTypes.ServerDestroyObject, message);
        }
        #endregion

        #region Message Handlers
        protected virtual void HandleClientReady(LiteNetLibMessageHandler messageHandler)
        {
            var peer = messageHandler.peer;
            var reader = messageHandler.reader;
            SetPlayerReady(peer, reader);
        }

        protected virtual void HandleClientNotReady(LiteNetLibMessageHandler messageHandler)
        {
            var peer = messageHandler.peer;
            var reader = messageHandler.reader;
            SetPlayerNotReady(peer, reader);
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
            var message = messageHandler.ReadMessage<ServerTimeMessage>();
            ServerTimeOffset = message.serverTime - Time.realtimeSinceStartup;
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

        protected virtual void HandleServerError(LiteNetLibMessageHandler messageHandler)
        {
            // Error sent from server
            var message = messageHandler.ReadMessage<ServerErrorMessage>();
            OnServerError(message);
        }
        #endregion

        /// <summary>
        /// Overrride this function to send custom data when send client ready message
        /// </summary>
        /// <param name="writer"></param>
        public virtual void SerializeClientReadyExtra(NetDataWriter writer) { }

        /// <summary>
        /// Override this function to read custom data that come with send client ready message
        /// </summary>
        /// <param name="playerIdentity"></param>
        /// <param name="reader"></param>
        public virtual void DeserializeClientReadyExtra(LiteNetLibIdentity playerIdentity, NetDataReader reader) { }

        /// <summary>
        /// Override this function to show error message / disconnect
        /// </summary>
        /// <param name="message"></param>
        public virtual void OnServerError(ServerErrorMessage message)
        {
            if (message.shouldDisconnect && !IsServer)
                StopClient();
        }

        public virtual void SetPlayerReady(NetPeer peer, NetDataReader reader)
        {
            if (!IsServer)
                return;

            var player = Players[peer.ConnectId];
            if (player.IsReady)
                return;

            player.IsReady = true;
            var playerIdentity = SpawnPlayer(peer);
            DeserializeClientReadyExtra(playerIdentity, reader);
            var spawnedObjects = Assets.SpawnedObjects.Values;
            foreach (var spawnedObject in spawnedObjects)
            {
                if (spawnedObject.ConnectId == player.ConnectId)
                    continue;

                if (spawnedObject.ShouldAddSubscriber(player))
                    spawnedObject.AddSubscriber(player);
            }
        }

        public virtual void SetPlayerNotReady(NetPeer peer, NetDataReader reader)
        {
            if (!IsServer)
                return;

            var player = Players[peer.ConnectId];
            if (!player.IsReady)
                return;

            player.IsReady = false;
            player.ClearSubscribing(true);
            player.DestroyAllObjects();
        }

        protected LiteNetLibIdentity SpawnPlayer(NetPeer peer)
        {
            if (Assets.PlayerPrefab == null)
                return null;
            return SpawnPlayer(peer, assets.PlayerPrefab);
        }

        protected LiteNetLibIdentity SpawnPlayer(NetPeer peer, LiteNetLibIdentity prefab)
        {
            if (prefab == null)
                return null;
            return SpawnPlayer(peer, prefab.AssetId);
        }

        protected LiteNetLibIdentity SpawnPlayer(NetPeer peer, string assetId)
        {
            var spawnedObject = Assets.NetworkSpawn(assetId, Assets.GetPlayerSpawnPosition(), 0, peer.ConnectId);
            if (spawnedObject != null)
            {
                spawnedObject.SendInitSyncFields(peer);
                spawnedObject.SendInitSyncLists(peer);
                return spawnedObject;
            }
            return null;
        }
    }
}
