using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLibHighLevel.Messages;

namespace LiteNetLibHighLevel
{
    [RequireComponent(typeof(LiteNetLibAssets))]
    public class LiteNetLibGameManager : LiteNetLibManager
    {
        public class GameMsgTypes
        {
            public const short ClientReady = 1;
            public const short ClientCallCommand = 2;
            public const short ServerSpawnObject = 3;
            public const short ServerDestroyObject = 4;
            public const short ServerUpdateSyncField = 5;
            public const short ServerRpc = 6;
            public const short Highest = 6;
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
            Assets.ClearRegisterPrefabs();
            Assets.RegisterPrefabs();
        }

        public override bool StartServer()
        {
            if (base.StartServer())
            {
                Assets.RegisterSceneObjects();
                return true;
            }
            return false;
        }

        public override LiteNetLibClient StartClient()
        {
            var client = base.StartClient();
            if (client != null && !IsServer)
                Assets.RegisterSceneObjects();
            return client;
        }

        protected override void RegisterServerMessages()
        {
            base.RegisterServerMessages();
            RegisterServerMessage(GameMsgTypes.ClientReady, HandleClientReady);
            RegisterServerMessage(GameMsgTypes.ClientCallCommand, HandleClientCallCommand);
        }

        protected override void RegisterClientMessages()
        {
            base.RegisterClientMessages();
            RegisterClientMessage(GameMsgTypes.ServerSpawnObject, HandleServerSpawnObject);
            RegisterClientMessage(GameMsgTypes.ServerDestroyObject, HandleServerDestroyObject);
            RegisterClientMessage(GameMsgTypes.ServerUpdateSyncField, HandleServerUpdateSyncField);
            RegisterClientMessage(GameMsgTypes.ServerRpc, HandleServerRpc);
        }

        #region Relates components functions
        public LiteNetLibIdentity NetworkSpawn(GameObject gameObject)
        {
            return Assets.NetworkSpawn(gameObject);
        }

        public bool NetworkDestroy(GameObject gameObject)
        {
            return Assets.NetworkDestroy(gameObject);
        }
        #endregion

        public void SendClientReady()
        {
            SendPacket(SendOptions.ReliableOrdered, Client.Peer, GameMsgTypes.ClientReady);
        }

        public void SendServerSpawnObject(LiteNetLibIdentity identity)
        {
            foreach (var peer in peers.Values)
            {
                SendServerSpawnObject(peer, identity);
            }
        }

        public void SendServerSpawnObject(NetPeer peer, LiteNetLibIdentity identity)
        {
            var message = new ServerSpawnObjectMessage();
            message.assetId = identity.AssetId;
            message.objectId = identity.ObjectId;
            message.connectId = identity.ConnectId;
            message.position = identity.transform.position;
            SendPacket(SendOptions.ReliableOrdered, peer, GameMsgTypes.ServerSpawnObject, message);
        }

        protected virtual void HandleClientReady(LiteNetLibMessageHandler messageHandler)
        {
            var spawnedObjects = Assets.SpawnedObjects.Values;
            foreach (var spawnedObject in spawnedObjects)
            {
                SendServerSpawnObject(messageHandler.peer, spawnedObject);
            }
        }

        protected virtual void HandleClientCallCommand(LiteNetLibMessageHandler messageHandler)
        {

        }

        protected virtual void HandleServerSpawnObject(LiteNetLibMessageHandler messageHandler)
        {
            var message = messageHandler.ReadMessage<ServerSpawnObjectMessage>();
            Assets.NetworkSpawn(message.assetId, message.position, message.objectId, message.connectId);
        }

        protected virtual void HandleServerDestroyObject(LiteNetLibMessageHandler messageHandler)
        {

        }

        protected virtual void HandleServerUpdateSyncField(LiteNetLibMessageHandler messageHandler)
        {

        }

        protected virtual void HandleServerRpc(LiteNetLibMessageHandler messageHandler)
        {

        }
    }
}

