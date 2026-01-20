using Cysharp.Threading.Tasks;
using Insthync.AddressableAssetTools;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace LiteNetLibManager
{
    public class LiteNetLibAdditiveSceneLoader : MonoBehaviour
    {
        public SceneField[] scenes = new SceneField[0];
        public AssetReferenceScene[] addressableScenes = new AssetReferenceScene[0];
#if UNITY_SERVER || UNITY_EDITOR
        public SceneField[] serverOnlyScenes = new SceneField[0];
        public AssetReferenceScene[] serverOnlyAddressableScenes = new AssetReferenceScene[0];
#endif
#if !UNITY_SERVER || UNITY_EDITOR
        public SceneField[] clientOnlyScenes = new SceneField[0];
        public AssetReferenceScene[] clientOnlyAddressableScenes = new AssetReferenceScene[0];
#endif
#if UNITY_EDITOR
        public SceneField[] editorOnlyScenes = new SceneField[0];
        public AssetReferenceScene[] editorOnlyAddressableScenes = new AssetReferenceScene[0];
#endif

        public int GetTotalLoadCount()
        {
            int total = 0;
            total += scenes.Length + addressableScenes.Length;
#if UNITY_SERVER
            total += serverOnlyScenes.Length + serverOnlyAddressableScenes.Length;
#endif
#if !UNITY_SERVER
            total += clientOnlyScenes.Length + clientOnlyAddressableScenes.Length;
#endif
#if UNITY_EDITOR
            total += editorOnlyScenes.Length + editorOnlyAddressableScenes.Length;
#endif
            return total;
        }

        public async UniTask LoadAll(LiteNetLibGameManager manager, string sceneName, bool isOnline)
        {
            int loadCount = 0;
            loadCount = await LoadAll(manager, sceneName, isOnline, scenes, addressableScenes, loadCount);
#if UNITY_SERVER
            loadCount = await LoadAll(manager, sceneName, isOnline, serverOnlyScenes, serverOnlyAddressableScenes, loadCount);
#endif
#if !UNITY_SERVER
            loadCount = await LoadAll(manager, sceneName, isOnline, clientOnlyScenes, clientOnlyAddressableScenes, loadCount);
#endif
#if UNITY_EDITOR
            loadCount = await LoadAll(manager, sceneName, isOnline, editorOnlyScenes, editorOnlyAddressableScenes, loadCount);
#endif
        }

        public async UniTask<int> LoadAll(LiteNetLibGameManager manager, string sceneName, bool isOnline, SceneField[] scenes, AssetReferenceScene[] addressableScenes, int loadCount)
        {
            // Load scene
            for (int i = 0; i < scenes.Length; ++i)
            {
                string loadingName = $"{sceneName}_{loadCount++}";
                // Load the scene
                await UniTask.Yield();
                manager.Assets.onLoadSceneStart.Invoke(loadingName, true, isOnline, 0f);
                var op = SceneManager.LoadSceneAsync(
                    scenes[i].SceneName,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                while (!op.isDone)
                {
                    await UniTask.Yield();
                    manager.Assets.onLoadSceneProgress.Invoke(loadingName, true, isOnline, op.progress);
                }
                await UniTask.Yield();
                manager.Assets.onLoadSceneFinish.Invoke(loadingName, true, isOnline, 1f);
                manager.LoadedAdditiveScenesCount++;
                manager.Assets.onLoadAdditiveSceneProgress.Invoke(manager.LoadedAdditiveScenesCount, manager.TotalAdditiveScensCount);
            }
            // Load from addressable
            for (int i = 0; i < addressableScenes.Length; ++i)
            {
                string loadingName = $"{sceneName}_{loadCount++}";
                // Download the scene
                await AddressableAssetDownloadManager.Download(
                    addressableScenes[i].RuntimeKey,
                    manager.Assets.onSceneFileSizeRetrieving.Invoke,
                    manager.Assets.onSceneFileSizeRetrieved.Invoke,
                    manager.Assets.onSceneDepsDownloading.Invoke,
                    manager.Assets.onSceneDepsFileDownloading.Invoke,
                    manager.Assets.onSceneDepsDownloaded.Invoke,
                    null);
                // Load the scene
                manager.Assets.onLoadSceneStart.Invoke(loadingName, true, isOnline, 0f);
                var op = Addressables.LoadSceneAsync(
                    addressableScenes[i].RuntimeKey,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                AddressableAssetsManager.AddAddressableSceneHandle(op);
                while (!op.IsDone)
                {
                    await UniTask.Yield();
                    float percentageComplete = op.GetDownloadStatus().Percent;
                    manager.Assets.onLoadSceneProgress.Invoke(loadingName, true, isOnline, percentageComplete);
                }
                await UniTask.Yield();
                manager.Assets.onLoadSceneFinish.Invoke(loadingName, true, isOnline, 1f);
                manager.LoadedAdditiveScenesCount++;
                manager.Assets.onLoadAdditiveSceneProgress.Invoke(manager.LoadedAdditiveScenesCount, manager.TotalAdditiveScensCount);
            }
            return loadCount;
        }
    }
}
