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
}