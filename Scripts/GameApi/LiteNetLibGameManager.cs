using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine.Profiling;
using Cysharp.Threading.Tasks;

namespace LiteNetLibManager
{
    [RequireComponent(typeof(LiteNetLibAssets))]
    public class LiteNetLibGameManager : LiteNetLibManager
    {
        [Header("Game manager configs")]
        public float pingDuration = 1f;
        public bool doNotEnterGameOnConnect;
        public bool doNotDestroyOnSceneChanges;

        protected readonly Dictionary<long, LiteNetLibPlayer> Players = new Dictionary<long, LiteNetLibPlayer>();

        private float tempDeltaTime;
        private float sendPingCountDown;
        private string serverSceneName;
        private AsyncOperation loadSceneAsyncOperation;
        private bool isPinging;
        private long pingTime;

        public long ClientConnectionId { get; protected set; }

        public long Rtt { get; private set; }
        public long Timestamp { get { return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); } }
        public long ServerUnixTimeOffset { get; protected set; }
        public long ServerUnixTime
        {
            get
            {
                if (IsServer)
                    return Timestamp;
                return Timestamp + ServerUnixTimeOffset;
            }
        }

        public string ServerSceneName
        {
            get
            {
                if (IsServer)
                    return serverSceneName;
                return string.Empty;
            }
        }

        public LiteNetLibAssets Assets { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Assets = GetComponent<LiteNetLibAssets>();
            serverSceneName = string.Empty;
            if (doNotDestroyOnSceneChanges)
                DontDestroyOnLoad(gameObject);
        }

        protected override void LateUpdate()
        {
            if (loadSceneAsyncOperation == null)
            {
                tempDeltaTime = Time.unscaledDeltaTime;
                // Update Spawned Objects
                Profiler.BeginSample("LiteNetLibGameManager - Update Spawned Objects");
                foreach (LiteNetLibIdentity spawnedObject in Assets.GetSpawnedObjects())
                {
                    if (spawnedObject == null)
                        continue;
                    spawnedObject.NetworkUpdate(tempDeltaTime);
                }
                Profiler.EndSample();

                if (IsClientConnected)
                {
                    // Send ping from client
                    sendPingCountDown -= tempDeltaTime;
                    if (sendPingCountDown <= 0f && !isPinging)
                    {
                        SendClientPing();
                        sendPingCountDown = pingDuration;
                    }
                }
            }
            base.LateUpdate();
        }

        public virtual uint PacketVersion()
        {
            return 2;
        }

        public bool TryGetPlayer(long connectionId, out LiteNetLibPlayer player)
        {
            return Players.TryGetValue(connectionId, out player);
        }

        public bool ContainsPlayer(long connectionId)
        {
            return Players.ContainsKey(connectionId);
        }

        public LiteNetLibPlayer GetPlayer(long connectionId)
        {
            return Players[connectionId];
        }

        public Dictionary<long, LiteNetLibPlayer>.ValueCollection GetPlayers()
        {
            return Players.Values;
        }

        public int PlayersCount
        {
            get { return Players.Count; }
        }

        /// <summary>
        /// Call this function to change gameplay scene at server, then the server will tell clients to change scene
        /// </summary>
        /// <param name="sceneName"></param>
        public void ServerSceneChange(string sceneName)
        {
            if (!IsServer)
                return;
            LoadSceneRoutine(sceneName, true).Forget();
        }

