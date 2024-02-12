using UnityEngine;
using UnityEngine.AddressableAssets;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LiteNetLibManager
{
    [System.Serializable]
    public class AssetReferenceScene : AssetReference
    {
        [SerializeField]
        private string sceneName = string.Empty;

        public string SceneName
        {
            get { return sceneName; }
        }

#if UNITY_EDITOR
        public AssetReferenceScene(SceneAsset scene)
        : base(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(scene)))
        {
            sceneName = scene.name;
        }

        public override bool ValidateAsset(string path)
        {
            return ValidateAsset(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
        }

        public override bool ValidateAsset(Object obj)
        {
            return (obj != null) && (obj is SceneAsset);
        }

        public override bool SetEditorAsset(Object value)
        {
            if (!base.SetEditorAsset(value))
            {
                return false;
            }

            if (value is SceneAsset scene)
            {
                sceneName = scene.name;
                return true;
            }
            else
            {
                sceneName = string.Empty;
                return false;
            }
        }

#endif
    }

    public static class AssetReferenceSceneExtensions
    {
        public static bool IsDataValid(this AssetReferenceScene scene)
        {
            return scene != null && scene.IsValid() && scene.RuntimeKeyIsValid();
        }

        public static bool IsSameScene(this AssetReferenceScene scene, ServerSceneInfo serverSceneInfo)
        {
            return scene.IsDataValid() && serverSceneInfo.Equals(new ServerSceneInfo()
            {
                isAddressable = true,
                sceneNameOrKey = scene.RuntimeKey as string,
            });
        }

        public static bool IsSameSceneName(this AssetReferenceScene scene, string sceneName)
        {
            return scene.IsDataValid() && string.Equals(scene.SceneName, sceneName);
        }

        public static ServerSceneInfo GetServerSceneInfo(this AssetReferenceScene scene)
        {
            return new ServerSceneInfo()
            {
                isAddressable = true,
                sceneNameOrKey = scene.RuntimeKey as string,
            };
        }
    }
}