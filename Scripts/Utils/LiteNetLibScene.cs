using UnityEngine;

namespace LiteNetLibManager
{
    [System.Serializable]
    public class LiteNetLibScene
    {
        [SerializeField]
        public Object sceneAsset;
        [SerializeField]
        public string sceneName = string.Empty;

        public string SceneName
        {
            get { return sceneName; }
            set { sceneName = value; }
        }

        public static implicit operator string(LiteNetLibScene unityScene)
        {
            return unityScene.SceneName;
        }

        public bool IsSet()
        {
            return !string.IsNullOrEmpty(sceneName);
        }
    }
}
