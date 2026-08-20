using UnityEditor;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>Draws consistently styled buttons and text boxes for accessible editor windows.</summary>
    public static class AccessibleControls
    {
        public static bool Button(string label, bool isSelected)
        {
            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            return Button(row, label, isSelected);
        }

        public static bool Button(Rect row, string label, bool isSelected, GUIStyle style = null)
        {
            AccessibleEditorStyles.DrawSelection(row, isSelected);
            return GUI.Button(row, label, style ?? EditorStyles.label);
        }

        public static string TextBox(Rect row, string controlName, string label, string value, bool focus)
        {
            GUI.SetNextControlName(controlName);
            string updatedValue = EditorGUI.TextField(row, label, value ?? string.Empty);
            if (focus)
            {
                EditorGUI.FocusTextInControl(controlName);
            }

            return updatedValue;
        }

        public static string ToolbarSearch(string controlName, string label, string value, bool focus)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.SetNextControlName(controlName);
            string updatedValue = EditorGUILayout.TextField(new GUIContent(label), value ?? string.Empty,
                EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            if (focus)
            {
                GUI.FocusControl(controlName);
            }

            return updatedValue;
        }
    }
}
