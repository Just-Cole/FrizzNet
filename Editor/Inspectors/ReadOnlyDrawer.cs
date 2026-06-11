using UnityEditor;
using UnityEngine;
using FrizzNet.Core;

namespace FrizzNet.Editor
{
    /// <summary>
    /// Custom property drawer that renders fields decorated with the ReadOnlyInspectorAttribute as read-only (disabled) in the Unity Editor.
    /// </summary>
    [CustomPropertyDrawer(typeof(ReadOnlyInspectorAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
}
