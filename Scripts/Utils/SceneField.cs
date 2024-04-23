using UnityEngine;

namespace LiteNetLibManager
{
    [System.Serializable]
    public struct SceneField
    {
        [SerializeField]
        private Object sceneAsset;

        public Object SceneAsset
        {
            get { return sceneAsset; }
        }

        public string overrideSceneName;
        public string SceneName
        {
            get
            {
                if (!string.IsNullOrEmpty(overrideSceneName))
                    return overrideSceneName;
                return sceneAsset.name;
            }
            set { overrideSceneName = value; }
        }

        public static implicit operator string(SceneField unityScene)
        {
            return unityScene.SceneName;
        }

        public bool IsSet()
        {
            return sceneAsset != null && !string.IsNullOrEmpty(sceneAsset.name);
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
                sceneNameOrKey = scene,
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
                sceneNameOrKey = scene,
            };
        }
    }
}