        /// <summary>
        /// This function will be called to load scene async
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="online"></param>
        /// <returns></returns>
        private async UniTaskVoid LoadSceneRoutine(string sceneName, bool online)
        {
            if (loadSceneAsyncOperation == null)
            {
                // If doNotDestroyOnSceneChanges not TRUE still not destroy this game object
                // But it will be destroyed after scene loaded, if scene is offline scene
                if (!doNotDestroyOnSceneChanges)
                    DontDestroyOnLoad(gameObject);

                if (online)
                {
                    foreach (LiteNetLibPlayer player in Players.Values)
                    {
                        player.IsReady = false;
                        player.SubscribingObjects.Clear();
                        player.SpawnedObjects.Clear();
                    }
                    Assets.Clear(true);
                }

                if (LogDev) Logging.Log(LogTag, "Loading Scene: " + sceneName + " is online: " + online);
                if (Assets.onLoadSceneStart != null)
                    Assets.onLoadSceneStart.Invoke(sceneName, online, 0f);

                loadSceneAsyncOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                while (loadSceneAsyncOperation != null && !loadSceneAsyncOperation.isDone)
                {
                    if (Assets.onLoadSceneProgress != null)
                        Assets.onLoadSceneProgress.Invoke(sceneName, online, loadSceneAsyncOperation.progress);
                    await UniTask.Yield();
                }
                loadSceneAsyncOperation = null;

                if (online)
                {
                    Assets.Initialize();
                    if (LogDev) Logging.Log(LogTag, "Loaded Scene: " + sceneName + " -> Assets.Initialize()");
                    if (IsServer)
                    {
                        serverSceneName = sceneName;
                        Assets.SpawnSceneObjects();
                        if (LogDev) Logging.Log(LogTag, "Loaded Scene: " + sceneName + " -> Assets.SpawnSceneObjects()");
                        OnServerOnlineSceneLoaded();
                        if (LogDev) Logging.Log(LogTag, "Loaded Scene: " + sceneName + " -> OnServerOnlineSceneLoaded()");
                        SendServerSceneChange(sceneName);
                        if (LogDev) Logging.Log(LogTag, "Loaded Scene: " + sceneName + " -> SendServerSceneChange()");
                    }
                    if (IsClient)
                    {
                        while (ClientConnectionId < 0)
                        {
                            // Wait for client connection Id from server
                            await UniTask.Yield();
                        }
                        OnClientOnlineSceneLoaded();
                        if (LogDev) Logging.Log(LogTag, "Loaded Scene: " + sceneName + " -> OnClientOnlineSceneLoaded()");
                        SendClientReady();
                        if (LogDev) Logging.Log(LogTag, "Loaded Scene: " + sceneName + " -> SendClientReady()");
                    }
                }
                else if (!doNotDestroyOnSceneChanges)
                {
                    // Destroy manager's game object if loaded scene is not online scene
                    Destroy(gameObject);
                }

                if (LogDev) Logging.Log(LogTag, "Loaded Scene: " + sceneName + " is online: " + online);
                if (Assets.onLoadSceneFinish != null)
                    Assets.onLoadSceneFinish.Invoke(sceneName, online, 1f);
            }
        }

        protected override void RegisterServerMessages()
        {
            base.RegisterServerMessages();
            EnableServerRequestResponse(GameMsgTypes.Request, GameMsgTypes.Response);
            RegisterServerRequest<EnterGameRequestMessage, EnterGameResponseMessage>(GameReqTypes.EnterGame, HandleEnterGameRequest);
            RegisterServerRequest<EmptyMessage, EmptyMessage>(GameReqTypes.ClientReady, HandleClientReadyRequest);
            RegisterServerRequest<EmptyMessage, EmptyMessage>(GameReqTypes.ClientNotReady, HandleClientNotReadyRequest);
            RegisterServerMessage(GameMsgTypes.CallFunction, HandleClientCallFunction);
            RegisterServerMessage(GameMsgTypes.UpdateSyncField, HandleClientUpdateSyncField);
            RegisterServerMessage(GameMsgTypes.InitialSyncField, HandleClientInitialSyncField);
            RegisterServerMessage(GameMsgTypes.ClientSendTransform, HandleClientSendTransform);
            RegisterServerMessage(GameMsgTypes.Ping, HandleClientPing);
        }

