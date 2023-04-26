using UnityEngine.Events;

namespace LiteNetLibManager
{
    [System.Serializable]
    public class LiteNetLibLoadSceneEvent : UnityEvent<string, bool, float>
    {
    }
}
