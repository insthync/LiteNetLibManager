using UnityEngine;

namespace LiteNetLibManager
{
    [System.Serializable]
    public struct SceneField
    {
#if UNITY_EDITOR
        [SerializeField]
        private Object sceneAsset;
#endif

        [SerializeField]
        private string sceneName;

#if UNITY_EDITOR
        public Object SceneAsset
        {
            get { return sceneAsset; }
        }
#endif

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

    public static class SceneFieldExtensions
    {
        public static bool IsDataValid(this SceneField scene)
        {
            return scene.IsSet();
        }

        public static bool IsSameScene(this SceneField scene, ServerSceneInfo serverSceneInfo)
        {
            return scene.IsDataValid() && serverSceneInfo.Equals(new ServerSceneInfo()
            {
                isAddressable = false,
                sceneName = scene,
            });
        }

        public static bool IsSameSceneName(this SceneField scene, string sceneName)
        {
            return scene.IsDataValid() && string.Equals(scene.SceneName, sceneName);
        }

        public static ServerSceneInfo GetServerSceneInfo(this SceneField scene)
        {
            return new ServerSceneInfo()
            {
                isAddressable = false,
                sceneName = scene,
            };
        }
    }
}