        protected override void RegisterClientMessages()
        {
            base.RegisterClientMessages();
            EnableClientRequestResponse(GameMsgTypes.Request, GameMsgTypes.Response);
            RegisterClientResponse<EnterGameRequestMessage, EnterGameResponseMessage>(GameReqTypes.EnterGame, HandleEnterGameResponse);
            RegisterClientResponse<EmptyMessage, EmptyMessage>(GameReqTypes.ClientReady, HandleClientReadyResponse);
            RegisterClientResponse<EmptyMessage, EmptyMessage>(GameReqTypes.ClientNotReady, HandleClientNotReadyResponse);
            RegisterClientMessage(GameMsgTypes.ServerSpawnSceneObject, HandleServerSpawnSceneObject);
            RegisterClientMessage(GameMsgTypes.ServerSpawnObject, HandleServerSpawnObject);
            RegisterClientMessage(GameMsgTypes.ServerDestroyObject, HandleServerDestroyObject);
            RegisterClientMessage(GameMsgTypes.CallFunction, HandleServerCallFunction);
            RegisterClientMessage(GameMsgTypes.UpdateSyncField, HandleServerUpdateSyncField);
            RegisterClientMessage(GameMsgTypes.InitialSyncField, HandleServerInitialSyncField);
            RegisterClientMessage(GameMsgTypes.OperateSyncList, HandleServerUpdateSyncList);
            RegisterClientMessage(GameMsgTypes.ServerSyncBehaviour, HandleServerSyncBehaviour);
            RegisterClientMessage(GameMsgTypes.ServerError, HandleServerError);
            RegisterClientMessage(GameMsgTypes.ServerSceneChange, HandleServerSceneChange);
            RegisterClientMessage(GameMsgTypes.ServerSetObjectOwner, HandleServerSetObjectOwner);
            RegisterClientMessage(GameMsgTypes.Ping, HandleServerPing);
        }

        public override void OnPeerConnected(long connectionId)
        {
            base.OnPeerConnected(connectionId);
            if (!Players.ContainsKey(connectionId))
                Players[connectionId] = new LiteNetLibPlayer(this, connectionId);
        }

        public override void OnPeerDisconnected(long connectionId, DisconnectInfo disconnectInfo)
        {
            base.OnPeerDisconnected(connectionId, disconnectInfo);
            if (Players.ContainsKey(connectionId))
            {
                LiteNetLibPlayer player = Players[connectionId];
                player.ClearSubscribing(false);
                player.DestroyAllObjects();
                Players.Remove(connectionId);
            }
        }

        public override void OnClientConnected()
        {
            base.OnClientConnected();
            // Reset client connection id, will be received from server later
            ClientConnectionId = -1;
            isPinging = false;
            Rtt = 0;
            if (!doNotEnterGameOnConnect)
                SendClientEnterGame();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Reset client connection id, will be received from server later
            ClientConnectionId = -1;
            isPinging = false;
            Rtt = 0;
            if (!Assets.onlineScene.IsSet() || Assets.onlineScene.SceneName.Equals(SceneManager.GetActiveScene().name))
            {
                serverSceneName = SceneManager.GetActiveScene().name;
                Assets.Initialize();
                Assets.SpawnSceneObjects();
                OnServerOnlineSceneLoaded();
            }
            else
            {
                serverSceneName = Assets.onlineScene.SceneName;
                LoadSceneRoutine(Assets.onlineScene.SceneName, true).Forget();
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            Players.Clear();
            Assets.Clear();
            if (Assets.offlineScene.IsSet() && !Assets.offlineScene.SceneName.Equals(SceneManager.GetActiveScene().name))
                LoadSceneRoutine(Assets.offlineScene.SceneName, false).Forget();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsServer)
            {
                Players.Clear();
                Assets.Clear();
                if (Assets.offlineScene.IsSet() && !Assets.offlineScene.SceneName.Equals(SceneManager.GetActiveScene().name))
                    LoadSceneRoutine(Assets.offlineScene.SceneName, false).Forget();
            }
        }

        #region Send messages functions
        public void SendClientEnterGame()
        {
            if (!IsClientConnected)
                return;
            ClientSendRequest(GameReqTypes.EnterGame, new EmptyMessage(), SerializeEnterGameData);
        }

        public void SendClientReady()
        {
            if (!IsClientConnected)
                return;
            ClientSendRequest(GameReqTypes.ClientReady, new EmptyMessage(), SerializeClientReadyData);
        }

        public void SendClientNotReady()
        {
            if (!IsClientConnected)
                return;
            ClientSendRequest(GameReqTypes.ClientNotReady, new EmptyMessage(), null);
        }

        public void SendClientPing()
        {
            if (!IsClientConnected)
                return;
            if (isPinging)
                return;
            isPinging = true;
            pingTime = Timestamp;
            ClientSendPacket(DeliveryMethod.ReliableOrdered, GameMsgTypes.Ping);
        }

        public void SendServerSpawnSceneObject(LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return;
            foreach (long connectionId in ConnectionIds)
            {
                SendServerSpawnSceneObject(connectionId, identity);
            }
        }

