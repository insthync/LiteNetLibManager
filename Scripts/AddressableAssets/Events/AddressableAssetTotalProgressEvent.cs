using UnityEngine.Events;

namespace LiteNetLibManager
{
    /// <summary>
    /// Loaded Count, Total Count
    /// </summary>
    [System.Serializable]
    public class AddressableAssetTotalProgressEvent : UnityEvent<int, int>
    {
    }
}
