using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Insthync.AddressableAssetTools;
using UnityEngine;
#if !DISABLE_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace LiteNetLibManager
{
    public partial class LiteNetLibAssets
    {
        public bool UsesClientPrefabLoading => loadClientPrefabsOnDemand && Manager != null && Manager.IsClient && !Manager.IsServer
#if DISABLE_ADDRESSABLES
            && false
#endif
            ;

#if !DISABLE_ADDRESSABLES
        [Tooltip("Release idle client prefab assets and their unused pooled instances after this many seconds. Active instances are preserved.")]
        [Min(1)] public float clientPrefabIdleSeconds = 60f;
        private readonly Dictionary<int, AssetReferenceLiteNetLibIdentity> clientPrefabReferences = new Dictionary<int, AssetReferenceLiteNetLibIdentity>();
        private readonly Dictionary<int, ClientPrefabLoad> clientPrefabLoads = new Dictionary<int, ClientPrefabLoad>();
        private readonly List<int> idleClientPrefabs = new List<int>();
        private CancellationTokenSource clientPrefabCancellation;
        private float nextClientPrefabMaintenance;

        private sealed class ClientPrefabLoad
        {
            internal readonly UniTaskCompletionSource<LiteNetLibIdentity> completion = new UniTaskCompletionSource<LiteNetLibIdentity>();
            internal AsyncOperationHandle<GameObject> handle;
            internal bool loaded;
            internal float idleSince;
        }

        private void RegisterClientPrefabReferences()
        {
            foreach (var reference in addressableSpawnablePrefabs)
                RegisterClientPrefabReference(reference);
            RegisterClientPrefabReference(addressablePlayerPrefab);
            AddressablePlayerPrefab = addressablePlayerPrefab;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Client Prefabs] Registered {clientPrefabReferences.Count} prefab references without preloading assets.");
#endif
        }

        internal void RegisterClientPrefabReference(AssetReferenceLiteNetLibIdentity reference)
        {
            if (reference == null || !reference.IsDataValid()) return;
            if (clientPrefabReferences.TryGetValue(reference.HashAssetId, out var previous) &&
                !Equals(previous.RuntimeKey, reference.RuntimeKey))
                throw new InvalidOperationException($"Conflicting client prefab ID: {reference.HashAssetId}.");
            clientPrefabReferences[reference.HashAssetId] = reference;
        }

        internal async UniTask<LiteNetLibIdentity> EnsureClientPrefabAsync(int hashAssetId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (GuidToPrefabs.TryGetValue(hashAssetId, out var existing) && existing != null)
                return existing;
            if (!clientPrefabReferences.TryGetValue(hashAssetId, out var reference))
                throw new InvalidOperationException($"Client prefab ID {hashAssetId} is not registered.");
            if (!clientPrefabLoads.TryGetValue(hashAssetId, out var load))
            {
                if (clientPrefabCancellation == null) clientPrefabCancellation = new CancellationTokenSource();
                load = new ClientPrefabLoad();
                clientPrefabLoads.Add(hashAssetId, load);
                LoadClientPrefab(hashAssetId, reference, load, clientPrefabCancellation.Token).Forget();
            }
            return await load.completion.Task.AttachExternalCancellation(token);
        }

        private async UniTask LoadClientPrefab(int hashAssetId, AssetReferenceLiteNetLibIdentity reference,
            ClientPrefabLoad load, CancellationToken token)
        {
            try
            {
                load.handle = LoadClientPrefabAsset(reference.RuntimeKey);
                var prefab = await load.handle.ToUniTask(cancellationToken: token, cancelImmediately: true);
                token.ThrowIfCancellationRequested();
                if (!clientPrefabReferences.TryGetValue(hashAssetId, out var registered) || !Equals(registered.RuntimeKey, reference.RuntimeKey))
                    throw new InvalidOperationException($"Client prefab ID {hashAssetId} was unregistered while loading.");
                if (prefab == null || !prefab.TryGetComponent(out LiteNetLibIdentity identity) || identity.HashAssetId != hashAssetId)
                    throw new InvalidOperationException($"Invalid client prefab or hash ID for {reference.RuntimeKey}: expected {hashAssetId}.");
                GuidToPrefabs[hashAssetId] = identity;
                load.loaded = true;
                load.idleSince = Time.unscaledTime;
                load.completion.TrySetResult(identity);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Client Prefabs] Loaded {identity.name} ({hashAssetId}) on demand; {clientPrefabLoads.Count} requested prefabs.");
#endif
            }
            catch (Exception ex)
            {
                if (load.handle.IsValid()) Addressables.Release(load.handle);
                load.handle = default;
                if (clientPrefabLoads.TryGetValue(hashAssetId, out var current) && ReferenceEquals(current, load))
                    clientPrefabLoads.Remove(hashAssetId);
                if (ex is OperationCanceledException) load.completion.TrySetCanceled(token);
                else load.completion.TrySetException(ex);
            }
        }

        protected virtual AsyncOperationHandle<GameObject> LoadClientPrefabAsset(object key)
        {
            return Addressables.LoadAssetAsync<GameObject>(key);
        }

        private void Update()
        {
            if (!UsesClientPrefabLoading || Time.unscaledTime < nextClientPrefabMaintenance) return;
            nextClientPrefabMaintenance = Time.unscaledTime + 1f;
            ReleaseIdleClientPrefabs(Time.unscaledTime);
        }

        internal void ReleaseIdleClientPrefabs(float now)
        {
            idleClientPrefabs.Clear();
            foreach (var entry in clientPrefabLoads)
            {
                if (!entry.Value.loaded) continue;
                bool active = false;
                foreach (var identity in SpawnedObjects.Values)
                    if (identity != null && identity.HashAssetId == entry.Key) { active = true; break; }
                if (active) entry.Value.idleSince = now;
                else if (now - entry.Value.idleSince >= Mathf.Max(1f, clientPrefabIdleSeconds))
                    idleClientPrefabs.Add(entry.Key);
            }
            foreach (int id in idleClientPrefabs)
            {
                var load = clientPrefabLoads[id];
                clientPrefabLoads.Remove(id);
                GuidToPrefabs.Remove(id);
                if (PooledObjects.TryGetValue(id, out var pool))
                {
                    while (pool.Count > 0)
                    {
                        var instance = pool.Dequeue();
                        if (instance != null) Destroy(instance.gameObject);
                    }
                    PooledObjects.Remove(id);
                }
                ReleaseClientPrefabAfterDestruction(load.handle).Forget();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Client Prefabs] Released idle prefab {id} and its unused pool.");
#endif
            }
        }

        private static async UniTask ReleaseClientPrefabAfterDestruction(AsyncOperationHandle<GameObject> handle)
        {
            // Destroyed instances must finish teardown before their AssetBundle can be unloaded.
            await UniTask.DelayFrame(1, PlayerLoopTiming.LastPostLateUpdate);
            if (handle.IsValid()) Addressables.Release(handle);
        }
#endif

        private void ClearClientPrefabLoads()
        {
            if (Manager != null) Manager.ClearClientPrefabMessages();
#if !DISABLE_ADDRESSABLES
            clientPrefabCancellation?.Cancel();
            clientPrefabCancellation?.Dispose();
            clientPrefabCancellation = null;
            foreach (var load in clientPrefabLoads.Values)
                if (load.loaded && load.handle.IsValid()) ReleaseClientPrefabAfterDestruction(load.handle).Forget();
            clientPrefabLoads.Clear();
            clientPrefabReferences.Clear();
#endif
        }

        private void OnDestroy() { ClearClientPrefabLoads(); }
    }
}
