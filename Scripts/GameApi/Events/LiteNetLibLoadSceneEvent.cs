using UnityEngine.Events;

namespace LiteNetLibManager
{
    /// <summary>
    /// Args - sceneName: string, isAdditive: bool, isOnline: bool, progress: float
    /// </summary>
    [System.Serializable]
    public class LiteNetLibLoadSceneEvent : UnityEvent<string, bool, bool, float>
    {
    }
}
