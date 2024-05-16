using System.IO;
using UnityEditor;
using UnityEngine;

namespace LiteNetLibManager
{
    [CustomPropertyDrawer(typeof(SceneField))]
    public class SceneFieldPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, GUIContent.none, property);
            SerializedProperty sceneAssetProp = property.FindPropertyRelative("sceneAsset");
            SerializedProperty sceneNameProp = property.FindPropertyRelative("sceneName");
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            if (sceneAssetProp != null)
            {
                EditorGUI.BeginChangeCheck();

                Object value = EditorGUI.ObjectField(position, sceneAssetProp.objectReferenceValue, typeof(SceneAsset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    sceneAssetProp.objectReferenceValue = value;
                    if (sceneAssetProp.objectReferenceValue != null)
                    {
                        string sceneName = (sceneAssetProp.objectReferenceValue as SceneAsset).name;
                        sceneNameProp.stringValue = sceneName;
                        SceneAsset sceneObj = GetSceneObject(sceneName);
                        if (sceneObj == null)
                        {
                            // Just warning, do not change value to null
                            Debug.LogWarning("The scene [" + sceneName + "] cannot be used. To use this scene add it to the build settings for the project");
                        }
                    }
                    else
                    {
                        sceneNameProp.stringValue = null;
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

            foreach (EditorBuildSettingsScene editorScene in EditorBuildSettings.scenes)
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
