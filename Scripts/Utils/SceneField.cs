using UnityEngine;

namespace LiteNetLibManager
{
    [System.Serializable]
    public struct SceneField
    {
        [SerializeField]
        private Object sceneAsset;
        [SerializeField]
        private string sceneName;

        public Object SceneAsset
        {
            get { return sceneAsset; }
        }

        public string SceneName
        {
            get { return sceneName; }
            set { sceneName = value; }
        }

        public static implicit operator string(SceneField unityScene)
        {
            return unityScene.SceneName;
        }

        public bool IsSet()
        {
            return !string.IsNullOrEmpty(sceneName);
        }
    }
}