        public void SendServerSpawnSceneObject(long connectionId, LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return;
            LiteNetLibPlayer player;
            if (!Players.TryGetValue(connectionId, out player) || !player.IsReady)
                return;
            ServerSpawnSceneObjectMessage message = new ServerSpawnSceneObjectMessage();
            message.objectId = identity.ObjectId;
            message.connectionId = identity.ConnectionId;
            message.position = identity.transform.position;
            message.rotation = identity.transform.rotation;
            ServerSendPacket(connectionId, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerSpawnSceneObject, message, identity.WriteInitialSyncFields);
        }

        public void SendServerSpawnObject(LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return;
            foreach (long connectionId in ConnectionIds)
            {
                SendServerSpawnObject(connectionId, identity);
            }
        }

        public void SendServerSpawnObject(long connectionId, LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return;
            LiteNetLibPlayer player;
            if (!Players.TryGetValue(connectionId, out player) || !player.IsReady)
                return;
            ServerSpawnObjectMessage message = new ServerSpawnObjectMessage();
            message.hashAssetId = identity.HashAssetId;
            message.objectId = identity.ObjectId;
            message.connectionId = identity.ConnectionId;
            message.position = identity.transform.position;
            message.rotation = identity.transform.rotation;
            ServerSendPacket(connectionId, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerSpawnObject, message, identity.WriteInitialSyncFields);
        }

        public void SendServerSpawnObjectWithData(long connectionId, LiteNetLibIdentity identity)
        {
            if (identity == null)
                return;

            if (Assets.ContainsSceneObject(identity.ObjectId))
                SendServerSpawnSceneObject(connectionId, identity);
            else
                SendServerSpawnObject(connectionId, identity);
            identity.SendInitSyncFields(connectionId);
            identity.SendInitSyncLists(connectionId);
        }

        public void SendServerDestroyObject(uint objectId, byte reasons)
        {
            if (!IsServer)
                return;
            foreach (long connectionId in ConnectionIds)
            {
                SendServerDestroyObject(connectionId, objectId, reasons);
            }
        }

        public void SendServerDestroyObject(long connectionId, uint objectId, byte reasons)
        {
            if (!IsServer)
                return;
            LiteNetLibPlayer player;
            if (!Players.TryGetValue(connectionId, out player) || !player.IsReady)
                return;
            ServerDestroyObjectMessage message = new ServerDestroyObjectMessage();
            message.objectId = objectId;
            message.reasons = reasons;
            ServerSendPacket(connectionId, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerDestroyObject, message);
        }

        public void SendServerError(bool shouldDisconnect, string errorMessage)
        {
            if (!IsServer)
                return;
            foreach (long connectionId in ConnectionIds)
            {
                SendServerError(connectionId, shouldDisconnect, errorMessage);
            }
        }

        public void SendServerError(long connectionId, bool shouldDisconnect, string errorMessage)
        {
            if (!IsServer)
                return;
            LiteNetLibPlayer player;
            if (!Players.TryGetValue(connectionId, out player) || !player.IsReady)
                return;
            ServerErrorMessage message = new ServerErrorMessage();
            message.shouldDisconnect = shouldDisconnect;
            message.errorMessage = errorMessage;
            ServerSendPacket(connectionId, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerDestroyObject, message);
        }

        public void SendServerSceneChange(string sceneName)
        {
            if (!IsServer)
                return;
            foreach (long connectionId in ConnectionIds)
            {
                if (IsClientConnected && connectionId == ClientConnectionId)
                    continue;
                SendServerSceneChange(connectionId, sceneName);
            }
        }

        public void SendServerSceneChange(long connectionId, string sceneName)
        {
            if (!IsServer)
                return;
            ServerSceneChangeMessage message = new ServerSceneChangeMessage();
            message.serverSceneName = sceneName;
            ServerSendPacket(connectionId, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerSceneChange, message);
        }

        public void SendServerSetObjectOwner(uint objectId, long ownerConnectionId)
        {
            if (!IsServer)
                return;
            foreach (long connectionId in ConnectionIds)
            {
                SendServerSetObjectOwner(connectionId, objectId, ownerConnectionId);
            }
        }

