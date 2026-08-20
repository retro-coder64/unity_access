using UnityEditor;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>Provides the shared visual language for accessible editor controls.</summary>
    public static class AccessibleEditorStyles
    {
        public static readonly Color SelectionColor = new Color(0.24f, 0.49f, 0.90f, 0.45f);

        /// <summary>Draws the standard highlight behind the currently selected control.</summary>
        public static void DrawSelection(Rect rect, bool isSelected)
        {
            if (isSelected)
            {
                EditorGUI.DrawRect(rect, SelectionColor);
            }
        }
    }
}
