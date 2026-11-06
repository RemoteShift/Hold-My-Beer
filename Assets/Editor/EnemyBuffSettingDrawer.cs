using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyBuffSetting))]
public class EnemyBuffSettingDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var enabled = property.FindPropertyRelative("enabled");
        var multiplier = property.FindPropertyRelative("multiplier");
        var toggleRect = new Rect(position.x, position.y, position.width * 0.65f, position.height);
        enabled.boolValue = EditorGUI.ToggleLeft(toggleRect, label, enabled.boolValue);
        var valueRect = new Rect(toggleRect.xMax + 4f, position.y, position.width - toggleRect.width - 4f, position.height);
        using (new EditorGUI.DisabledScope(!enabled.boolValue))
        {
            var prefix = new Rect(valueRect.x, valueRect.y, 14f, valueRect.height);
            EditorGUI.LabelField(prefix, "×");
            valueRect.xMin += 14f;
            multiplier.floatValue = Mathf.Max(1f, EditorGUI.FloatField(valueRect, multiplier.floatValue));
        }
        EditorGUI.EndProperty();
    }
}
