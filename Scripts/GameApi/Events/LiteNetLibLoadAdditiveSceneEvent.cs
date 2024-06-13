using UnityEngine.Events;

namespace LiteNetLibManager
{
    /// <summary>
    /// Args - loadedFileCount: int, totalFileCount: int
    /// </summary>
    [System.Serializable]
    public class LiteNetLibLoadAdditiveSceneEvent : UnityEvent<int, int>
    {
    }
}
