using UnityEngine;

namespace LiteNetLibHighLevel
{
    [System.Serializable]
    public class LiteNetLibScene
    {
        [SerializeField]
        private Object sceneAsset;
        [SerializeField]
        private string sceneName = "";

        public string SceneName
        {
            get { return sceneName; }
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