        public void SendServerSetObjectOwner(long connectionId, uint objectId, long ownerConnectionId)
        {
            if (!IsServer)
                return;
            ServerSetObjectOwner message = new ServerSetObjectOwner();
            message.objectId = objectId;
            message.connectionId = ownerConnectionId;
            ServerSendPacket(connectionId, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerSetObjectOwner, message);
        }
        #endregion

        #region Message Handlers

        protected virtual UniTaskVoid HandleEnterGameRequest(
            RequestHandlerData requestHandler,
            EnterGameRequestMessage request,
            RequestProceedResultDelegate<EnterGameResponseMessage> result)
        {
            AckResponseCode responseCode = AckResponseCode.Error;
            EnterGameResponseMessage response = new EnterGameResponseMessage();
            if (request.packetVersion == PacketVersion() &&
                DeserializeEnterGameData(requestHandler.ConnectionId, requestHandler.Reader))
            {
                responseCode = AckResponseCode.Success;
                response.connectionId = requestHandler.ConnectionId;
                response.serverSceneName = ServerSceneName;
            }
            result.Invoke(responseCode, response);
            return default;
        }

        protected virtual UniTaskVoid HandleEnterGameResponse(
            ResponseHandlerData responseHandler,
            AckResponseCode responseCode,
            EnterGameResponseMessage response)
        {
            if (responseCode == AckResponseCode.Success)
            {
                ClientConnectionId = response.connectionId;
                HandleServerSceneChange(response.serverSceneName);
            }
            else
            {
                if (LogError) Logging.LogError(LogTag, "Enter game request was refused by server, disconnecting...");
                StopClient();
            }
            return default;
        }

        protected virtual UniTaskVoid HandleClientReadyRequest(
            RequestHandlerData requestHandler,
            EmptyMessage request,
            RequestProceedResultDelegate<EmptyMessage> result)
        {
            AckResponseCode responseCode = AckResponseCode.Error;
            if (SetPlayerReady(requestHandler.ConnectionId, requestHandler.Reader))
            {
                responseCode = AckResponseCode.Success;
            }
            result.Invoke(responseCode, new EmptyMessage());
            return default;
        }

        protected virtual UniTaskVoid HandleClientReadyResponse(
            ResponseHandlerData responseHandler,
            AckResponseCode responseCode,
            EmptyMessage response)
        {
            // Override this function to do something by response code
            return default;
        }

        protected virtual UniTaskVoid HandleClientNotReadyRequest(
            RequestHandlerData requestHandler,
            EmptyMessage request,
            RequestProceedResultDelegate<EmptyMessage> result)
        {
            AckResponseCode responseCode = AckResponseCode.Error;
            if (SetPlayerNotReady(requestHandler.ConnectionId, requestHandler.Reader))
            {
                responseCode = AckResponseCode.Success;
            }
            result.Invoke(responseCode, new EmptyMessage());
            return default;
        }

        protected virtual UniTaskVoid HandleClientNotReadyResponse(
            ResponseHandlerData responseHandler,
            AckResponseCode responseCode,
            EmptyMessage response)
        {
            // Override this function to do something by response code
            return default;
        }

