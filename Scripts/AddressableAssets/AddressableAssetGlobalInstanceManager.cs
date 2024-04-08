using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LiteNetLibManager
{
    public partial class AddressableAssetGlobalInstanceManager : MonoBehaviour
    {
        public AssetReference[] globalInstancePrefabs = new AssetReference[0];
        [Header("Events")]
        public UnityEvent onStart = new UnityEvent();
        public UnityEvent onEnd = new UnityEvent();
        public UnityEvent onFileSizeRetrieving = new UnityEvent();
        public AddressableAssetFileSizeEvent onFileSizeRetrieved = new AddressableAssetFileSizeEvent();
        public AddressableAssetTotalProgressEvent onDepsDownloading = new AddressableAssetTotalProgressEvent();
        public AddressableAssetTotalProgressEvent onDepsDownloaded = new AddressableAssetTotalProgressEvent();
        public AddressableAssetTotalProgressEvent onDownloading = new AddressableAssetTotalProgressEvent();
        public AddressableAssetTotalProgressEvent onDownloaded = new AddressableAssetTotalProgressEvent();
        public AddressableAssetDownloadProgressEvent onFileDownloading = new AddressableAssetDownloadProgressEvent();

        private long _fileSize;
        private int _loadedCount = 0;
        private int _totalCount = 0;

        private async void Start()
        {
            onStart?.Invoke();
            _totalCount = globalInstancePrefabs.Length;
            for (int i = 0; i < globalInstancePrefabs.Length; ++i)
            {
                await Load(globalInstancePrefabs[i]);
                _loadedCount++;
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
            onDepsDownloaded?.RemoveAllListeners();
            onDepsDownloaded = null;
            onDownloading?.RemoveAllListeners();
            onDownloading = null;
            onDownloaded?.RemoveAllListeners();
            onDownloaded = null;
            onFileDownloading?.RemoveAllListeners();
            onFileDownloading = null;
        }


        private async Task GetSize(AsyncOperationHandle<long> getSizeOp)
        {
            onFileSizeRetrieving?.Invoke();
            while (!getSizeOp.IsDone)
            {
                await Task.Yield();
            }
            _fileSize = getSizeOp.Result;
            onFileSizeRetrieved.Invoke(_fileSize);
        }

        private async Task DownloadDeps(AsyncOperationHandle loadOp)
        {
            onDepsDownloading?.Invoke(_loadedCount, _totalCount);
            while (!loadOp.IsDone)
            {
                onFileDownloading?.Invoke((long)(loadOp.PercentComplete * _fileSize), _fileSize, loadOp.PercentComplete);
                await Task.Yield();
            }
            onDepsDownloaded?.Invoke(_loadedCount, _totalCount);
        }

        private async Task Download(AsyncOperationHandle<GameObject> loadOp)
        {
            onDownloading?.Invoke(_loadedCount, _totalCount);
            while (!loadOp.IsDone)
            {
                onFileDownloading?.Invoke((long)(loadOp.PercentComplete * _fileSize), _fileSize, loadOp.PercentComplete);
                await Task.Yield();
            }
            onDownloaded?.Invoke(_loadedCount, _totalCount);
            // Setup instance as global instance (`DontDestroyOnLoad` not works in `Awake` function properly, so we've to setup it here)
            loadOp.Result.SetActive(true);
            DontDestroyOnLoad(loadOp.Result);
        }

        public async Task Load(AssetReference asset)
        {
            await GetSize(Addressables.GetDownloadSizeAsync(asset.RuntimeKey));
            await DownloadDeps(Addressables.DownloadDependenciesAsync(asset.RuntimeKey));
            await Download(Addressables.InstantiateAsync(asset.RuntimeKey));
        }
    }
}
