using System;
using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LiteNetLibManager.Tests
{
    public class PrefabAssetPreloadProbe : MonoBehaviour, ILiteNetLibPrefabAssetsPreloader
    {
        public int calls;
        public UniTaskCompletionSource<IDisposable> pending = new UniTaskCompletionSource<IDisposable>();
        public UniTask<IDisposable> PreloadPrefabAssets() { ++calls; return pending.Task; }
    }

    public class PrefabAssetLeaseProbe : IDisposable
    {
        public int disposals;
        public void Dispose() { ++disposals; }
    }

    public class PrefabAssetPreloadingTests
    {
        private GameObject root;
        private GameObject prefabRoot;
        private GameManagerHarness manager;
        private LiteNetLibAssets assets;
        private PrefabAssetPreloadProbe preloader;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PrefabAssetPreloadingFixture");
            root.SetActive(false);
            assets = root.AddComponent<LiteNetLibAssets>();
            manager = root.AddComponent<GameManagerHarness>();
            manager.InitializeForTest();
            prefabRoot = new GameObject("PrefabAssetPreloadingFixture_Prefab");
            prefabRoot.SetActive(false);
            var prefab = prefabRoot.AddComponent<LiteNetLibIdentity>();
            preloader = prefabRoot.AddComponent<PrefabAssetPreloadProbe>();
            assets.GuidToPrefabs[123] = prefab;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(prefabRoot);
            Object.DestroyImmediate(root);
        }

        [UnityTest]
        public IEnumerator ServerWaitsForDependenciesAndHoldsLeaseUntilCatalogClear() => UniTask.ToCoroutine(async () =>
        {
            typeof(LiteNetLibManager).GetProperty("IsServer").SetValue(manager, true);
            var pending = assets.Initialize();
            Assert.AreEqual(1, preloader.calls);
            Assert.IsFalse(assets.IsInitialized);
            var lease = new PrefabAssetLeaseProbe();
            preloader.pending.TrySetResult(lease);
            await pending;
            Assert.IsTrue(assets.IsInitialized);
            Assert.AreEqual(0, lease.disposals);
            assets.Clear();
            Assert.AreEqual(0, lease.disposals);
            await UniTask.DelayFrame(2);
            Assert.AreEqual(1, lease.disposals);
        });

        [UnityTest]
        public IEnumerator ClientDoesNotPreloadServerDependencies() => UniTask.ToCoroutine(async () =>
        {
            typeof(LiteNetLibManager).GetProperty("IsClient").SetValue(manager, true);
            await assets.Initialize();
            Assert.IsTrue(assets.IsInitialized);
            Assert.AreEqual(0, preloader.calls);
        });

        [UnityTest]
        public IEnumerator ClearingDuringPreloadDisposesLateResultWithoutMarkingOldCatalogReady() => UniTask.ToCoroutine(async () =>
        {
            typeof(LiteNetLibManager).GetProperty("IsServer").SetValue(manager, true);
            var pending = assets.Initialize();
            assets.Clear();
            var lease = new PrefabAssetLeaseProbe();
            preloader.pending.TrySetResult(lease);
            await pending;
            Assert.IsFalse(assets.IsInitialized);
            Assert.AreEqual(1, lease.disposals);
            await UniTask.DelayFrame(2);
            Assert.AreEqual(1, lease.disposals);
        });
    }
}
