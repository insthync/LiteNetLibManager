using UnityEngine.Events;

namespace LiteNetLibManager
{
    /// <summary>
    /// Args - downloadedSize: long, fileSize: long, percentComplete: float (1 = 100%)
    /// </summary>
    [System.Serializable]
    public class AddressableAssetDownloadProgressEvent : UnityEvent<long, long, float>
    {
    }
}
