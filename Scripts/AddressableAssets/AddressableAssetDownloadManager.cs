using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace LiteNetLibManager
{
    public partial class AddressableAssetDownloadManager : MonoBehaviour
    {
        public AssetReferenceDownloadManagerSettings settingsAssetReference;
        [Header("Events")]
        public UnityEvent onStart = new UnityEvent();
        public UnityEvent onEnd = new UnityEvent();
        public UnityEvent onFileSizeRetrieving = new UnityEvent();
        public AddressableAssetFileSizeEvent onFileSizeRetrieved = new AddressableAssetFileSizeEvent();
        public AddressableAssetTotalProgressEvent onDepsDownloading = new AddressableAssetTotalProgressEvent();
        public AddressableAssetTotalProgressEvent onDepsDownloaded = new AddressableAssetTotalProgressEvent();
        public AddressableAssetDownloadProgressEvent onDepsFileDownloading = new AddressableAssetDownloadProgressEvent();
        public UnityEvent onDownloadedAll = new UnityEvent();

        public long FileSize { get; protected set; } = 0;
        public int LoadedCount { get; protected set; } = 0;
        public int TotalCount { get; protected set; } = 0;

        private async void Start()
        {
            await UniTask.Yield();
            onStart?.Invoke();
            AsyncOperationHandle<AddressableAssetDownloadManagerSettings> settingsAsyncOp = settingsAssetReference.LoadAssetAsync();
            await settingsAsyncOp.Task;
            AddressableAssetDownloadManagerSettings settings = settingsAsyncOp.Result;
            TotalCount = settings.PrepareObjects.Count + settings.InitialObjects.Count;

            // Downloads
            for (int i = 0; i < settings.PrepareObjects.Count; ++i)
            {
                if (settings.PrepareObjects[i] == null ||
                    !settings.PrepareObjects[i].IsDataValid())
                {
                    // Invalid data
                    continue;
                }
                try
                {
                    await Download(
                        settings.PrepareObjects[i],
                        OnFileSizeRetrieving,
                        OnFileSizeRetrieved,
                        OnDepsDownloading,
                        OnDepsFileDownloading,
                        OnDepsDownloaded);
                }
                catch (System.Exception ex)
                {
                    Logging.LogException(ex);
                }
                LoadedCount++;
            }
            for (int i = 0; i < settings.InitialObjects.Count; ++i)
            {
                if (settings.InitialObjects[i] == null ||
                    !settings.InitialObjects[i].IsDataValid())
                {
                    // Invalid data
                    continue;
                }
                try
                {
                    await Download(
                        settings.InitialObjects[i],
                        OnFileSizeRetrieving,
                        OnFileSizeRetrieved,
                        OnDepsDownloading,
                        OnDepsFileDownloading,
                        OnDepsDownloaded);
                }
                catch (System.Exception ex)
                {
                    Logging.LogException(ex);
                }
                LoadedCount++;
            }
            await UniTask.Yield();
            onDownloadedAll?.Invoke();
            // Instantiates
            for (int i = 0; i < settings.InitialObjects.Count; ++i)
            {
                try
                {
                    AsyncOperationHandle<GameObject> instantiateOp = Addressables.InstantiateAsync(settings.InitialObjects[i].RuntimeKey);
                    await instantiateOp.Task;
                    Logging.Log($"Initialized {instantiateOp.Result.name}");
                }
                catch (System.Exception ex)
                {
                    Logging.LogException(ex);
                }
            }

            onEnd?.Invoke();
        }

        private void OnDestroy()
        {
            onStart?.RemoveAllListeners();
            onStart = null;
            onEnd?.RemoveAllListeners();
            onEnd = null;
            onFileSizeRetrieving?.RemoveAllListeners();
            onFileSizeRetrieving = null;
            onFileSizeRetrieved?.RemoveAllListeners();
            onFileSizeRetrieved = null;
            onDepsDownloading?.RemoveAllListeners();
            onDepsDownloading = null;
            onDepsFileDownloading?.RemoveAllListeners();
            onDepsFileDownloading = null;
            onDepsDownloaded?.RemoveAllListeners();
            onDepsDownloaded = null;
            onDownloadedAll?.RemoveAllListeners();
            onDownloadedAll = null;
        }

        protected virtual void OnFileSizeRetrieving()
        {
            FileSize = 0;
            onFileSizeRetrieving?.Invoke();
        }

        protected virtual void OnFileSizeRetrieved(long fileSize)
        {
            FileSize = fileSize;
            onFileSizeRetrieved?.Invoke(fileSize);
        }

        protected virtual void OnDepsDownloading()
        {
            onDepsDownloading?.Invoke(LoadedCount, TotalCount);
        }

        protected virtual void OnDepsFileDownloading(long downloadSize, long fileSize, float percentComplete)
        {
            onDepsFileDownloading?.Invoke(downloadSize, fileSize, percentComplete);
        }

        protected virtual void OnDepsDownloaded()
        {
            onDepsDownloaded?.Invoke(LoadedCount, TotalCount);
        }

        public static async Task<SceneInstance> DownloadAndLoadScene(
            object runtimeKey,
            LoadSceneParameters loadSceneParameters,
            System.Action onFileSizeRetrieving,
            AddressableAssetFileSizeDelegate onFileSizeRetrieved,
            System.Action onDepsDownloading,
            AddressableAssetDownloadProgressDelegate onDepsFileDownloading,
            System.Action onDepsDownloaded)
        {
            await Download(runtimeKey, onFileSizeRetrieving, onFileSizeRetrieved, onDepsDownloading, onDepsFileDownloading, onDepsDownloaded);
            AsyncOperationHandle<SceneInstance> loadSceneOp = Addressables.LoadSceneAsync(runtimeKey, loadSceneParameters);
            while (!loadSceneOp.IsDone)
            {
                await UniTask.Yield();
            }
            return loadSceneOp.Result;
        }

        public static async Task<GameObject> DownloadAndInstantiate(
            object runtimeKey,
            System.Action onFileSizeRetrieving,
            AddressableAssetFileSizeDelegate onFileSizeRetrieved,
            System.Action onDepsDownloading,
            AddressableAssetDownloadProgressDelegate onDepsFileDownloading,
            System.Action onDepsDownloaded)
        {
            await Download(runtimeKey, onFileSizeRetrieving, onFileSizeRetrieved, onDepsDownloading, onDepsFileDownloading, onDepsDownloaded);
            AsyncOperationHandle<GameObject> instantiateOp = Addressables.InstantiateAsync(runtimeKey);
            while (!instantiateOp.IsDone)
            {
                await UniTask.Yield();
            }
            return instantiateOp.Result;
        }

        public static async Task Download(
            object runtimeKey,
            System.Action onFileSizeRetrieving,
            AddressableAssetFileSizeDelegate onFileSizeRetrieved,
            System.Action onDepsDownloading,
            AddressableAssetDownloadProgressDelegate onDepsFileDownloading,
            System.Action onDepsDownloaded)
        {
            // Get download size
            AsyncOperationHandle<long> getSizeOp;
            try
            {
                getSizeOp = Addressables.GetDownloadSizeAsync(runtimeKey);
                onFileSizeRetrieving?.Invoke();
                while (!getSizeOp.IsDone)
                {
                    await UniTask.Yield();
                }
            }
            catch
            {
                return;
            }
            await UniTask.Yield();
            long fileSize = getSizeOp.Result;
            onFileSizeRetrieved.Invoke(fileSize);
            // Download dependencies
            if (fileSize > 0)
            {
                AsyncOperationHandle downloadOp;
                try
                {
                    downloadOp = Addressables.DownloadDependenciesAsync(runtimeKey);
                    await UniTask.Yield();
                    onDepsDownloading?.Invoke();
                    while (!downloadOp.IsDone)
                    {
                        await UniTask.Yield();
                        float percentageComplete = downloadOp.GetDownloadStatus().Percent;
                        onDepsFileDownloading?.Invoke((long)(percentageComplete * fileSize), fileSize, percentageComplete);
                    }
                }
                catch
                {
                    return;
                }
                await UniTask.Yield();
                onDepsDownloaded?.Invoke();
                Addressables.ReleaseInstance(downloadOp);
            }
            else
            {
                onDepsDownloading?.Invoke();
                onDepsFileDownloading?.Invoke(0, 0, 1);
                onDepsDownloaded?.Invoke();
            }
        }
    }
}