        protected virtual void HandleClientInitialSyncField(MessageHandlerData messageHandler)
        {
            // Field updated at owner-client, if this is server then multicast message to other clients
            if (!IsServer)
                return;
            NetDataReader reader = messageHandler.Reader;
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(reader);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(info.objectId, out identity))
            {
                LiteNetLibSyncField syncField = identity.GetSyncField(info);
                // Sync field at server also have to be client multicast to allow it to multicast to other clients
                if (syncField != null && syncField.syncMode == LiteNetLibSyncField.SyncMode.ClientMulticast)
                {
                    // If this is server but it is not host, set data (deserialize) then pass to other clients
                    // If this is host don't set data because it's already did (in LiteNetLibSyncField class)
                    if (!identity.IsOwnerClient)
                        syncField.Deserialize(reader, true);
                    // Send to other clients
                    foreach (long connectionId in GetConnectionIds())
                    {
                        // Don't send the update to owner client because it was updated before send update to server
                        if (connectionId == messageHandler.ConnectionId)
                            continue;
                        // Send update to clients except owner client
                        if (identity.IsSubscribedOrOwning(connectionId))
                            syncField.SendUpdate(true, connectionId);
                    }
                }
            }
        }

        protected virtual void HandleClientUpdateSyncField(MessageHandlerData messageHandler)
        {
            // Field updated at owner-client, if this is server then multicast message to other clients
            if (!IsServer)
                return;
            NetDataReader reader = messageHandler.Reader;
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(reader);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(info.objectId, out identity))
            {
                LiteNetLibSyncField syncField = identity.GetSyncField(info);
                // Sync field at server also have to be client multicast to allow it to multicast to other clients
                if (syncField != null && syncField.syncMode == LiteNetLibSyncField.SyncMode.ClientMulticast)
                {
                    // If this is server but it is not host, set data (deserialize) then pass to other clients
                    // If this is host don't set data because it's already did (in LiteNetLibSyncField class)
                    if (!identity.IsOwnerClient)
                        syncField.Deserialize(reader, false);
                    // Send to other clients
                    foreach (long connectionId in GetConnectionIds())
                    {
                        // Don't send the update to owner client because it was updated before send update to server
                        if (connectionId == messageHandler.ConnectionId)
                            continue;
                        // Send update to clients except owner client
                        if (identity.IsSubscribedOrOwning(connectionId))
                            syncField.SendUpdate(false, connectionId);
                    }
                }
            }
        }

        protected virtual void HandleClientCallFunction(MessageHandlerData messageHandler)
        {
            NetDataReader reader = messageHandler.Reader;
            FunctionReceivers receivers = (FunctionReceivers)reader.GetByte();
            long connectionId = -1;
            if (receivers == FunctionReceivers.Target)
                connectionId = reader.GetPackedLong();
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(reader);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(info.objectId, out identity))
            {
                LiteNetLibFunction netFunction = identity.GetNetFunction(info);
                if (netFunction == null)
                {
                    // There is no net function that player try to call (player may try to hack)
                    return;
                }
                if (!netFunction.CanCallByEveryone && messageHandler.ConnectionId != identity.ConnectionId)
                {
                    // The function not allowed anyone except owner client to call this net function
                    // And the client is also not the owner client
                    return;
                }
                if (receivers == FunctionReceivers.Server)
                {
                    // Request from client to server, so hook callback at server immediately
                    identity.ProcessNetFunction(netFunction, reader, true);
                }
                else
                {
                    // Request from client to other clients, so hook callback later
                    identity.ProcessNetFunction(netFunction, reader, false);
                    // Use call with out parameters set because parameters already set while process net function
                    if (receivers == FunctionReceivers.Target)
                        netFunction.CallWithoutParametersSet(connectionId);
                    else
                        netFunction.CallWithoutParametersSet(DeliveryMethod.ReliableOrdered, receivers);
                }
            }
        }

        protected virtual void HandleClientSendTransform(MessageHandlerData messageHandler)
        {
            NetDataReader reader = messageHandler.Reader;
            uint objectId = reader.GetPackedUInt();
            byte behaviourIndex = reader.GetByte();
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(objectId, out identity))
            {
                LiteNetLibTransform netTransform;
                if (identity.TryGetBehaviour(behaviourIndex, out netTransform))
                    netTransform.HandleClientSendTransform(reader);
            }
        }

        protected void HandleClientPing(MessageHandlerData messageHandler)
        {
            ServerSendPacket(messageHandler.ConnectionId, DeliveryMethod.ReliableOrdered, GameMsgTypes.Ping, (writer) =>
            {
                // Send server time
                writer.PutPackedLong(ServerUnixTime);
            });
        }

        protected virtual void HandleServerSpawnSceneObject(MessageHandlerData messageHandler)
        {
            ServerSpawnSceneObjectMessage message = messageHandler.ReadMessage<ServerSpawnSceneObjectMessage>();
            if (!IsServer)
                Assets.NetworkSpawnScene(message.objectId, message.position, message.rotation, message.objectId);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(message.objectId, out identity))
            {
                // If it is not server, read its initial data
                if (!IsServer)
                    identity.ReadInitialSyncFields(messageHandler.Reader);
                // If it is host, it may hidden so show it
                if (IsServer)
                    identity.OnServerSubscribingAdded();
            }
        }

        protected virtual void HandleServerSpawnObject(MessageHandlerData messageHandler)
        {
            ServerSpawnObjectMessage message = messageHandler.ReadMessage<ServerSpawnObjectMessage>();
            if (!IsServer)
                Assets.NetworkSpawn(message.hashAssetId, message.position, message.rotation, message.objectId, message.connectionId);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(message.objectId, out identity))
            {
                // If it is not server, read its initial data
                if (!IsServer)
                    identity.ReadInitialSyncFields(messageHandler.Reader);
                // If it is host, it may hidden so show it
                if (IsServer)
                    identity.OnServerSubscribingAdded();
            }
        }

        protected virtual void HandleServerDestroyObject(MessageHandlerData messageHandler)
        {
            ServerDestroyObjectMessage message = messageHandler.ReadMessage<ServerDestroyObjectMessage>();
            if (!IsServer)
                Assets.NetworkDestroy(message.objectId, message.reasons);
            // If this is host and reasons is removed from subscribing so hide it, don't destroy it
            LiteNetLibIdentity identity;
            if (IsServer && message.reasons == DestroyObjectReasons.RemovedFromSubscribing && Assets.TryGetSpawnedObject(message.objectId, out identity))
                identity.OnServerSubscribingRemoved();
        }

        protected virtual void HandleServerInitialSyncField(MessageHandlerData messageHandler)
        {
            // Field updated at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            NetDataReader reader = messageHandler.Reader;
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(reader);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(info.objectId, out identity))
                identity.ProcessSyncField(info, reader, true);
        }

        protected virtual void HandleServerUpdateSyncField(MessageHandlerData messageHandler)
        {
            // Field updated at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            NetDataReader reader = messageHandler.Reader;
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(reader);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(info.objectId, out identity))
                identity.ProcessSyncField(info, reader, false);
        }

        protected virtual void HandleServerCallFunction(MessageHandlerData messageHandler)
        {
            NetDataReader reader = messageHandler.Reader;
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(reader);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(info.objectId, out identity))
            {
                // All function from server will be processed (because it's always trust server)
                identity.ProcessNetFunction(info, reader, true);
            }
        }

        protected virtual void HandleServerUpdateSyncList(MessageHandlerData messageHandler)
        {
            // List updated at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            NetDataReader reader = messageHandler.Reader;
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(reader);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(info.objectId, out identity))
                identity.ProcessSyncList(info, reader);
        }

        protected virtual void HandleServerSyncBehaviour(MessageHandlerData messageHandler)
        {
            // Behaviour sync from server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            NetDataReader reader = messageHandler.Reader;
            uint objectId = reader.GetPackedUInt();
            byte behaviourIndex = reader.GetByte();
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(objectId, out identity))
                identity.ProcessSyncBehaviour(behaviourIndex, reader);
        }

        protected virtual void HandleServerError(MessageHandlerData messageHandler)
        {
            // Error sent from server
            ServerErrorMessage message = messageHandler.ReadMessage<ServerErrorMessage>();
            OnServerError(message);
        }

        protected virtual void HandleServerSceneChange(MessageHandlerData messageHandler)
        {
            // Received scene name from server
            ServerSceneChangeMessage message = messageHandler.ReadMessage<ServerSceneChangeMessage>();
            HandleServerSceneChange(message.serverSceneName);
        }

        protected virtual void HandleServerSceneChange(string serverSceneName)
        {
            if (IsServer)
            {
                // If it is host (both client and server) it will send ready state to spawn player's character without scene load
                if (string.IsNullOrEmpty(serverSceneName) || serverSceneName.Equals(SceneManager.GetActiveScene().name))
                {
                    OnClientOnlineSceneLoaded();
                    SendClientReady();
                }
                return;
            }

            if (string.IsNullOrEmpty(serverSceneName) || serverSceneName.Equals(SceneManager.GetActiveScene().name))
            {
                Assets.Initialize();
                OnClientOnlineSceneLoaded();
                SendClientReady();
            }
            else
            {
                // If scene is difference, load changing scene
                LoadSceneRoutine(serverSceneName, true).Forget();
            }
        }

        protected virtual void HandleServerSetObjectOwner(MessageHandlerData messageHandler)
        {
            ServerSetObjectOwner message = messageHandler.ReadMessage<ServerSetObjectOwner>();
            if (!IsServer)
                Assets.SetObjectOwner(message.objectId, message.connectionId);
        }

        protected void HandleServerPing(MessageHandlerData messageHandler)
        {
            isPinging = false;
            Rtt = Timestamp - pingTime;
            // Time offset = server time - current timestamp - rtt
            ServerUnixTimeOffset = messageHandler.Reader.GetPackedLong() - Timestamp - Rtt;
            if (LogDev) Logging.Log(LogTag, "Rtt: " + Rtt + ", ServerUnixTimeOffset: " + ServerUnixTimeOffset);
        }
        #endregion

        /// <summary>
        /// Overrride this function to send custom data when send enter game message
        /// </summary>
        /// <param name="writer"></param>
        public virtual void SerializeEnterGameData(NetDataWriter writer) { }

        /// <summary>
        /// Override this function to read custom data that come with enter game message
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="reader"></param>
        /// <returns>Return `true` if allow player to enter game.</returns>
        public virtual bool DeserializeEnterGameData(long connectionId, NetDataReader reader) { return true; }

        /// <summary>
        /// Overrride this function to send custom data when send client ready message
        /// </summary>
        /// <param name="writer"></param>
        public virtual void SerializeClientReadyData(NetDataWriter writer) { }

        /// <summary>
        /// Override this function to read custom data that come with client ready message
        /// </summary>
        /// <param name="playerIdentity"></param>
        /// <param name="connectionId"></param>
        /// <param name="reader"></param>
        /// <returns>Return `true` if player is ready to play.</returns>
        public virtual bool DeserializeClientReadyData(LiteNetLibIdentity playerIdentity, long connectionId, NetDataReader reader) { return true; }

        /// <summary>
        /// Override this function to do anything after online scene loaded at server side
        /// </summary>
        public virtual void OnServerOnlineSceneLoaded() { }

        /// <summary>
        /// Override this function to do anything after online scene loaded at client side
        /// </summary>
        public virtual void OnClientOnlineSceneLoaded() { }

        /// <summary>
        /// Override this function to show error message / disconnect
        /// </summary>
        /// <param name="message"></param>
        public virtual void OnServerError(ServerErrorMessage message)
        {
            if (message.shouldDisconnect && !IsServer)
                StopClient();
        }

        public virtual bool SetPlayerReady(long connectionId, NetDataReader reader)
        {
            if (!IsServer)
                return false;

            LiteNetLibPlayer player = Players[connectionId];
            if (player.IsReady)
                return false;

            player.IsReady = true;
            if (!DeserializeClientReadyData(SpawnPlayer(connectionId), connectionId, reader))
            {
                player.IsReady = false;
                return false;
            }

            foreach (LiteNetLibIdentity spawnedObject in Assets.GetSpawnedObjects())
            {
                if (spawnedObject.ConnectionId == player.ConnectionId)
                    continue;

                if (spawnedObject.ShouldAddSubscriber(player))
                    spawnedObject.AddSubscriber(player);
            }
            return true;
        }

        public virtual bool SetPlayerNotReady(long connectionId, NetDataReader reader)
        {
            if (!IsServer)
                return false;

            LiteNetLibPlayer player = Players[connectionId];
            if (!player.IsReady)
                return false;

            player.IsReady = false;
            player.ClearSubscribing(true);
            player.DestroyAllObjects();
            return true;
        }

        protected LiteNetLibIdentity SpawnPlayer(long connectionId)
        {
            if (Assets.PlayerPrefab == null)
                return null;
            return SpawnPlayer(connectionId, Assets.PlayerPrefab);
        }

        protected LiteNetLibIdentity SpawnPlayer(long connectionId, LiteNetLibIdentity prefab)
        {
            if (prefab == null)
                return null;
            return SpawnPlayer(connectionId, prefab.HashAssetId);
        }

        protected LiteNetLibIdentity SpawnPlayer(long connectionId, int hashAssetId)
        {
            LiteNetLibIdentity spawnedObject = Assets.NetworkSpawn(hashAssetId, Assets.GetPlayerSpawnPosition(), Quaternion.identity, 0, connectionId);
            if (spawnedObject != null)
                return spawnedObject;
            return null;
        }
    }
}
