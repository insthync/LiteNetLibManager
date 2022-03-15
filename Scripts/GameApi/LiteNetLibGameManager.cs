using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using LiteNetLib;
using LiteNetLib.Utils;
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

        private float clientSendPingCountDown;
        private float serverSendPingCountDown;
        private AsyncOperation loadSceneAsyncOperation;

        public long ClientConnectionId { get; protected set; }
        private long lastPingTime;
        private long rtt;
        public long Rtt
        {
            get
            {
                if (IsServer)
                    return 0;
                return rtt;
            }
        }
        public long Timestamp { get { return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); } }
        /// <summary>
        /// Unix timestamp (milliseconds) offsets from server
        /// </summary>
        public long ServerTimestampOffsets { get; protected set; }
        /// <summary>
        /// Server unix timestamp (milliseconds)
        /// </summary>
        public long ServerTimestamp
        {
            get
            {
                if (IsServer)
                    return Timestamp;
                return Timestamp + ServerTimestampOffsets;
            }
        }
        public string ServerSceneName { get; protected set; }
        public LiteNetLibAssets Assets { get; protected set; }
        public BaseInterestManager InterestManager { get; protected set; }

        protected virtual void Awake()
        {
            Assets = GetComponent<LiteNetLibAssets>();
            InterestManager = GetComponent<BaseInterestManager>();
            if (InterestManager == null)
                InterestManager = gameObject.AddComponent<DefaultInterestManager>();
            ServerSceneName = string.Empty;
            if (doNotDestroyOnSceneChanges)
                DontDestroyOnLoad(gameObject);
        }

        protected override void FixedUpdate()
        {
            if (loadSceneAsyncOperation == null)
            {
                if (IsClientConnected)
                {
                    // Send ping from client
                    clientSendPingCountDown -= Time.fixedDeltaTime;
                    if (clientSendPingCountDown <= 0f)
                    {
                        SendClientPing();
                        clientSendPingCountDown = pingDuration;
                    }
                }
                if (IsServer)
                {
                    // Send ping from server
                    serverSendPingCountDown -= Time.fixedDeltaTime;
                    if (serverSendPingCountDown <= 0f)
                    {
                        SendServerPing();
                        serverSendPingCountDown = pingDuration;
                    }
                }
            }
            base.FixedUpdate();
        }

        public virtual uint PacketVersion()
        {
            return 8;
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

        public IEnumerable<LiteNetLibPlayer> GetPlayers()
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
        public virtual void ServerSceneChange(string sceneName)
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
                        player.Subscribings.Clear();
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
                    Assets.InitPoolingObjects();
                    if (LogDev) Logging.Log(LogTag, "Loaded Scene: " + sceneName + " -> Assets.Initialize()");
                    if (IsClient)
                    {
                        // If it is host (both client and server) wait for client connection id before proceed server scene load
                        while (ClientConnectionId < 0) await UniTask.Yield();
                    }
                    if (IsServer)
                    {
                        ServerSceneName = sceneName;
                        Assets.SpawnSceneObjects();
                        if (LogDev) Logging.Log(LogTag, "Loaded Scene: " + sceneName + " -> Assets.SpawnSceneObjects()");
                        OnServerOnlineSceneLoaded();
                        if (LogDev) Logging.Log(LogTag, "Loaded Scene: " + sceneName + " -> OnServerOnlineSceneLoaded()");
                        SendServerSceneChange(sceneName);
                        if (LogDev) Logging.Log(LogTag, "Loaded Scene: " + sceneName + " -> SendServerSceneChange()");
                    }
                    if (IsClient)
                    {
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

        protected override void RegisterMessages()
        {
            EnableRequestResponse(GameMsgTypes.Request, GameMsgTypes.Response);
            // Request to server (response to client)
            RegisterRequestToServer<EnterGameRequestMessage, EnterGameResponseMessage>(GameReqTypes.EnterGame, HandleEnterGameRequest, HandleEnterGameResponse);
            RegisterRequestToServer<EmptyMessage, EmptyMessage>(GameReqTypes.ClientReady, HandleClientReadyRequest, HandleClientReadyResponse);
            RegisterRequestToServer<EmptyMessage, EmptyMessage>(GameReqTypes.ClientNotReady, HandleClientNotReadyRequest, HandleClientNotReadyResponse);
            // Server messages
            RegisterServerMessage(GameMsgTypes.CallFunction, HandleClientCallFunction);
            RegisterServerMessage(GameMsgTypes.UpdateSyncField, HandleClientUpdateSyncField);
            RegisterServerMessage(GameMsgTypes.InitialSyncField, HandleClientInitialSyncField);
            RegisterServerMessage(GameMsgTypes.ClientSendTransform, HandleClientSendTransform);
            RegisterServerMessage(GameMsgTypes.Ping, HandleClientPing);
            RegisterServerMessage(GameMsgTypes.Pong, HandleClientPong);
            // Client messages
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
            RegisterClientMessage(GameMsgTypes.Pong, HandleServerPong);
        }

        public override void OnPeerConnected(long connectionId)
        {
            base.OnPeerConnected(connectionId);
            if (!Players.ContainsKey(connectionId))
                Players.Add(connectionId, new LiteNetLibPlayer(this, connectionId));
        }

        public override void OnPeerDisconnected(long connectionId, DisconnectInfo disconnectInfo)
        {
            base.OnPeerDisconnected(connectionId, disconnectInfo);
            if (Players.ContainsKey(connectionId))
            {
                LiteNetLibPlayer player = Players[connectionId];
                player.ClearSubscribing(false);
                player.DestroyObjectsWhenDisconnect();
                Players.Remove(connectionId);
            }
        }

        public override void OnClientConnected()
        {
            base.OnClientConnected();
            // Reset client connection id, will be received from server later
            ClientConnectionId = -1;
            rtt = 0;
            if (!doNotEnterGameOnConnect)
                SendClientEnterGame();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Reset client connection id, will be received from server later
            ClientConnectionId = -1;
            rtt = 0;
            if (!Assets.onlineScene.IsSet() || Assets.onlineScene.SceneName.Equals(SceneManager.GetActiveScene().name))
            {
                ServerSceneName = SceneManager.GetActiveScene().name;
                Assets.Initialize();
                Assets.SpawnSceneObjects();
                Assets.InitPoolingObjects();
                OnServerOnlineSceneLoaded();
            }
            else
            {
                ServerSceneName = Assets.onlineScene.SceneName;
                LoadSceneRoutine(Assets.onlineScene.SceneName, true).Forget();
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerSceneName = string.Empty;
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
            ClientSendRequest(GameReqTypes.EnterGame, new EnterGameRequestMessage()
            {
                packetVersion = PacketVersion(),
            }, extraRequestSerializer: SerializeEnterGameData);
        }

        public void SendClientReady()
        {
            if (!IsClientConnected)
                return;
            ClientSendRequest(GameReqTypes.ClientReady, EmptyMessage.Value, extraRequestSerializer: SerializeClientReadyData);
        }

        public void SendClientNotReady()
        {
            if (!IsClientConnected)
                return;
            ClientSendRequest(GameReqTypes.ClientNotReady, EmptyMessage.Value);
        }

        public void SendClientPing()
        {
            if (!IsClientConnected)
                return;
            ClientSendPacket(0, DeliveryMethod.Unreliable, GameMsgTypes.Ping, new PingMessage()
            {
                pingTime = Timestamp,
            });
        }

        public void SendServerPing()
        {
            if (!IsServer)
                return;
            ServerSendPacketToAllConnections(0, DeliveryMethod.Unreliable, GameMsgTypes.Ping, new PingMessage()
            {
                pingTime = Timestamp,
            });
        }

        public bool SendServerSpawnSceneObject(long connectionId, LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return false;
            LiteNetLibPlayer player;
            if (!Players.TryGetValue(connectionId, out player) || !player.IsReady)
                return false;
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerSpawnSceneObject, new ServerSpawnSceneObjectMessage()
            {
                objectId = identity.ObjectId,
                connectionId = identity.ConnectionId,
                position = identity.transform.position,
                rotation = identity.transform.rotation,
            }, identity.WriteInitialSyncFields);
            return true;
        }

        public bool SendServerSpawnObject(long connectionId, LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return false;
            LiteNetLibPlayer player;
            if (!Players.TryGetValue(connectionId, out player) || !player.IsReady)
                return false;
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerSpawnObject, new ServerSpawnObjectMessage()
            {
                hashAssetId = identity.HashAssetId,
                objectId = identity.ObjectId,
                connectionId = identity.ConnectionId,
                position = identity.transform.position,
                rotation = identity.transform.rotation,
            }, identity.WriteInitialSyncFields);
            return true;
        }

        public void SendServerSpawnObjectWithData(long connectionId, LiteNetLibIdentity identity)
        {
            if (identity == null)
                return;

            bool spawnObjectSent;
            if (Assets.ContainsSceneObject(identity.ObjectId))
                spawnObjectSent = SendServerSpawnSceneObject(connectionId, identity);
            else
                spawnObjectSent = SendServerSpawnObject(connectionId, identity);
            if (spawnObjectSent)
            {
                identity.SendInitSyncFields(connectionId);
                identity.SendInitSyncLists(connectionId);
            }
        }

        public bool SendServerDestroyObject(long connectionId, uint objectId, byte reasons)
        {
            if (!IsServer)
                return false;
            LiteNetLibPlayer player;
            if (!Players.TryGetValue(connectionId, out player) || !player.IsReady)
                return false;
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerDestroyObject, new ServerDestroyObjectMessage()
            {
                objectId = objectId,
                reasons = reasons,
            });
            return true;
        }

        public void SendServerError(bool shouldDisconnect, string errorMessage)
        {
            if (!IsServer)
                return;
            foreach (long connectionId in Server.ConnectionIds)
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
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerDestroyObject, new ServerErrorMessage()
            {
                shouldDisconnect = shouldDisconnect,
                errorMessage = errorMessage,
            });
        }

        public void SendServerSceneChange(string sceneName)
        {
            if (!IsServer)
                return;
            foreach (long connectionId in Server.ConnectionIds)
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
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerSceneChange, new ServerSceneChangeMessage()
            {
                serverSceneName = sceneName,
            });
        }

        public void SendServerSetObjectOwner(long connectionId, uint objectId, long ownerConnectionId)
        {
            if (!IsServer)
                return;
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerSetObjectOwner, new ServerSetObjectOwner()
            {
                objectId = objectId,
                connectionId = ownerConnectionId,
            });
        }
        #endregion

        #region Message Handlers

        protected virtual async UniTaskVoid HandleEnterGameRequest(
            RequestHandlerData requestHandler,
            EnterGameRequestMessage request,
            RequestProceedResultDelegate<EnterGameResponseMessage> result)
        {
            AckResponseCode responseCode = AckResponseCode.Error;
            EnterGameResponseMessage response = new EnterGameResponseMessage();
            if (request.packetVersion == PacketVersion() &&
                await DeserializeEnterGameData(requestHandler.ConnectionId, requestHandler.Reader))
            {
                responseCode = AckResponseCode.Success;
                response.connectionId = requestHandler.ConnectionId;
                response.serverSceneName = ServerSceneName;
            }
            result.Invoke(responseCode, response);
        }

        protected virtual void HandleEnterGameResponse(
            ResponseHandlerData responseHandler,
            AckResponseCode responseCode,
            EnterGameResponseMessage response)
        {
            if (responseCode == AckResponseCode.Success)
            {
                ClientConnectionId = response.connectionId;
                if (IsClientConnected)
                    HandleServerSceneChange(response.serverSceneName);
            }
            else
            {
                if (LogError) Logging.LogError(LogTag, "Enter game request was refused by server, disconnecting...");
                StopClient();
            }
        }

        protected virtual async UniTaskVoid HandleClientReadyRequest(
            RequestHandlerData requestHandler,
            EmptyMessage request,
            RequestProceedResultDelegate<EmptyMessage> result)
        {
            AckResponseCode responseCode = AckResponseCode.Error;
            if (await SetPlayerReady(requestHandler.ConnectionId, requestHandler.Reader))
            {
                responseCode = AckResponseCode.Success;
            }
            result.Invoke(responseCode, EmptyMessage.Value);
        }

        protected virtual void HandleClientReadyResponse(
            ResponseHandlerData responseHandler,
            AckResponseCode responseCode,
            EmptyMessage response)
        {
            // Override this function to do something by response code
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
            result.Invoke(responseCode, EmptyMessage.Value);
            return default;
        }

        protected void HandleClientNotReadyResponse(
            ResponseHandlerData responseHandler,
            AckResponseCode responseCode,
            EmptyMessage response)
        {
            // Override this function to do something by response code
        }

        protected virtual void HandleClientInitialSyncField(MessageHandlerData messageHandler)
        {
            // Field updated at owner-client, if this is server then multicast message to other clients
            if (!IsServer)
                return;
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(messageHandler.Reader);
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
                        syncField.Deserialize(messageHandler.Reader, true);
                    // Send to other clients
                    foreach (long connectionId in Server.ConnectionIds)
                    {
                        // Don't send the update to owner client because it was updated before send update to server
                        if (connectionId == messageHandler.ConnectionId)
                            continue;
                        // Send update to clients except owner client
                        if (identity.HasSubscriberOrIsOwning(connectionId))
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
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(messageHandler.Reader);
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
                        syncField.Deserialize(messageHandler.Reader, false);
                    // Send to other clients
                    foreach (long connectionId in Server.ConnectionIds)
                    {
                        // Don't send the update to owner client because it was updated before send update to server
                        if (connectionId == messageHandler.ConnectionId)
                            continue;
                        // Send update to clients except owner client
                        if (identity.HasSubscriberOrIsOwning(connectionId))
                            syncField.SendUpdate(false, connectionId);
                    }
                }
            }
        }

        protected virtual void HandleClientCallFunction(MessageHandlerData messageHandler)
        {
            FunctionReceivers receivers = (FunctionReceivers)messageHandler.Reader.GetByte();
            long connectionId = -1;
            if (receivers == FunctionReceivers.Target)
                connectionId = messageHandler.Reader.GetPackedLong();
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(messageHandler.Reader);
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
                    identity.ProcessNetFunction(netFunction, messageHandler.Reader, true);
                }
                else
                {
                    // Request from client to other clients, so hook callback later
                    identity.ProcessNetFunction(netFunction, messageHandler.Reader, false);
                    // Use call with out parameters set because parameters already set while process net function
                    if (receivers == FunctionReceivers.Target)
                        netFunction.CallWithoutParametersSet(connectionId);
                    else
                        netFunction.CallWithoutParametersSet(receivers);
                }
            }
        }

        protected virtual void HandleClientSendTransform(MessageHandlerData messageHandler)
        {
            uint objectId = messageHandler.Reader.GetPackedUInt();
            byte behaviourIndex = messageHandler.Reader.GetByte();
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(objectId, out identity))
            {
                LiteNetLibTransform netTransform;
                if (identity.TryGetBehaviour(behaviourIndex, out netTransform))
                    netTransform.HandleClientSendTransform(messageHandler.Reader);
            }
        }

        protected void HandleClientPing(MessageHandlerData messageHandler)
        {
            PingMessage message = messageHandler.ReadMessage<PingMessage>();
            ServerSendPacket(messageHandler.ConnectionId, 0, DeliveryMethod.Unreliable, GameMsgTypes.Pong, new PongMessage()
            {
                pingTime = message.pingTime,
                serverTime = Timestamp,
            });
        }

        protected void HandleClientPong(MessageHandlerData messageHandler)
        {
            if (!Players.ContainsKey(messageHandler.ConnectionId))
                return;
            PongMessage message = messageHandler.ReadMessage<PongMessage>();
            if (Players[messageHandler.ConnectionId].LastPingTime < message.pingTime)
            {
                Players[messageHandler.ConnectionId].LastPingTime = message.pingTime;
                Players[messageHandler.ConnectionId].Rtt = Timestamp - message.pingTime;
            }
        }

        protected virtual void HandleServerSpawnSceneObject(MessageHandlerData messageHandler)
        {
            ServerSpawnSceneObjectMessage message = messageHandler.ReadMessage<ServerSpawnSceneObjectMessage>();
            if (!IsServer)
                Assets.NetworkSpawnScene(message.objectId, message.position, message.rotation, message.connectionId);
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
            {
                Assets.NetworkDestroy(message.objectId, message.reasons);
            }
            else
            {
                LiteNetLibIdentity identity;
                if (Assets.TryGetSpawnedObject(message.objectId, out identity))
                    identity.OnServerSubscribingRemoved();
            }
        }

        protected virtual void HandleServerInitialSyncField(MessageHandlerData messageHandler)
        {
            // Field updated at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(messageHandler.Reader);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(info.objectId, out identity))
                identity.ProcessSyncField(info, messageHandler.Reader, true);
        }

        protected virtual void HandleServerUpdateSyncField(MessageHandlerData messageHandler)
        {
            // Field updated at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(messageHandler.Reader);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(info.objectId, out identity))
                identity.ProcessSyncField(info, messageHandler.Reader, false);
        }

        protected virtual void HandleServerCallFunction(MessageHandlerData messageHandler)
        {
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(messageHandler.Reader);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(info.objectId, out identity))
            {
                // All function from server will be processed (because it's always trust server)
                identity.ProcessNetFunction(info, messageHandler.Reader, true);
            }
        }

        protected virtual void HandleServerUpdateSyncList(MessageHandlerData messageHandler)
        {
            // List updated at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            LiteNetLibElementInfo info = LiteNetLibElementInfo.DeserializeInfo(messageHandler.Reader);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(info.objectId, out identity))
                identity.ProcessSyncList(info, messageHandler.Reader);
        }

        protected virtual void HandleServerSyncBehaviour(MessageHandlerData messageHandler)
        {
            // Behaviour sync from server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            uint objectId = messageHandler.Reader.GetPackedUInt();
            byte behaviourIndex = messageHandler.Reader.GetByte();
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(objectId, out identity))
                identity.ProcessSyncBehaviour(behaviourIndex, messageHandler.Reader);
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

        protected void HandleServerSceneChange(string serverSceneName)
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
                Assets.InitPoolingObjects();
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
            // Object owner was set at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;
            ServerSetObjectOwner message = messageHandler.ReadMessage<ServerSetObjectOwner>();
            Assets.SetObjectOwner(message.objectId, message.connectionId);
        }

        protected void HandleServerPing(MessageHandlerData messageHandler)
        {
            PingMessage message = messageHandler.ReadMessage<PingMessage>();
            // Send pong back to server (then server will calculates Rtt for this client later)
            ClientSendPacket(0, DeliveryMethod.Unreliable, GameMsgTypes.Pong, new PongMessage()
            {
                pingTime = message.pingTime,
            });
        }

        protected void HandleServerPong(MessageHandlerData messageHandler)
        {
            PongMessage message = messageHandler.ReadMessage<PongMessage>();
            if (lastPingTime < message.pingTime)
            {
                lastPingTime = message.pingTime;
                rtt = Timestamp - message.pingTime;
                // Calculate time offsets by device time offsets and RTT
                ServerTimestampOffsets = (long)(message.serverTime - Timestamp + (Rtt * 0.5f));
                if (LogDev) Logging.Log(LogTag, "Rtt: " + Rtt + ", ServerTimestampOffsets: " + ServerTimestampOffsets);
            }
        }
        #endregion

        /// <summary>
        /// Overrride this function to send custom data when send enter game message
        /// </summary>
        /// <param name="writer"></param>
        public virtual void SerializeEnterGameData(NetDataWriter writer)
        {

        }

        /// <summary>
        /// Override this function to read custom data that come with enter game message
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="reader"></param>
        /// <returns>Return `true` if allow player to enter game.</returns>
        public virtual async UniTask<bool> DeserializeEnterGameData(long connectionId, NetDataReader reader)
        {
            await UniTask.Yield();
            return true;
        }
        /// <summary>
        /// Overrride this function to send custom data when send client ready message
        /// </summary>
        /// <param name="writer"></param>
        public virtual void SerializeClientReadyData(NetDataWriter writer)
        {

        }

        /// <summary>
        /// Override this function to read custom data that come with client ready message
        /// </summary>
        /// <param name="playerIdentity"></param>
        /// <param name="connectionId"></param>
        /// <param name="reader"></param>
        /// <returns>Return `true` if player is ready to play.</returns>
        public virtual async UniTask<bool> DeserializeClientReadyData(LiteNetLibIdentity playerIdentity, long connectionId, NetDataReader reader)
        {
            await UniTask.Yield();
            return true;
        }

        /// <summary>
        /// Override this function to do anything after online scene loaded at server side
        /// </summary>
        public virtual void OnServerOnlineSceneLoaded()
        {

        }

        /// <summary>
        /// Override this function to do anything after online scene loaded at client side
        /// </summary>
        public virtual void OnClientOnlineSceneLoaded()
        {

        }

        /// <summary>
        /// Override this function to show error message / disconnect
        /// </summary>
        /// <param name="message"></param>
        public virtual void OnServerError(ServerErrorMessage message)
        {
            if (message.shouldDisconnect && !IsServer)
                StopClient();
        }

        public virtual async UniTask<bool> SetPlayerReady(long connectionId, NetDataReader reader)
        {
            if (!IsServer)
                return false;

            LiteNetLibPlayer player = Players[connectionId];
            if (player.IsReady)
                return false;
            player.IsReady = true;
            if (!await DeserializeClientReadyData(SpawnPlayer(connectionId), connectionId, reader))
            {
                player.IsReady = false;
                return false;
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
            player.DestroyObjectsWhenNotReady();
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
