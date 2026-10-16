#if !DISABLE_ADDRESSABLES
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using LiteNetLib.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LiteNetLibManager.Tests
{
    public class ManualClientPrefabOperation : AsyncOperationBase<GameObject>
    {
        protected override void Execute() { }
        public void Finish(GameObject prefab) { Complete(prefab, true, (string)null); }
    }

    public class ClientPrefabTestAssets : LiteNetLibAssets
    {
        public int loads;
        public ManualClientPrefabOperation operation;
        public AsyncOperationHandle<GameObject> requestedHandle;
        protected override AsyncOperationHandle<GameObject> LoadClientPrefabAsset(object key)
        {
            ++loads;
            operation = new ManualClientPrefabOperation();
            requestedHandle = Addressables.ResourceManager.StartOperation(operation, default);
            return requestedHandle;
        }
    }

    public class ClientPrefabLoadingTests
    {
        private GameObject root;
        private GameObject prefabRoot;
        private GameManagerHarness manager;
        private ClientPrefabTestAssets assets;
        private LiteNetLibIdentity prefab;
        private AssetReferenceLiteNetLibIdentity reference;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ClientPrefabLoadingTest_Manager");
            root.SetActive(false);
            assets = root.AddComponent<ClientPrefabTestAssets>();
            manager = root.AddComponent<GameManagerHarness>();
            manager.InitializeForTest();
            manager.UseServerTransportForTest(new RecordingGameTransport());
            var client = new LiteNetLibClient(manager);
            typeof(LiteNetLibClient).GetField("_isNetworkActive", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(client, true);
            typeof(LiteNetLibManager).GetProperty("Client").SetValue(manager, client);
            typeof(LiteNetLibManager).GetProperty("IsClient").SetValue(manager, true);
            assets.loadClientPrefabsOnDemand = true;
            assets.limitByPoolingSize = false;
            prefabRoot = new GameObject("ClientPrefabLoadingTest_Prefab");
            prefabRoot.SetActive(false);
            prefab = prefabRoot.AddComponent<LiteNetLibIdentity>();
            typeof(LiteNetLibIdentity).GetField("assetId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(prefab, "ClientPrefabLoadingTest_Identity");
            reference = new AssetReferenceLiteNetLibIdentity("0123456789abcdef0123456789abcdef") { HashAssetId = prefab.HashAssetId };
            assets.addressableSpawnablePrefabs = new[] { reference };
            assets.RegisterPrefabs().GetAwaiter().GetResult();
        }

        [TearDown]
        public void TearDown()
        {
            manager.ClearClientPrefabMessages();
            foreach (var identity in new List<LiteNetLibIdentity>(assets.SpawnedObjects.Values))
                if (identity != null) Object.DestroyImmediate(identity.gameObject);
            assets.SpawnedObjects.Clear();
            foreach (var pool in assets.PooledObjects.Values)
                foreach (var identity in pool)
                    if (identity != null) Object.DestroyImmediate(identity.gameObject);
            assets.PooledObjects.Clear();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(prefabRoot);
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 5;
            while (!condition() && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), "Asynchronous prefab work did not finish.");
        }

        private void Receive(string handler, ushort type, byte[] bytes)
        {
            typeof(LiteNetLibGameManager).GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, new object[] { new MessageHandlerData(type, null, 1, new NetDataReader(bytes)) });
        }

        private byte[] SpawnPacket()
        {
            var writer = new NetDataWriter();
            writer.PutPackedUInt(10);
            writer.Put((ushort)1);
            writer.Put((byte)GameStateSyncType.Spawn);
            writer.Put(false);
            writer.PutPackedInt(prefab.HashAssetId);
            for (int i = 0; i < 6; ++i) writer.Put(0f);
            writer.PutPackedUInt(42);
            writer.PutPackedLong(-1);
            writer.PutPackedInt(0);
            return writer.CopyData();
        }

        [Test]
        public void RegisteringClientCatalogDoesNotLoadPrefabAssets()
        {
            Assert.IsTrue(assets.UsesClientPrefabLoading);
            Assert.Zero(assets.loads);
            Assert.IsEmpty(assets.GuidToPrefabs);
        }

        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void ServersHostsAndInactiveManagersKeepThePreloadPath(bool client, bool server)
        {
            typeof(LiteNetLibManager).GetProperty("IsClient").SetValue(manager, client);
            typeof(LiteNetLibManager).GetProperty("IsServer").SetValue(manager, server);
            Assert.IsFalse(assets.UsesClientPrefabLoading);
        }

        [Test]
        public void LegacyOptOutKeepsThePreloadPath()
        {
            assets.loadClientPrefabsOnDemand = false;
            Assert.IsFalse(assets.UsesClientPrefabLoading);
        }

        [UnityTest]
        public IEnumerator ConcurrentRequestsShareOneAssetLoad()
        {
            var first = assets.EnsureClientPrefabAsync(prefab.HashAssetId, CancellationToken.None).AsTask();
            var second = assets.EnsureClientPrefabAsync(prefab.HashAssetId, CancellationToken.None).AsTask();
            Assert.AreEqual(1, assets.loads);
            assets.operation.Finish(prefabRoot);
            yield return Until(() => first.IsCompleted && second.IsCompleted);
            Assert.AreSame(prefab, first.Result);
            Assert.AreSame(prefab, second.Result);
        }

        [UnityTest]
        public IEnumerator DelayedSpawnOwnsPacketBytesAndReplaysOwnerRpcAndDespawnInOrder()
        {
            byte[] spawn = SpawnPacket();
            Receive("HandleServerSyncBaseLine", GameMsgTypes.SyncBaseLine, spawn);
            Assert.IsEmpty(assets.SpawnedObjects);
            Array.Clear(spawn, 0, spawn.Length); // Simulate transport recycling the original reader.

            assets.manuallyApplyOwnerChanges = true;
            var writer = new NetDataWriter();
            new ServerSetObjectOwner { objectId = 42, connectionId = 123 }.Serialize(writer);
            Receive("HandleServerSetObjectOwner", GameMsgTypes.ServerSetObjectOwner, writer.CopyData());
            Assert.IsEmpty(assets.ChangingOwnerObjects);

            writer.Reset();
            LiteNetLibElementInfo.SerializeInfo(new LiteNetLibElementInfo { objectId = 999, elementId = 1 }, writer);
            writer.Put((byte)7);
            byte[] rpc = writer.CopyData();
            Receive("HandleServerCallFunction", GameMsgTypes.RPC, rpc);
            Array.Clear(rpc, 0, rpc.Length);

            writer.Reset();
            writer.PutPackedUInt(11);
            writer.Put((ushort)1);
            writer.Put((byte)GameStateSyncType.Destroy);
            writer.PutPackedUInt(42);
            writer.Put(DestroyObjectReasons.RequestedToDestroy);
            Receive("HandleServerSyncBaseLine", GameMsgTypes.SyncBaseLine, writer.CopyData());

            assets.operation.Finish(prefabRoot);
            yield return Until(() => assets.PooledObjects.Count > 0);
            Assert.IsEmpty(assets.SpawnedObjects, "The queued despawn must remove the delayed spawn.");
            Assert.AreEqual(123, assets.ChangingOwnerObjects[42]);
            Assert.AreEqual(1, manager.PendingRpcCount);
            Assert.AreEqual(7, manager.OldestPendingRpcFirstByte);
        }

        [UnityTest]
        public IEnumerator ClearingTheSessionCancelsPendingLoadsAndPreventsLateRegistration()
        {
            var pending = assets.EnsureClientPrefabAsync(prefab.HashAssetId, CancellationToken.None).AsTask();
            assets.Clear();
            assets.operation.Finish(prefabRoot);
            yield return Until(() => pending.IsCompleted);
            // This UniTask version converts cancellation to a faulted Task containing the cancellation exception.
            Assert.Throws<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            Assert.IsEmpty(assets.GuidToPrefabs);
            Assert.IsFalse(assets.requestedHandle.IsValid());
        }

        [UnityTest]
        public IEnumerator IdlePrefabIsReleasedAndCanBeLoadedAgain()
        {
            var pending = assets.EnsureClientPrefabAsync(prefab.HashAssetId, CancellationToken.None).AsTask();
            assets.operation.Finish(prefabRoot);
            yield return Until(() => pending.IsCompleted);
            assets.ReleaseIdleClientPrefabs(Time.unscaledTime + 61);
            Assert.IsEmpty(assets.GuidToPrefabs);
            yield return Until(() => !assets.requestedHandle.IsValid());
            var reloaded = assets.EnsureClientPrefabAsync(prefab.HashAssetId, CancellationToken.None).AsTask();
            Assert.AreEqual(2, assets.loads);
            assets.operation.Finish(prefabRoot);
            yield return Until(() => reloaded.IsCompleted);
            Assert.AreSame(prefab, reloaded.Result);
        }

        [UnityTest]
        public IEnumerator ActiveSpawnKeepsItsPrefabAlivePastTheIdleLimit()
        {
            var pending = assets.EnsureClientPrefabAsync(prefab.HashAssetId, CancellationToken.None).AsTask();
            assets.operation.Finish(prefabRoot);
            yield return Until(() => pending.IsCompleted);
            var active = assets.NetworkSpawn(prefab.HashAssetId, Vector3.zero, Quaternion.identity, 42);
            Assert.IsNotNull(active);
            assets.ReleaseIdleClientPrefabs(Time.unscaledTime + 120);
            Assert.IsTrue(assets.GuidToPrefabs.ContainsKey(prefab.HashAssetId));
            Assert.IsTrue(assets.requestedHandle.IsValid());
        }

        [UnityTest]
        public IEnumerator CustomEntityMessagesWaitButConnectionControlMessagesDoNot()
        {
            typeof(LiteNetLibManager).GetProperty("Client").SetValue(manager, new LiteNetLibClient(manager));
            bool customApplied = false;
            bool pingApplied = false;
            manager.RegisterClientMessage(99, message =>
            {
                Assert.IsTrue(assets.SpawnedObjects.ContainsKey(42));
                Assert.AreEqual(7, message.Reader.GetByte());
                customApplied = true;
            });
            manager.RegisterClientMessage(GameMsgTypes.Ping, message => pingApplied = true);
            Receive("HandleServerSyncBaseLine", GameMsgTypes.SyncBaseLine, SpawnPacket());
            var writer = new NetDataWriter();
            writer.PutPackedUShort(99);
            writer.Put((byte)7);
            byte[] payload = writer.CopyData();
            var readPacket = typeof(TransportHandler).GetMethod("ReadPacket", BindingFlags.Instance | BindingFlags.NonPublic);
            readPacket.Invoke(manager.Client, new object[] { 1L, new NetDataReader(payload) });
            Array.Clear(payload, 0, payload.Length);
            Assert.IsFalse(customApplied);
            writer.Reset();
            writer.PutPackedUShort(GameMsgTypes.Ping);
            readPacket.Invoke(manager.Client, new object[] { 1L, new NetDataReader(writer.CopyData()) });
            Assert.IsTrue(pingApplied);
            assets.operation.Finish(prefabRoot);
            yield return Until(() => customApplied);
        }

        [UnityTest]
        public IEnumerator DisconnectDropsAQueuedSpawnBeforeItsAssetCompletes()
        {
            Receive("HandleServerSyncBaseLine", GameMsgTypes.SyncBaseLine, SpawnPacket());
            assets.Clear();
            assets.operation.Finish(prefabRoot);
            yield return Until(() => !assets.requestedHandle.IsValid());
            Assert.IsEmpty(assets.SpawnedObjects);
            Assert.IsEmpty(assets.GuidToPrefabs);
        }
    }

    public class ClientPrefabMessageQueueTests
    {
        [Test]
        public void MessagesWaitForTheFirstLoadAndKeepArrivalOrder()
        {
            Exception failure = null;
            var queue = new ClientPrefabMessageQueue(ex => failure = ex);
            var loaded = new UniTaskCompletionSource();
            var order = new List<int>();
            queue.Enqueue(10, async token => { await loaded.Task.AttachExternalCancellation(token); order.Add(1); });
            queue.Enqueue(5, token => { order.Add(2); return UniTask.CompletedTask; });
            Assert.IsEmpty(order);
            Assert.AreEqual(15, queue.PendingBytes);
            loaded.TrySetResult();
            CollectionAssert.AreEqual(new[] { 1, 2 }, order);
            Assert.Zero(queue.PendingBytes);
            Assert.IsFalse(queue.IsProcessing);
            Assert.IsNull(failure);
        }

        [Test]
        public void ClearingDropsObsoleteMessagesWithoutBlockingTheNextSession()
        {
            var queue = new ClientPrefabMessageQueue(ex => Assert.Fail(ex.ToString()));
            var obsolete = new UniTaskCompletionSource();
            int read = 0;
            queue.Enqueue(10, async token => { await obsolete.Task.AttachExternalCancellation(token); token.ThrowIfCancellationRequested(); ++read; });
            queue.Enqueue(5, token => { read += 100; return UniTask.CompletedTask; });
            queue.Clear();
            queue.Enqueue(2, token => { read += 10; return UniTask.CompletedTask; });
            obsolete.TrySetResult();
            Assert.AreEqual(10, read);
            Assert.Zero(queue.PendingBytes);
            Assert.IsFalse(queue.IsProcessing);
        }

        [Test]
        public void BufferOverflowFailsAndReleasesQueuedPayloads()
        {
            Exception failure = null;
            var queue = new ClientPrefabMessageQueue(ex => failure = ex);
            var pending = new UniTaskCompletionSource();
            queue.Enqueue(ClientPrefabMessageQueue.MaxBytes, token => pending.Task.AttachExternalCancellation(token));
            queue.Enqueue(1, token => UniTask.CompletedTask);
            Assert.IsInstanceOf<InvalidOperationException>(failure);
            Assert.Zero(queue.PendingBytes);
            Assert.IsFalse(queue.IsProcessing);
        }

        [Test]
        public void FailedLoadDoesNotApplyLaterStateMessages()
        {
            Exception failure = null;
            var queue = new ClientPrefabMessageQueue(ex => failure = ex);
            var loaded = new UniTaskCompletionSource();
            bool laterApplied = false;
            queue.Enqueue(10, token => loaded.Task);
            queue.Enqueue(5, token => { laterApplied = true; return UniTask.CompletedTask; });
            loaded.TrySetException(new InvalidOperationException("Missing prefab"));
            Assert.IsNotNull(failure);
            Assert.IsFalse(laterApplied);
            Assert.Zero(queue.PendingBytes);
        }
    }
}
#endif
