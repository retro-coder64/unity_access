using UnityEditor;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>Standardises selection, position text, scrolling, and row drawing for accessible lists.</summary>
    public static class AccessibleList
    {
        public static int Move(int selectedIndex, int direction, int itemCount)
        {
            return itemCount <= 0 ? -1 : Mathf.Clamp(selectedIndex + direction, 0, itemCount - 1);
        }

        public static int Clamp(int selectedIndex, int itemCount)
        {
            return itemCount <= 0 ? -1 : Mathf.Clamp(selectedIndex, 0, itemCount - 1);
        }

        public static string Position(int selectedIndex, int itemCount)
        {
            return (selectedIndex + 1) + " of " + itemCount;
        }

        public static void KeepVisible(ref Vector2 scrollPosition, int selectedIndex, float rowHeight)
        {
            if (selectedIndex >= 0)
            {
                scrollPosition.y = Mathf.Max(0.0f, (selectedIndex - 2) * rowHeight);
            }
        }

        public static Rect DrawLabelRow(string text, bool isSelected, GUIStyle style = null, float height = 0.0f)
        {
            float rowHeight = height > 0.0f ? height : EditorGUIUtility.singleLineHeight;
            Rect row = EditorGUILayout.GetControlRect(false, rowHeight);
            AccessibleEditorStyles.DrawSelection(row, isSelected);
            EditorGUI.LabelField(row, text, style ?? EditorStyles.label);
            return row;
        }
    }
}
