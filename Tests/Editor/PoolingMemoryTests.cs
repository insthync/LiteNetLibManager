using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace LiteNetLibManager.Tests
{
    public class PoolLifecycleProbe : MonoBehaviour
    {
        public int getCount;
        public int returnCount;

        public void RecordGet()
        {
            ++getCount;
        }

        public void RecordReturn()
        {
            ++returnCount;
        }
    }

    public class PoolingMemoryTests
    {
        private const string Prefix = "PoolingMemoryFixture_";
        private readonly List<GameObject> _fixtures = new List<GameObject>();
        private LiteNetLibAssets _assets;

        private Dictionary<int, LiteNetLibIdentity> Prefabs =>
            (Dictionary<int, LiteNetLibIdentity>)typeof(LiteNetLibAssets)
                .GetField("GuidToPrefabs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_assets);

        private Dictionary<int, Queue<LiteNetLibIdentity>> Pools =>
            (Dictionary<int, Queue<LiteNetLibIdentity>>)typeof(LiteNetLibAssets)
                .GetField("PooledObjects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_assets);

        [SetUp]
        public void SetUp()
        {
            var root = new GameObject(Prefix + "Assets");
            root.SetActive(false);
            _fixtures.Add(root);
            _assets = root.AddComponent<LiteNetLibAssets>();
        }

        [TearDown]
        public void TearDown()
        {
            Pools.Clear();
            foreach (GameObject fixture in _fixtures)
            {
                if (fixture != null)
                    Object.DestroyImmediate(fixture);
            }
            _fixtures.Clear();
        }

        private LiteNetLibIdentity CreatePrefab(int index, int size)
        {
            var root = new GameObject(Prefix + index);
            root.SetActive(false);
            _fixtures.Add(root);
            var prefab = root.AddComponent<LiteNetLibIdentity>();
            typeof(LiteNetLibIdentity).GetField("assetId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(prefab, Prefix + index);
            prefab.PoolingSize = size;
            var probe = root.AddComponent<PoolLifecycleProbe>();
            UnityEventTools.AddPersistentListener(prefab.onGetInstance, probe.RecordGet);
            prefab.onGetInstance.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            UnityEventTools.AddPersistentListener(prefab.onPushBack, probe.RecordReturn);
            prefab.onPushBack.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            Prefabs.Add(prefab.HashAssetId, prefab);
            return prefab;
        }

        private LiteNetLibIdentity Get(LiteNetLibIdentity prefab, Vector3 position)
        {
            var instance = _assets.GetObjectInstance(prefab.HashAssetId, position, Quaternion.identity);
            _fixtures.Add(instance.gameObject);
            return instance;
        }

        private void TrackPrewarmedObjects()
        {
            foreach (var queue in Pools.Values)
            {
                foreach (var instance in queue)
                    _fixtures.Add(instance.gameObject);
            }
        }

        [Test]
        public void SceneInitialization_DoesNotInstantiateUnusedPrefabs()
        {
            for (int i = 0; i < 50; ++i)
                CreatePrefab(i, 8);

            _assets.InitPoolingQueues();
            TrackPrewarmedObjects();

            Assert.IsFalse(_assets.prewarmPooling);
            Assert.IsEmpty(Pools);
            Assert.AreEqual(50, Resources.FindObjectsOfTypeAll<LiteNetLibIdentity>()
                .Count(identity => identity.name.StartsWith(Prefix)));
        }

        [Test]
        public void FirstSpawnAndReturn_CreateOnlyOneInstanceAndReuseIt()
        {
            var prefab = CreatePrefab(0, 30);
            var first = Get(prefab, new Vector3(1, 2, 3));
            var probe = first.GetComponent<PoolLifecycleProbe>();

            Assert.IsTrue(first.IsPooledInstance);
            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.AreEqual(new Vector3(1, 2, 3), first.transform.position);
            Assert.AreEqual(1, probe.getCount, "Cold spawns must run the same get callback as prewarmed spawns.");

            for (int i = 0; i < 100; ++i)
            {
                _assets.PushInstanceBack(first);
                TrackPrewarmedObjects();
                Assert.AreEqual(1, Pools[prefab.HashAssetId].Count,
                    "Returning the first instance must not create the other configured pool entries.");
                var reused = _assets.GetObjectInstance(prefab.HashAssetId, new Vector3(i, 0, 0), Quaternion.identity);
                Assert.AreSame(first, reused);
                Assert.AreEqual(new Vector3(i, 0, 0), reused.transform.position);
            }

            Assert.AreEqual(101, probe.getCount);
            Assert.AreEqual(100, probe.returnCount);
            Assert.IsEmpty(Pools[prefab.HashAssetId]);
        }

        [Test]
        public void ConcurrentSpawns_OnlyRetainInstancesThatWereUsed()
        {
            var prefab = CreatePrefab(0, 30);
            CreatePrefab(1, 30);
            var instances = Enumerable.Range(0, 3).Select(i => Get(prefab, Vector3.zero)).ToArray();

            foreach (var instance in instances)
                _assets.PushInstanceBack(instance);

            TrackPrewarmedObjects();
            Assert.AreEqual(1, Pools.Count);
            Assert.AreEqual(3, Pools[prefab.HashAssetId].Count);
        }

        [Test]
        public void ExplicitPrewarming_PreservesConfiguredInitialPool()
        {
            var prefab = CreatePrefab(0, 4);
            _assets.prewarmPooling = true;
            _assets.InitPoolingQueues();
            TrackPrewarmedObjects();
            _assets.InitPoolingQueues();
            TrackPrewarmedObjects();

            Assert.AreEqual(4, Pools[prefab.HashAssetId].Count);
            var first = Get(prefab, Vector3.zero);
            Assert.AreEqual(1, first.GetComponent<PoolLifecycleProbe>().getCount);
            _assets.PushInstanceBack(first);
            Assert.AreEqual(4, Pools[prefab.HashAssetId].Count);
        }

        [Test]
        public void DisabledPooling_DoesNotInitializeQueues()
        {
            var prefab = CreatePrefab(0, 4);
            _assets.disablePooling = true;
            _assets.prewarmPooling = true;
            _assets.InitPoolingQueues();
            Assert.IsNull(_assets.GetOrInitPoolingQueue(prefab.HashAssetId));
            Assert.IsEmpty(Pools);
        }

        [TestCase(0)]
        [TestCase(2)]
        public void ReturnLimit_DoesNotRetainExcessInstances(int size)
        {
            var prefab = CreatePrefab(0, size);
            var instances = Enumerable.Range(0, size + 1).Select(i => Get(prefab, Vector3.zero)).ToArray();
            for (int i = 0; i < size; ++i)
                _assets.PushInstanceBack(instances[i]);

            // Destroy is deferred in gameplay. EditMode rejects it; expect that error here,
            // verify the excess object was not retained, then remove the fixture immediately.
            LogAssert.Expect(LogType.Error, new Regex("Destroy.*edit mode", RegexOptions.IgnoreCase));
            _assets.PushInstanceBack(instances[size]);
            Assert.AreEqual(size, Pools[prefab.HashAssetId].Count);
        }

        [Test]
        public void UnlimitedRetention_PreservesExistingOptOut()
        {
            var prefab = CreatePrefab(0, 1);
            _assets.limitByPoolingSize = false;
            for (int i = 0; i < 3; ++i)
            {
                var instance = Get(prefab, Vector3.zero);
                // Keep each instance checked out until all three have actually been used.
                if (i == 2)
                {
                    foreach (var used in _fixtures.Select(o => o.GetComponent<LiteNetLibIdentity>())
                        .Where(identity => identity != null && identity != prefab).ToArray())
                        _assets.PushInstanceBack(used);
                }
            }
            Assert.AreEqual(3, Pools[prefab.HashAssetId].Count);
        }
    }
}
