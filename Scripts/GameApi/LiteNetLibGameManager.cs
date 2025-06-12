using Cysharp.Threading.Tasks;
using Insthync.AddressableAssetTools;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

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
        public bool safeGameStatePacket = false;

        protected readonly Dictionary<long, LiteNetLibPlayer> Players = new Dictionary<long, LiteNetLibPlayer>();

        private double _clientSendPingCountDown;
        private double _serverSendPingCountDown;
        public static List<ServerSceneLoadingInfo> LoadingServerScenes { get; private set; } = new List<ServerSceneLoadingInfo>();
        public int LoadedAdditiveScenesCount { get; internal set; } = 0;
        public int TotalAdditiveScensCount { get; protected set; } = 0;

        public long ClientConnectionId { get; protected set; }
        protected readonly LiteNetLibSyncingStates ClientSyncingStates = new LiteNetLibSyncingStates();

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
        public uint Tick
        {
            get
            {
                if (IsServer)
                    return _serverUpdater.Tick;
                return _clientUpdater.Tick;
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
        protected readonly List<LiteNetLibSyncElement> _updatingClientSyncElements = new List<LiteNetLibSyncElement>();
        protected readonly List<LiteNetLibSyncElement> _updatingServerSyncElements = new List<LiteNetLibSyncElement>();
        protected NetDataWriter _gameStatesWriter = new NetDataWriter(true, 1024);
        protected NetDataWriter _syncElementWriter = new NetDataWriter(true, 1024);

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
            ProceedServerGameStateSync();
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
                ProceedClientGameStateSync();
            // Send ping from client
            _clientSendPingCountDown -= updater.DeltaTime;
            if (_clientSendPingCountDown <= 0f)
            {
                SendClientPing();
                _clientSendPingCountDown = pingDuration;
            }
        }

        private void ProceedServerGameStateSync()
        {
            // Filter which elements can be synced
            LiteNetLibPlayer tempPlayer;
            foreach (long connectionId in Server.ConnectionIds)
            {
                if (!Players.TryGetValue(connectionId, out tempPlayer) || !tempPlayer.IsReady)
                    continue;
                if (_updatingServerSyncElements.Count == 0)
                    continue;
                foreach (LiteNetLibSyncElement syncElement in _updatingServerSyncElements)
                {
                    if (!syncElement.CanSyncFromServer(tempPlayer))
                        continue;
                    if (syncElement.WillSyncFromServerUnreliably(tempPlayer))
                    {
                        _syncElementWriter.Reset();
                        _syncElementWriter.PutPackedUShort(GameMsgTypes.SyncElement);
                        WriteSyncElement(_syncElementWriter, syncElement);
                        ServerSendMessage(tempPlayer.ConnectionId, 0, DeliveryMethod.Unreliable, _syncElementWriter);
                    }
                    if (syncElement.WillSyncFromServerReliably(tempPlayer))
                        tempPlayer.SyncingStates.AppendDataSyncState(syncElement);
                }
            }

            foreach (long connectionId in Server.ConnectionIds)
            {
                if (!Players.TryGetValue(connectionId, out tempPlayer) || !tempPlayer.IsReady)
                    continue;
                SyncGameStateToClient(tempPlayer);
            }

            if (_updatingServerSyncElements.Count > 0)
            {
                for (int i = _updatingServerSyncElements.Count - 1; i >= 0; --i)
                {
                    _updatingServerSyncElements[i].Synced();
                }
            }
        }

        private void ProceedClientGameStateSync()
        {
            if (_updatingClientSyncElements.Count == 0)
                return;
            foreach (LiteNetLibSyncElement syncElement in _updatingClientSyncElements)
            {
                if (!syncElement.CanSyncFromOwnerClient())
                    continue;
                if (syncElement.WillSyncFromOwnerClientUnreliably())
                {
                    _syncElementWriter.Reset();
                    _syncElementWriter.PutPackedUShort(GameMsgTypes.SyncElement);
                    WriteSyncElement(_syncElementWriter, syncElement);
                    ClientSendMessage(0, DeliveryMethod.Unreliable, _syncElementWriter);
                }
                if (syncElement.WillSyncFromOwnerClientReliably())
                    ClientSyncingStates.AppendDataSyncState(syncElement);
            }
            SyncGameStateToServer();
            for (int i = _updatingClientSyncElements.Count - 1; i >= 0; --i)
            {
                _updatingClientSyncElements[i].Synced();
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
                _gameStatesWriter.PutPackedUShort(GameMsgTypes.SyncStates);
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
                _gameStatesWriter.PutPackedUShort(GameMsgTypes.SyncStates);
                byte syncChannelId = syncingStatesByChannelId.Key;
                int stateCount = WriteGameStateFromClient(_gameStatesWriter, syncChannelId, syncingStatesByChannelId.Value);
                if (stateCount > 0)
                {
                    // Send data to client
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
            RegisterServerMessage(GameMsgTypes.SyncStates, HandleClientSyncStates);
            RegisterServerMessage(GameMsgTypes.SyncElement, HandleClientSyncElement);
            RegisterServerMessage(GameMsgTypes.Ping, HandleClientPing);
            RegisterServerMessage(GameMsgTypes.Pong, HandleClientPong);
            // Client messages
            RegisterClientMessage(GameMsgTypes.CallFunction, HandleServerCallFunction);
            RegisterClientMessage(GameMsgTypes.SyncStates, HandleServerSyncStates);
            RegisterClientMessage(GameMsgTypes.SyncElement, HandleServerSyncElement);
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
            ClientSyncingStates.Clear();
            RttCalculator.Reset();
            _updatingClientSyncElements.Clear();
            _updatingServerSyncElements.Clear();

            if (!doNotEnterGameOnConnect)
                SendClientEnterGame();
            SendClientPing();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Reset client connection id, will be received from server later
            ClientConnectionId = -1;
            ClientSyncingStates.Clear();
            RttCalculator.Reset();
            _updatingClientSyncElements.Clear();
            _updatingServerSyncElements.Clear();

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
            ServerSendPacket(connectionId, 0, DeliveryMethod.ReliableOrdered, GameMsgTypes.ServerError, new ServerErrorMessage()
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

        protected virtual void HandleClientSyncStates(MessageHandlerData messageHandler)
        {
            ReadGameStateFromClient(messageHandler.Reader);
        }

        protected virtual void HandleClientSyncElement(MessageHandlerData messageHandler)
        {
            ReadSymcElement(messageHandler.Reader);
        }

        protected void HandleClientPing(MessageHandlerData messageHandler)
        {
            PingMessage pingMessage = messageHandler.ReadMessage<PingMessage>();
            PongMessage pongMessage = RttCalculator.GetPongMessage(pingMessage);
            pongMessage.tick = _serverUpdater.Tick;
            ServerSendPacket(messageHandler.ConnectionId, 0, DeliveryMethod.Unreliable, GameMsgTypes.Pong, pongMessage);
        }

        protected void HandleClientPong(MessageHandlerData messageHandler)
        {
            if (!Players.TryGetValue(messageHandler.ConnectionId, out LiteNetLibPlayer player))
                return;
            player.RttCalculator.OnPong(messageHandler.ReadMessage<PongMessage>());
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
            ReadSymcElement(messageHandler.Reader);
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
            PongMessage message = messageHandler.ReadMessage<PongMessage>();
            RttCalculator.OnPong(message);
            _clientUpdater.OnSyncTick(message.tick, RttCalculator.Rtt);
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

        #region Game State Syncing
        private void WriteSyncElement(NetDataWriter writer, LiteNetLibSyncElement syncElement)
        {
            writer.PutPackedUInt(Tick);
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
                syncElement.WriteSyncData(false, writer);
                dataLength = writer.Length - posAfterWriteDataLen;
                // Put data length
                int posAfterWriteData = writer.Length;
                writer.SetPosition(posBeforeWriteDataLen);
                writer.Put(dataLength);
                writer.SetPosition(posAfterWriteData);
            }
            else
            {
                syncElement.WriteSyncData(false, writer);
            }
        }

        private bool ReadSymcElement(NetDataReader reader)
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
                        element.ReadSyncData(false, reader);
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
                        element.ReadSyncData(false, reader);
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
            writer.PutPackedUInt(Tick);
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
                if (Assets.TryGetSpawnedObject(objectId, out LiteNetLibIdentity identity))
                {
                    switch (syncingStatesByObjectId.Value.StateType)
                    {
                        case GameStateSyncData.STATE_TYPE_SPAWN:
                            writer.Put(GameStateSyncData.STATE_TYPE_SPAWN);
                            WriteSpawnGameState(writer, player, identity, syncingStatesByObjectId.Value);
                            // TODO: Move this to somewhere else
                            if (player.ConnectionId == ClientConnectionId)
                            {
                                // Simulate object spawning if it is a host
                                identity.OnServerSubscribingAdded();
                            }
                            ++stateCount;
                            break;
                        case GameStateSyncData.STATE_TYPE_SYNC:
                            writer.Put(GameStateSyncData.STATE_TYPE_SYNC);
                            WriteSyncGameState(writer, objectId, syncingStatesByObjectId.Value);
                            ++stateCount;
                            break;
                        case GameStateSyncData.STATE_TYPE_DESTROY:
                            writer.Put(GameStateSyncData.STATE_TYPE_DESTROY);
                            WriteDestroyGameState(writer, objectId, syncingStatesByObjectId.Value);
                            // TODO: Move this to somewhere else
                            if (player.ConnectionId == ClientConnectionId)
                            {
                                // Simulate object destroying if it is a host
                                identity.OnServerSubscribingRemoved();
                            }
                            ++stateCount;
                            break;
                    }
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
            writer.PutPackedUInt(Tick);
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
                        WriteSyncGameState(writer, objectId, syncingStatesByObjectId.Value);
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
                        if (!ReadSpawnGameState(reader))
                            return;
                        break;
                    case GameStateSyncData.STATE_TYPE_SYNC:
                        if (!ReadSyncGameState(reader))
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
                if (!ReadSyncGameState(reader))
                    return;
            }
        }

        private void WriteSpawnGameState(NetDataWriter writer, LiteNetLibPlayer player, LiteNetLibIdentity identity, GameStateSyncData syncData)
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
            WriteSyncElements(writer, syncData.SyncElements, true);
            syncData.SyncElements.Clear();
        }

        private bool ReadSpawnGameState(NetDataReader reader)
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
            return ReadSyncElements(reader, identity, true);
        }

        private void WriteSyncGameState(NetDataWriter writer, uint objectId, GameStateSyncData syncData)
        {
            writer.PutPackedUInt(objectId);
            WriteSyncElements(writer, syncData.SyncElements, false);
            syncData.SyncElements.Clear();
        }

        private bool ReadSyncGameState(NetDataReader reader)
        {
            uint objectId = reader.GetPackedUInt();
            if (!Assets.TryGetSpawnedObject(objectId, out LiteNetLibIdentity identity))
                return false;
            return ReadSyncElements(reader, identity, false);
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

        private void WriteSyncElements(NetDataWriter writer, ICollection<LiteNetLibSyncElement> elements, bool initial)
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
                    syncElement.WriteSyncData(initial, writer);
                    dataLength = writer.Length - posAfterWriteDataLen;
                    // Put data length
                    int posAfterWriteData = writer.Length;
                    writer.SetPosition(posBeforeWriteDataLen);
                    writer.Put(dataLength);
                    writer.SetPosition(posAfterWriteData);
                }
                else
                {
                    syncElement.WriteSyncData(initial, writer);
                }
            }
        }

        private bool ReadSyncElements(NetDataReader reader, LiteNetLibIdentity identity, bool initial)
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
                            element.ReadSyncData(initial, reader);
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
                            element.ReadSyncData(initial, reader);
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
        #endregion
    }
}
