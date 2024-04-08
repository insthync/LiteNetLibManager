using UnityEngine.Events;

namespace LiteNetLibManager
{
    /// <summary>
    /// Downloaded size, File size, Percent complete (1 = 100%)
    /// </summary>
    [System.Serializable]
    public class AddressableAssetDownloadProgressEvent : UnityEvent<long, long, float>
    {
    }
}
