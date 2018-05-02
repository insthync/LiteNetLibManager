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
                        sceneName.stringValue = (sceneAsset.objectReferenceValue as SceneAsset).name;
                }
            }
            EditorGUI.EndProperty();
        }
    }
}
