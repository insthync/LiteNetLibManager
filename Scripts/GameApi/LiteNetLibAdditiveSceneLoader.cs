using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace LiteNetLibManager
{
    public class LiteNetLibAdditiveSceneLoader : MonoBehaviour
    {
        public SceneField[] scenes = new SceneField[0];
        public AssetReferenceScene[] addressableScenes = new AssetReferenceScene[0];

        public async UniTask LoadAll(LiteNetLibGameManager manager, string sceneName, bool isOnline)
        {
            int loadCount = 0;
            // Load scene
            for (int i = 0; i < scenes.Length; ++i)
            {
                string loadingName = $"{sceneName}_{loadCount++}";
                // Load the scene
                manager.Assets.onLoadSceneStart.Invoke(loadingName, true, isOnline, 0f);
                var op = SceneManager.LoadSceneAsync(
                    scenes[i].SceneName,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                while (!op.isDone)
                {
                    await UniTask.NextFrame();
                    manager.Assets.onLoadSceneProgress.Invoke(loadingName, true, isOnline, op.progress);
                }
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
                    manager.Assets.onSceneDepsDownloaded.Invoke);
                // Load the scene
                manager.Assets.onLoadSceneStart.Invoke(loadingName, true, isOnline, 0f);
                var op = Addressables.LoadSceneAsync(
                    addressableScenes[i].RuntimeKey,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                while (!op.IsDone)
                {
                    await UniTask.NextFrame();
                    manager.Assets.onLoadSceneProgress.Invoke(loadingName, true, isOnline, op.PercentComplete);
                }
                manager.Assets.onLoadSceneFinish.Invoke(loadingName, true, isOnline, 1f);
                manager.LoadedAdditiveScenesCount++;
                manager.Assets.onLoadAdditiveSceneProgress.Invoke(manager.LoadedAdditiveScenesCount, manager.TotalAdditiveScensCount);
            }
        }
    }
}
