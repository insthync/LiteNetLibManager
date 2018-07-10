using System.IO;
using UnityEditor;
using UnityEngine;

namespace LiteNetLibManager
{
    [CustomPropertyDrawer(typeof(LiteNetLibScene))]
    public class LiteNetLibScenePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label)
        {
            EditorGUI.BeginProperty(_position, GUIContent.none, _property);
            SerializedProperty sceneAsset = _property.FindPropertyRelative("sceneAsset");
            SerializedProperty sceneName = _property.FindPropertyRelative("sceneName");
            _position = EditorGUI.PrefixLabel(_position, GUIUtility.GetControlID(FocusType.Passive), _label);
            if (sceneAsset != null)
            {
                EditorGUI.BeginChangeCheck();

                Object value = EditorGUI.ObjectField(_position, sceneAsset.objectReferenceValue, typeof(SceneAsset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    sceneAsset.objectReferenceValue = value;
                    if (sceneAsset.objectReferenceValue != null)
                    {
                        var _sceneName = (sceneAsset.objectReferenceValue as SceneAsset).name;
                        sceneName.stringValue = _sceneName;
                        var sceneObj = GetSceneObject(_sceneName);
                        if (sceneObj == null)
                        {
                            Debug.LogWarning("The scene [" + _sceneName + "] cannot be used. To use this scene add it to the build settings for the project");
                            sceneAsset.objectReferenceValue = null;
                            sceneName.stringValue = null;
                        }
                    }
                }
            }
            EditorGUI.EndProperty();
        }

        protected SceneAsset GetSceneObject(string sceneObjectName)
        {
            if (string.IsNullOrEmpty(sceneObjectName))
            {
                return null;
            }

            foreach (var editorScene in EditorBuildSettings.scenes)
            {
                var sceneNameWithoutExtension = Path.GetFileNameWithoutExtension(editorScene.path);
                if (sceneNameWithoutExtension == sceneObjectName)
                {
                    return AssetDatabase.LoadAssetAtPath(editorScene.path, typeof(SceneAsset)) as SceneAsset;
                }
            }
            return null;
        }
    }
}
