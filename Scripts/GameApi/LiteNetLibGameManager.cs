﻿using Cysharp.Threading.Tasks;
using Insthync.AddressableAssetTools;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace LiteNetLibManager
{
    [RequireComponent(typeof(LiteNetLibAssets))]
    public class LiteNetLibGameManager : LiteNetLibManager
    {
        public struct ServerSceneLoadingInfo
        {
            public ServerSceneInfo sceneInfo;
            public bool isOnline;
        }

        [Header("Game manager configs")]
        public uint packetVersion = 1;
        public float pingDuration = 1f;
        public bool doNotEnterGameOnConnect = false;
        public bool doNotReadyOnSceneLoaded = false;
        public bool doNotDestroyOnSceneChanges = false;
        public bool loadOfflineSceneWhenClientStopped = true;

        protected readonly Dictionary<long, LiteNetLibPlayer> Players = new Dictionary<long, LiteNetLibPlayer>();

        private double _clientSendPingCountDown;
        private double _serverSendPingCountDown;
        public static List<ServerSceneLoadingInfo> LoadingServerScenes { get; private set; } = new List<ServerSceneLoadingInfo>();
        public int LoadedAdditiveScenesCount { get; internal set; } = 0;
        public int TotalAdditiveScensCount { get; protected set; } = 0;

        public long ClientConnectionId { get; protected set; }
        public RttCalculator RttCalculator { get; protected set; } = new RttCalculator();
        public long Rtt
        {
            get
            {
                if (IsServer)
                    return 0;
                return RttCalculator.Rtt;
            }
        }
        /// <summary>
        /// Server unix timestamp (milliseconds)
        /// </summary>
        public long ServerTimestamp
        {
            get
            {
                if (IsServer)
                    return RttCalculator.LocalTimestamp;
                return RttCalculator.PeerTimestamp;
            }
        }
        public ServerSceneInfo? ServerSceneInfo { get; protected set; } = null;
        public LiteNetLibAssets Assets { get; protected set; }

        protected BaseInterestManager _interestManager;
        public BaseInterestManager InterestManager
        {
            get { return _interestManager; }
            set
            {
                if (value == null)
                    return;
                if (value == _interestManager)
                    return;
                _interestManager = value;
                _interestManager.Setup(this);
            }
        }

        protected BaseInterestManager _defaultInterestManager;
        /// <summary>
        /// Dictionary[ChannelID:byte, HashSet[LiteNetLibSyncElement]]
        /// </summary>
        protected readonly Dictionary<byte, HashSet<LiteNetLibSyncElement>> _updatingClientSyncElements = new Dictionary<byte, HashSet<LiteNetLibSyncElement>>();
        /// <summary>
        /// Dictionary[ChannelID:byte, HashSet[LiteNetLibSyncElement]]
        /// </summary>
        protected readonly Dictionary<byte, HashSet<LiteNetLibSyncElement>> _updatingServerSyncElements = new Dictionary<byte, HashSet<LiteNetLibSyncElement>>();
        protected NetDataWriter _syncElementsWriter = new NetDataWriter(true, 1500);

        protected virtual void Awake()
        {
            Assets = GetComponent<LiteNetLibAssets>();
            _defaultInterestManager = GetComponent<BaseInterestManager>();
            if (_defaultInterestManager == null)
                _defaultInterestManager = gameObject.AddComponent<DefaultInterestManager>();
            InterestManager = _defaultInterestManager;
            ServerSceneInfo = null;
            if (doNotDestroyOnSceneChanges)
                DontDestroyOnLoad(gameObject);
        }

        protected override void OnServerUpdate(LogicUpdater updater)
        {
            ProceedSyncElements(_updatingServerSyncElements, true);
            // Send ping from server
            _serverSendPingCountDown -= updater.DeltaTime;
            if (_serverSendPingCountDown <= 0f)
            {
                SendServerPing();
                _serverSendPingCountDown = pingDuration;
            }
            if (InterestManager == null)
                InterestManager = _defaultInterestManager;
            InterestManager.UpdateInterestManagement(updater.DeltaTimeF);
        }

        protected override void OnClientUpdate(LogicUpdater updater)
        {
            if (!IsServer)
                ProceedSyncElements(_updatingClientSyncElements, false);
            // Send ping from client
            _clientSendPingCountDown -= updater.DeltaTime;
            if (_clientSendPingCountDown <= 0f)
            {
                SendClientPing();
                _clientSendPingCountDown = pingDuration;
            }
        }

        private void ProceedSyncElements(Dictionary<byte, HashSet<LiteNetLibSyncElement>> collection, bool isServer)
        {
            foreach (var kv in collection)
            {
                byte syncChannelId = kv.Key;
                // Send to players
                foreach (long connectionId in Server.ConnectionIds)
                {
                    _syncElementsWriter.Reset();
                    // Message Type ID
                    _syncElementsWriter.PutPackedUShort(GameMsgTypes.SyncElements);
                    // Reserve position for element count
                    int positionToWriteSyncElementsCount = _syncElementsWriter.Length;
                    _syncElementsWriter.Put(0);

                    int tempPositionBeforeWrite;
                    int syncElementsCount = 0;
                    foreach (var element in kv.Value)
                    {
                        if (!element.WillSyncData(connectionId))
                            continue;
                        // Write element info
                        _syncElementsWriter.PutPackedUInt(element.ObjectId);
                        _syncElementsWriter.PutPackedInt(element.ElementId);
                        // Reserve position for data length
                        int positionToWriteDataLength = _syncElementsWriter.Length;
                        _syncElementsWriter.Put(0);
                        // Write sync data
                        element.WriteSyncData(_syncElementsWriter);
                        int dataLength = _syncElementsWriter.Length - positionToWriteDataLength - 1;
                        // Put data length
                        tempPositionBeforeWrite = _syncElementsWriter.Length;
                        _syncElementsWriter.SetPosition(positionToWriteDataLength);
                        _syncElementsWriter.Put(dataLength);
                        _syncElementsWriter.SetPosition(tempPositionBeforeWrite);
                        // Increase sync element count
                        syncElementsCount++;
                    }

                    // Put element count
                    tempPositionBeforeWrite = _syncElementsWriter.Length;
                    _syncElementsWriter.SetPosition(positionToWriteSyncElementsCount);
                    _syncElementsWriter.Put(syncElementsCount);
                    _syncElementsWriter.SetPosition(tempPositionBeforeWrite);
                    // Send sync elements
                    if (isServer)
                        ServerSendMessage(connectionId, syncChannelId, DeliveryMethod.ReliableOrdered, _syncElementsWriter);
                }
                kv.Value.Clear();
            }
        }

        internal void RegisterServerSyncElement(LiteNetLibSyncElement element)
        {
            RegisterSyncElement(_updatingServerSyncElements, element);
        }

        internal void UnregisterServerSyncElement(LiteNetLibSyncElement element)
        {
            UnregisterSyncElement(_updatingServerSyncElements, element);
        }

        internal void RegisterClientSyncElement(LiteNetLibSyncElement element)
        {
            RegisterSyncElement(_updatingClientSyncElements, element);
        }

        internal void UnregisterClientSyncElement(LiteNetLibSyncElement element)
        {
            UnregisterSyncElement(_updatingClientSyncElements, element);
        }

        private void RegisterSyncElement(Dictionary<byte, HashSet<LiteNetLibSyncElement>> collection, LiteNetLibSyncElement element)
        {
            if (element == null)
                return;
            byte channelId = element.SyncChannelId;
            if (!collection.TryGetValue(channelId, out HashSet<LiteNetLibSyncElement> elements))
            {
                elements = new HashSet<LiteNetLibSyncElement>();
                collection[channelId] = elements;
            }
            elements.Add(element);
        }

        private void UnregisterSyncElement(Dictionary<byte, HashSet<LiteNetLibSyncElement>> collection, LiteNetLibSyncElement element)
        {
            if (element == null)
                return;
            byte channelId = element.SyncChannelId;
            if (!collection.TryGetValue(channelId, out HashSet<LiteNetLibSyncElement> elements))
                return;
            elements.Remove(element);
        }

        public virtual uint PacketVersion()
        {
            return packetVersion;
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
        /// <param name="serverSceneInfo"></param>
        public virtual void ServerSceneChange(ServerSceneInfo serverSceneInfo)
        {
            if (!IsServer)
                return;
            LoadSceneRoutine(serverSceneInfo, true).Forget();
        }

        /// <summary>
        /// This function will be called to load scene async
        /// </summary>
        /// <param name="serverSceneInfo"></param>
        /// <param name="isOnline"></param>
        /// <returns></returns>
        private async UniTaskVoid LoadSceneRoutine(ServerSceneInfo serverSceneInfo, bool isOnline)
        {
            if (IsServer)
                ServerSceneInfo = null;

            if (LoadingServerScenes.Count > 0)
            {
                ServerSceneLoadingInfo prevLoadingInfo = LoadingServerScenes[0];

                if (prevLoadingInfo.sceneInfo.isAddressable && serverSceneInfo.isAddressable &&
                    string.Equals(prevLoadingInfo.sceneInfo.addressableKey, serverSceneInfo.addressableKey) &&
                    prevLoadingInfo.isOnline == isOnline)
                {
                    // Loading the same scene, don't do anything
                    return;
                }

                if (!prevLoadingInfo.sceneInfo.isAddressable && !serverSceneInfo.isAddressable &&
                    string.Equals(prevLoadingInfo.sceneInfo.sceneName, serverSceneInfo.sceneName) &&
                    prevLoadingInfo.isOnline == isOnline)
                {
                    // Loading the same scene, don't do anything
                    return;
                }
            }

            LoadingServerScenes.Add(new ServerSceneLoadingInfo()
            {
                sceneInfo = serverSceneInfo,
                isOnline = isOnline,
            });

            // Must have only 2 scenes, remove middle ones
            while (LoadingServerScenes.Count > 2)
            {
                LoadingServerScenes.RemoveAt(1);
            }

            // If doNotDestroyOnSceneChanges not TRUE still not destroy this game object
            // But it will be destroyed after scene loaded, if scene is offline scene
            if (!doNotDestroyOnSceneChanges)
                DontDestroyOnLoad(gameObject);

            if (isOnline)
            {
                foreach (LiteNetLibPlayer player in Players.Values)
                {
                    player.IsReady = false;
                    player.Subscribings.Clear();
                    player.SpawnedObjects.Clear();
                }
                Assets.Clear(true);
            }

            // Reset additive scenes count
            LoadedAdditiveScenesCount = 0;
            TotalAdditiveScensCount = 0;

            if (LogDev) Logging.Log(LogTag, $"Loading Scene: {serverSceneInfo.isAddressable} {serverSceneInfo.sceneName} is online: {isOnline}");
            Assets.onLoadSceneStart.Invoke(serverSceneInfo.sceneName, false, isOnline, 0f);
            if (serverSceneInfo.isAddressable)
            {
                // Download the scene
                await AddressableAssetDownloadManager.Download(
                    serverSceneInfo.addressableKey,
                    Assets.onSceneFileSizeRetrieving.Invoke,
                    Assets.onSceneFileSizeRetrieved.Invoke,
                    Assets.onSceneDepsDownloading.Invoke,
                    Assets.onSceneDepsFileDownloading.Invoke,
                    Assets.onSceneDepsDownloaded.Invoke,
                    null);
                // Load the scene
                AsyncOperationHandle<SceneInstance> asyncOp = Addressables.LoadSceneAsync(
                    serverSceneInfo.addressableKey,
                    new LoadSceneParameters(LoadSceneMode.Single));
                // Wait until scene loaded
                while (!asyncOp.IsDone)
                {
                    await UniTask.Yield();
                    float percentageComplete = asyncOp.GetDownloadStatus().Percent;
                    Assets.onLoadSceneProgress.Invoke(serverSceneInfo.sceneName, false, isOnline, percentageComplete);
                }
            }
            else
            {
                // Load the scene
                AsyncOperation asyncOp = SceneManager.LoadSceneAsync(
                    serverSceneInfo.sceneName,
                    new LoadSceneParameters(LoadSceneMode.Single));
                // Wait until scene loaded
                while (asyncOp != null && !asyncOp.isDone)
                {
                    await UniTask.Yield();
                    Assets.onLoadSceneProgress.Invoke(serverSceneInfo.sceneName, false, isOnline, asyncOp.progress);
                }
            }

            // Clear unused assets after new scene loaded
            AddressableAssetsManager.ReleaseAll();
            await Resources.UnloadUnusedAssets();

            // If scene changed while loading, have to load the new one
            LoadingServerScenes.RemoveAt(0);
            if (LoadingServerScenes.Count <= 0)
            {
                // Spawn additive scenes
                for (int i = 0; i < SceneManager.loadedSceneCount; ++i)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    // Load additive scenes
                    List<LiteNetLibAdditiveSceneLoader> listOfLoaders = new List<LiteNetLibAdditiveSceneLoader>();
                    GameObject[] rootGameObjects = scene.GetRootGameObjects();
                    for (int j = 0; j < rootGameObjects.Length; ++j)
                    {
                        if (!rootGameObjects[j].activeSelf)
                            continue;
                        listOfLoaders.AddRange(rootGameObjects[j].GetComponentsInChildren<LiteNetLibAdditiveSceneLoader>(false));
                    }
                    for (int j = 0; j < listOfLoaders.Count; ++j)
                    {
                        if (listOfLoaders[j].scenes != null)
                            TotalAdditiveScensCount += listOfLoaders[j].scenes.Length;
                        if (listOfLoaders[j].addressableScenes != null)
                            TotalAdditiveScensCount += listOfLoaders[j].addressableScenes.Length;
                    }
                    Assets.onLoadAdditiveSceneStart.Invoke(LoadedAdditiveScenesCount, TotalAdditiveScensCount);
                    for (int j = 0; j < listOfLoaders.Count; ++j)
                    {
                        await listOfLoaders[i].LoadAll(this, serverSceneInfo.isAddressable ? serverSceneInfo.addressableKey : serverSceneInfo.sceneName, isOnline);
                    }
                    Assets.onLoadAdditiveSceneFinish.Invoke(LoadedAdditiveScenesCount, TotalAdditiveScensCount);
                }
            }
            else
            {
                ServerSceneLoadingInfo nextLoadingInfo = LoadingServerScenes[LoadingServerScenes.Count - 1];
                LoadSceneRoutine(nextLoadingInfo.sceneInfo, nextLoadingInfo.isOnline).Forget();
                return;
            }

            if (isOnline)
            {
                // Proceed online scene loaded
                await ProceedOnlineSceneLoaded(serverSceneInfo);
            }
            else if (!doNotDestroyOnSceneChanges)
            {
                // Destroy manager's game object if loaded scene is not online scene
                Destroy(gameObject);
            }

            if (LogDev) Logging.Log(LogTag, $"Loaded Scene: {serverSceneInfo.isAddressable} {serverSceneInfo.sceneName} is online: {isOnline}");
            Assets.onLoadSceneFinish.Invoke(serverSceneInfo.sceneName, false, isOnline, 1f);
        }

        protected async UniTask ProceedOnlineSceneLoaded(ServerSceneInfo serverSceneInfo)
        {
            await UniTask.Yield();
            if (LogDev) Logging.Log(LogTag, $"Loaded Scene: {serverSceneInfo.isAddressable} {serverSceneInfo.sceneName} -> Assets.Initialize()");
            await Assets.Initialize();
            Assets.InitPoolingObjects();
            if (IsClient)
            {
                // If it is host (both client and server) wait for client connection id before proceed server scene load
                do { await UniTask.Delay(25); } while (ClientConnectionId < 0);
            }
            if (IsServer)
            {
                ServerSceneInfo = serverSceneInfo;
                if (LogDev) Logging.Log(LogTag, $"Loaded Scene: {serverSceneInfo.isAddressable} {serverSceneInfo.sceneName} -> Assets.SpawnSceneObjects()");
                Assets.SpawnSceneObjects();
                if (LogDev) Logging.Log(LogTag, $"Loaded Scene: {serverSceneInfo.isAddressable} {serverSceneInfo.sceneName} -> OnServerOnlineSceneLoaded()");
                OnServerOnlineSceneLoaded();
                if (LogDev) Logging.Log(LogTag, $"Loaded Scene: {serverSceneInfo.isAddressable} {serverSceneInfo.sceneName} -> SendServerSceneChange()");
                SendServerSceneChange(serverSceneInfo);
            }
            if (IsClient)
            {
                if (LogDev) Logging.Log(LogTag, $"Loaded Scene: {serverSceneInfo.isAddressable} {serverSceneInfo.sceneName} -> OnClientOnlineSceneLoaded()");
                OnClientOnlineSceneLoaded();
                if (!doNotReadyOnSceneLoaded)
                {
                    if (LogDev) Logging.Log(LogTag, $"Loaded Scene: {serverSceneInfo.isAddressable} {serverSceneInfo.sceneName} -> SendClientReady()");
                    SendClientReady();
                }
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
            RegisterServerMessage(GameMsgTypes.Ping, HandleClientPing);
            RegisterServerMessage(GameMsgTypes.Pong, HandleClientPong);
            // TODO: Server handle sync elements
            // Client messages
            RegisterClientMessage(GameMsgTypes.ServerSpawnSceneObject, HandleServerSpawnSceneObject);
            RegisterClientMessage(GameMsgTypes.ServerSpawnObject, HandleServerSpawnObject);
            RegisterClientMessage(GameMsgTypes.ServerDestroyObject, HandleServerDestroyObject);
            RegisterClientMessage(GameMsgTypes.CallFunction, HandleServerCallFunction);
            RegisterClientMessage(GameMsgTypes.OperateSyncList, HandleServerUpdateSyncList);
            RegisterClientMessage(GameMsgTypes.SyncElements, HandleServerSyncElements);
            RegisterClientMessage(GameMsgTypes.ServerError, HandleServerError);
            RegisterClientMessage(GameMsgTypes.ServerSceneChange, HandleServerSceneChange);
            RegisterClientMessage(GameMsgTypes.ServerSetObjectOwner, HandleServerSetObjectOwner);
            RegisterClientMessage(GameMsgTypes.Ping, HandleServerPing);
            RegisterClientMessage(GameMsgTypes.Pong, HandleServerPong);
            RegisterClientMessage(GameMsgTypes.Disconnect, HandleServerDisconnect);
        }

        public async void KickClient(long connectionId, byte[] data)
        {
            if (!IsServer)
                return;
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.Disconnect, (writer) => writer.PutBytesWithLength(data));
            await UniTask.Delay(500);
            ServerTransport.ServerDisconnect(connectionId);
        }

        public override void OnPeerConnected(long connectionId)
        {
            base.OnPeerConnected(connectionId);
            if (!Players.ContainsKey(connectionId))
                Players.Add(connectionId, new LiteNetLibPlayer(this, connectionId));
        }

        public override void OnPeerDisconnected(long connectionId, DisconnectReason reason, SocketError socketError)
        {
            base.OnPeerDisconnected(connectionId, reason, socketError);
            if (!Players.TryGetValue(connectionId, out LiteNetLibPlayer player))
                return;
            player.ClearSubscribing(false);
            player.DestroyObjectsWhenDisconnect();
            Players.Remove(connectionId);
        }

        public override void OnClientConnected()
        {
            base.OnClientConnected();
            // Reset client connection id, will be received from server later
            ClientConnectionId = -1;
            RttCalculator.Reset();
            if (!doNotEnterGameOnConnect)
                SendClientEnterGame();
            SendClientPing();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Reset client connection id, will be received from server later
            ClientConnectionId = -1;
            RttCalculator.Reset();
            _updatingServerSyncElements.Clear();
            _updatingClientSyncElements.Clear();

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (Assets.addressableOnlineScene.IsDataValid() && !Assets.addressableOnlineScene.IsSameSceneName(activeSceneName))
            {
                LoadSceneRoutine(Assets.addressableOnlineScene.GetServerSceneInfo(), true).Forget();
            }
#if !EXCLUDE_PREFAB_REFS
            else if (Assets.onlineScene.IsDataValid() && !Assets.onlineScene.IsSameSceneName(activeSceneName))
            {
                LoadSceneRoutine(Assets.onlineScene.GetServerSceneInfo(), true).Forget();
            }
#endif
            else
            {
                ProceedOnlineSceneLoaded(new ServerSceneInfo()
                {
                    isAddressable = false,
                    sceneName = activeSceneName,
                }).Forget();
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerSceneInfo = null;
            Players.Clear();
            Assets.Clear();
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (Assets.addressableOfflineScene.IsDataValid() && !Assets.addressableOfflineScene.IsSameSceneName(activeSceneName))
            {
                LoadSceneRoutine(Assets.addressableOfflineScene.GetServerSceneInfo(), false).Forget();
            }
#if !EXCLUDE_PREFAB_REFS
            else if (Assets.offlineScene.IsDataValid() && !Assets.offlineScene.IsSameSceneName(activeSceneName))
            {
                LoadSceneRoutine(Assets.offlineScene.GetServerSceneInfo(), false).Forget();
            }
#endif
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsServer)
            {
                Players.Clear();
                Assets.Clear();
                if (loadOfflineSceneWhenClientStopped)
                {
                    string activeSceneName = SceneManager.GetActiveScene().name;
                    if (Assets.addressableOfflineScene.IsDataValid() && !Assets.addressableOfflineScene.IsSameSceneName(activeSceneName))
                    {
                        LoadSceneRoutine(Assets.addressableOfflineScene.GetServerSceneInfo(), false).Forget();
                    }
#if !EXCLUDE_PREFAB_REFS
                    else if (Assets.offlineScene.IsDataValid() && !Assets.offlineScene.IsSameSceneName(activeSceneName))
                    {
                        LoadSceneRoutine(Assets.offlineScene.GetServerSceneInfo(), false).Forget();
                    }
#endif
                }
            }
        }

        #region Send messages functions
        public virtual void SendClientEnterGame()
        {
            if (!IsClientConnected)
                return;
            ClientSendRequest(GameReqTypes.EnterGame, new EnterGameRequestMessage()
            {
                packetVersion = PacketVersion(),
            }, extraRequestSerializer: SerializeEnterGameData);
        }

        public virtual void SendClientReady()
        {
            if (!IsClientConnected)
                return;
            ClientSendRequest(GameReqTypes.ClientReady, EmptyMessage.Value, extraRequestSerializer: SerializeClientReadyData);
        }

        public virtual void SendClientNotReady()
        {
            if (!IsClientConnected)
                return;
            ClientSendRequest(GameReqTypes.ClientNotReady, EmptyMessage.Value);
        }

        public void SendClientPing()
        {
            if (!IsClientConnected)
                return;
            for (int i = 0; i < 10; ++i)
            {
                ClientSendPacket(0, DeliveryMethod.Unreliable, GameMsgTypes.Ping, RttCalculator.GetPingMessage());
            }
        }

        public void SendServerPing()
        {
            if (!IsServer)
                return;
            for (int i = 0; i < 10; ++i)
            {
                ServerSendPacketToAllConnections(0, DeliveryMethod.Unreliable, GameMsgTypes.Ping, RttCalculator.GetPingMessage());
            }
        }

        public bool SendServerSpawnSceneObject(long connectionId, LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return false;
            if (!Players.TryGetValue(connectionId, out LiteNetLibPlayer player) || !player.IsReady)
                return false;
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerSpawnSceneObject, new ServerSpawnSceneObjectMessage()
            {
                objectId = identity.ObjectId,
                sceneObjectId = identity.HashSceneObjectId,
                connectionId = identity.ConnectionId,
                position = identity.transform.position,
                rotation = identity.transform.rotation,
            }, identity.WriteInitSyncFields);
            return true;
        }

        public bool SendServerSpawnObject(long connectionId, LiteNetLibIdentity identity)
        {
            if (!IsServer)
                return false;
            if (!Players.TryGetValue(connectionId, out LiteNetLibPlayer player) || !player.IsReady)
                return false;
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerSpawnObject, new ServerSpawnObjectMessage()
            {
                hashAssetId = identity.HashAssetId,
                objectId = identity.ObjectId,
                connectionId = identity.ConnectionId,
                position = identity.transform.position,
                rotation = identity.transform.rotation,
            }, identity.WriteInitSyncFields);
            return true;
        }

        public void SendServerSpawnObjectWithData(long connectionId, LiteNetLibIdentity identity)
        {
            if (identity == null)
                return;
            bool spawnObjectSent;
            if (Assets.ContainsSceneObject(identity.HashSceneObjectId))
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
            if (!Players.TryGetValue(connectionId, out LiteNetLibPlayer player) || !player.IsReady)
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
            if (!Players.TryGetValue(connectionId, out LiteNetLibPlayer player) || !player.IsReady)
                return;
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerDestroyObject, new ServerErrorMessage()
            {
                shouldDisconnect = shouldDisconnect,
                errorMessage = errorMessage,
            });
        }

        public void SendServerSceneChange(ServerSceneInfo serverSceneInfo)
        {
            if (!IsServer)
                return;
            foreach (long connectionId in Server.ConnectionIds)
            {
                if (IsClientConnected && connectionId == ClientConnectionId)
                    continue;
                SendServerSceneChange(connectionId, serverSceneInfo);
            }
        }

        public void SendServerSceneChange(long connectionId, ServerSceneInfo serverSceneInfo)
        {
            if (!IsServer)
                return;
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerSceneChange, new ServerSceneChangeMessage()
            {
                serverSceneInfo = serverSceneInfo,
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
                if (ServerSceneInfo.HasValue)
                    response.serverSceneInfo = ServerSceneInfo.Value;
            }
            result.Invoke(responseCode, response, serializer => WriteExtraEnterGameResponse(responseCode, request, serializer));
        }

        /// <summary>
        /// Override this to write more data when sending enter game response to client
        /// </summary>
        /// <param name="responseCode"></param>
        /// <param name="request"></param>
        /// <param name="writer"></param>
        protected virtual void WriteExtraEnterGameResponse(AckResponseCode responseCode, EnterGameRequestMessage request, NetDataWriter writer)
        {

        }

        protected virtual void HandleEnterGameResponse(
            ResponseHandlerData responseHandler,
            AckResponseCode responseCode,
            EnterGameResponseMessage response)
        {
            ReadExtraEnterGameResponse(responseCode, response, responseHandler.Reader);
            if (responseCode == AckResponseCode.Success)
            {
                ClientConnectionId = response.connectionId;
                if (IsClientConnected)
                    HandleServerSceneChange(response.serverSceneInfo);
            }
            else
            {
                if (LogError) Logging.LogError(LogTag, "Enter game request was refused by server, disconnecting...");
                OnClientConnectionRefused();
            }
        }

        /// <summary>
        /// Override this to read more data when receiving enter game response from server
        /// </summary>
        /// <param name="responseCode"></param>
        /// <param name="response"></param>
        /// <param name="reader"></param>
        protected virtual void ReadExtraEnterGameResponse(AckResponseCode responseCode, EnterGameResponseMessage response, NetDataReader reader)
        {

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
            result.Invoke(responseCode, EmptyMessage.Value, serializer => WriteExtraClientReadyResponse(responseCode, request, serializer));
        }

        /// <summary>
        /// Override this to write more data when sending client ready response to client
        /// </summary>
        /// <param name="responseCode"></param>
        /// <param name="request"></param>
        /// <param name="writer"></param>
        protected virtual void WriteExtraClientReadyResponse(AckResponseCode responseCode, EmptyMessage request, NetDataWriter writer)
        {

        }

        protected virtual void HandleClientReadyResponse(
            ResponseHandlerData responseHandler,
            AckResponseCode responseCode,
            EmptyMessage response)
        {
            ReadExtraClientReadyResponse(responseCode, response, responseHandler.Reader);
        }

        /// <summary>
        /// Override this to read more data when receiving client ready response from server
        /// </summary>
        /// <param name="responseCode"></param>
        /// <param name="response"></param>
        /// <param name="reader"></param>
        protected virtual void ReadExtraClientReadyResponse(AckResponseCode responseCode, EmptyMessage response, NetDataReader reader)
        {

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
            result.Invoke(responseCode, EmptyMessage.Value, serializer => WriteExtraClientNotReadyResponse(responseCode, request, serializer));
            return default;
        }

        /// <summary>
        /// Override this to write more data when sending client not ready response to client
        /// </summary>
        /// <param name="responseCode"></param>
        /// <param name="request"></param>
        /// <param name="writer"></param>
        protected virtual void WriteExtraClientNotReadyResponse(AckResponseCode responseCode, EmptyMessage request, NetDataWriter writer)
        {

        }

        protected void HandleClientNotReadyResponse(
            ResponseHandlerData responseHandler,
            AckResponseCode responseCode,
            EmptyMessage response)
        {
            ReadExtraClientNotReadyResponse(responseCode, response, responseHandler.Reader);
        }

        /// <summary>
        /// Override this to read more data when receiving client not ready response from server
        /// </summary>
        /// <param name="responseCode"></param>
        /// <param name="response"></param>
        /// <param name="reader"></param>
        protected virtual void ReadExtraClientNotReadyResponse(AckResponseCode responseCode, EmptyMessage response, NetDataReader reader)
        {

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

        protected void HandleClientPing(MessageHandlerData messageHandler)
        {
            PingMessage message = messageHandler.ReadMessage<PingMessage>();
            ServerSendPacket(messageHandler.ConnectionId, 0, DeliveryMethod.Unreliable, GameMsgTypes.Pong, RttCalculator.GetPongMessage(message));
        }

        protected void HandleClientPong(MessageHandlerData messageHandler)
        {
            if (!Players.TryGetValue(messageHandler.ConnectionId, out LiteNetLibPlayer player))
                return;
            player.RttCalculator.OnPong(messageHandler.ReadMessage<PongMessage>());
        }

        protected virtual void HandleServerSpawnSceneObject(MessageHandlerData messageHandler)
        {
            ServerSpawnSceneObjectMessage message = messageHandler.ReadMessage<ServerSpawnSceneObjectMessage>();
            if (!IsServer)
                Assets.NetworkSpawnScene(message.objectId, message.sceneObjectId, message.position, message.rotation, message.connectionId);
            LiteNetLibIdentity identity;
            if (Assets.TryGetSpawnedObject(message.objectId, out identity))
            {
                // If it is not server, read its initial data
                if (!IsServer)
                {
                    identity.ResetSyncData();
                    identity.ReadInitSyncFields(messageHandler.Reader);
                }
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
                    identity.ReadInitSyncFields(messageHandler.Reader);
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

        protected virtual void HandleServerSyncElements(MessageHandlerData messageHandler)
        {
            if (IsServer)
                return;
            NetDataReader reader = messageHandler.Reader;
            int syncElementsCount = reader.GetInt();
            for (int i = 0; i < syncElementsCount; ++i)
            {
                // Get element info
                uint objectId = reader.GetPackedUInt();
                int elementId = reader.GetPackedInt();
                // Get data length, it will being used for bytes skipping if it is unable to proceed data properly
                int dataLength = reader.GetInt();
                int positionBeforeRead = reader.Position;

                bool readFail = false;
                if (Assets.TryGetSpawnedObject(objectId, out LiteNetLibIdentity identity) &&
                    identity.TryGetSyncElement(elementId, out LiteNetLibSyncElement element))
                {
                    // Can read data properly
                    try
                    {
                        element.ReadSyncData(reader);
                    } catch
                    {
                        readFail = true;
                    }
                }
                else
                {
                    readFail = true;
                }

                if (readFail)
                {
                    // Missing data, unable to proceed properly, so skip bytes
                    reader.SetPosition(positionBeforeRead);
                    reader.SkipBytes(dataLength);
                }
            }
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
            HandleServerSceneChange(message.serverSceneInfo);
        }

        protected virtual async void HandleServerSceneChange(ServerSceneInfo serverSceneInfo)
        {
            // Scene loaded at server, if this is host (client and server) then skip it.
            if (IsServer)
                return;

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(serverSceneInfo.sceneName) || activeSceneName.Equals(serverSceneInfo.sceneName))
            {
                await Assets.Initialize();
                Assets.InitPoolingObjects();
                OnClientOnlineSceneLoaded();
                if (!doNotReadyOnSceneLoaded)
                {
                    SendClientReady();
                }
            }
            else
            {
                // If scene is difference, load changing scene
                LoadSceneRoutine(serverSceneInfo, true).Forget();
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
            ClientSendPacket(0, DeliveryMethod.Unreliable, GameMsgTypes.Pong, RttCalculator.GetPongMessage(message));
        }

        protected void HandleServerPong(MessageHandlerData messageHandler)
        {
            RttCalculator.OnPong(messageHandler.ReadMessage<PongMessage>());
        }

        protected void HandleServerDisconnect(MessageHandlerData messageHandler)
        {
            Client.SetDisconnectData(messageHandler.Reader.GetBytesWithLength());
        }
        #endregion

        /// <summary>
        /// Overrride this function to send custom data when send enter game message, enter game message will be sent from client to request to join a game before client's scene loading
        /// </summary>
        /// <param name="writer"></param>
        public virtual void SerializeEnterGameData(NetDataWriter writer)
        {

        }

        /// <summary>
        /// Override this function to read custom data that come with enter game message, enter game message will be sent from client to request to join a game before client's scene loading
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="reader"></param>
        /// <returns>Return `true` if allow player to enter game.</returns>
        public virtual UniTask<bool> DeserializeEnterGameData(long connectionId, NetDataReader reader)
        {
            return new UniTask<bool>(true);
        }

        /// <summary>
        /// Overrride this function to send custom data when send client ready message, client ready message will be sent from client when client's scene loaded
        /// </summary>
        /// <param name="writer"></param>
        public virtual void SerializeClientReadyData(NetDataWriter writer)
        {

        }

        /// <summary>
        /// Override this function to read custom data that come with client ready message, client ready message will be sent from client when client's scene loaded
        /// </summary>
        /// <param name="playerIdentity"></param>
        /// <param name="connectionId"></param>
        /// <param name="reader"></param>
        /// <returns>Return `true` if player is ready to play.</returns>
        public virtual UniTask<bool> DeserializeClientReadyData(LiteNetLibIdentity playerIdentity, long connectionId, NetDataReader reader)
        {
            return new UniTask<bool>(true);
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
        /// Override this function to do anything after refused from server
        /// </summary>
        public virtual void OnClientConnectionRefused()
        {
            StopClient();
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
            if (Assets.AddressablePlayerPrefab.IsDataValid())
                return SpawnPlayer(connectionId, Assets.AddressablePlayerPrefab);
#if !EXCLUDE_PREFAB_REFS
            if (Assets.PlayerPrefab != null)
                return SpawnPlayer(connectionId, Assets.PlayerPrefab);
#endif
            return null;
        }

        protected LiteNetLibIdentity SpawnPlayer(long connectionId, AssetReferenceLiteNetLibIdentity addressablePrefab)
        {
            return SpawnPlayer(connectionId, addressablePrefab.HashAssetId);
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
