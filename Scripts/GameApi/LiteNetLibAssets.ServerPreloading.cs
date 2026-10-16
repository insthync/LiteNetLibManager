using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace LiteNetLibManager
{
    public partial class LiteNetLibAssets
    {
        private int assetInitializationGeneration;
        private readonly List<IDisposable> serverPrefabAssetLeases = new List<IDisposable>();

        private async UniTask<bool> PreloadServerPrefabAssets(int generation)
        {
            if (Manager == null || !Manager.IsServer) return true;
            var leases = new List<IDisposable>();
            try
            {
                foreach (var prefab in GuidToPrefabs.Values)
                {
                    if (prefab == null) continue;
                    foreach (var preloader in prefab.GetComponentsInChildren<ILiteNetLibPrefabAssetsPreloader>(true))
                    {
                        var lease = await preloader.PreloadPrefabAssets();
                        if (lease != null) leases.Add(lease);
                        if (generation != assetInitializationGeneration)
                        {
                            foreach (var acquired in leases) acquired.Dispose();
                            return false;
                        }
                    }
                }
                serverPrefabAssetLeases.AddRange(leases);
                return true;
            }
            catch
            {
                foreach (var lease in leases) lease.Dispose();
                throw;
            }
        }

        private void ReleaseServerPrefabAssets()
        {
            ++assetInitializationGeneration;
            if (serverPrefabAssetLeases.Count == 0) return;
            var leases = serverPrefabAssetLeases.ToArray();
            serverPrefabAssetLeases.Clear();
            ReleaseServerPrefabAssetsAfterDestruction(leases).Forget();
        }

        private static async UniTask ReleaseServerPrefabAssetsAfterDestruction(IDisposable[] leases)
        {
            await UniTask.DelayFrame(1, PlayerLoopTiming.LastPostLateUpdate);
            foreach (var lease in leases) lease.Dispose();
        }
    }
}
