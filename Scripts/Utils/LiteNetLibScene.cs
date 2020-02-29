using UnityEngine;

namespace LiteNetLibManager
{
    [System.Serializable]
    public struct LiteNetLibScene
    {
        [SerializeField]
        private Object sceneAsset;
        [SerializeField]
        private string sceneName;

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
