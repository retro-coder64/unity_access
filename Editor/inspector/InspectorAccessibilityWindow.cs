using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>
    /// Observes hierarchy selection and exposes editable transform values to NVDA.
    /// </summary>
    public sealed class InspectorAccessibilityWindow : EditorWindow
    {
        private const string WindowTitle = "Accessible Inspector";
        private const string EditControlName = "AccessibleInspectorValue";
        private readonly List<InspectorItem> items = new List<InspectorItem>();
        private GameObject inspectedObject;
        private Vector2 scrollPosition;
        private int selectedIndex;
        private bool isEditing;
        private string editValue = string.Empty;

        /// <summary>
        /// Opens the inspector for the current shared or Unity selection.
        /// </summary>
        [MenuItem("Unity Access/Inspector", false, 2)]
        public static void Open()
        {
            GameObject selectedObject = SharedSelection.CurrentObject != null
                ? SharedSelection.CurrentObject
                : Selection.activeGameObject;

            if (selectedObject == null)
            {
                SpeakSafely("Select a scene object in the accessible hierarchy first.");
                return;
            }

            OpenForObject(selectedObject);
        }

        /// <summary>
        /// Opens and refreshes the inspector in response to the shared selection observer.
        /// </summary>
        internal static void OpenForObject(GameObject selectedObject)
        {
            try
            {
                InspectorAccessibilityWindow window = GetWindow<InspectorAccessibilityWindow>();
                window.titleContent = new GUIContent(WindowTitle);
                window.minSize = new Vector2(380.0f, 220.0f);
                window.SetInspectedObject(selectedObject);
                window.Show();
                window.Focus();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(InspectorAccessibilityWindow), exception);
            }
        }

        private void OnGUI()
        {
            try
            {
                HandleKeyboardInput(Event.current);
                DrawInspector();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(InspectorAccessibilityWindow), exception);
            }
        }

        private void DrawInspector()
        {
            if (inspectedObject == null)
            {
                EditorGUILayout.LabelField("No scene object selected.");
                return;
            }

            EditorGUILayout.LabelField(inspectedObject.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Up and Down navigate. Enter edits a value. Escape returns to the hierarchy.");
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int index = 0; index < items.Count; index++)
            {
                Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                if (index == selectedIndex)
                {
                    EditorGUI.DrawRect(row, new Color(0.24f, 0.49f, 0.90f, 0.45f));
                }

                InspectorItem item = items[index];
                if (isEditing && index == selectedIndex && item.IsEditable)
                {
                    GUI.SetNextControlName(EditControlName);
                    editValue = EditorGUI.TextField(row, item.Label, editValue);
                    EditorGUI.FocusTextInControl(EditControlName);
                }
                else
                {
                    EditorGUI.LabelField(row, item.Label + ": " + item.Value);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void HandleKeyboardInput(Event currentEvent)
        {
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (isEditing)
            {
                if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
                {
                    CommitEdit();
                    currentEvent.Use();
                }
                else if (currentEvent.keyCode == KeyCode.Escape)
                {
                    isEditing = false;
                    SpeakSafely("Edit cancelled. " + GetSelectedItemDescription());
                    currentEvent.Use();
                }

                return;
            }

            if (currentEvent.keyCode == KeyCode.UpArrow)
            {
                MoveSelection(-1);
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.DownArrow)
            {
                MoveSelection(1);
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                BeginEdit();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                currentEvent.Use();
                Close();
                // Wait until this OnGUI cycle has ended so Unity cannot restore focus to the closing window.
                EditorApplication.delayCall += SharedSelection.RequestHierarchyReturn;
            }
        }

        private void MoveSelection(int direction)
        {
            if (items.Count == 0)
            {
                SpeakSafely("The selected object has no inspectable properties.");
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, items.Count - 1);
            SpeakSafely(GetSelectedItemDescription() + ", " + (selectedIndex + 1) + " of " + items.Count + ".");
            Repaint();
        }

        private void BeginEdit()
        {
            if (items.Count == 0 || selectedIndex < 0 || selectedIndex >= items.Count)
            {
                return;
            }

            InspectorItem item = items[selectedIndex];
            if (!item.IsEditable)
            {
                SpeakSafely(item.Label + ", read only.");
                return;
            }

            editValue = item.Value;
            isEditing = true;
            SpeakSafely("Editing " + item.Label + ". Current value " + item.Value + ". Type a number and press Enter. Escape cancels.");
            Repaint();
        }

        private void CommitEdit()
        {
            float parsedValue;
            if (!float.TryParse(editValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue) ||
                float.IsNaN(parsedValue) || float.IsInfinity(parsedValue))
            {
                SpeakSafely("Invalid number. Use digits, a minus sign, and a decimal point.");
                return;
            }

            InspectorItem item = items[selectedIndex];
            Undo.RecordObject(inspectedObject.transform, "Unity Access edit " + item.Label);
            item.SetValue(parsedValue);
            EditorUtility.SetDirty(inspectedObject.transform);
            isEditing = false;
            RefreshItems();
            SpeakSafely(item.Label + " changed to " + FormatFloat(parsedValue) + ".");
            Repaint();
        }

        private void SetInspectedObject(GameObject selectedObject)
        {
            if (selectedObject == null)
            {
                return;
            }

            inspectedObject = selectedObject;
            selectedIndex = 0;
            isEditing = false;
            RefreshItems();
            SpeakSafely("Inspector opened for " + inspectedObject.name + ". " + items.Count + " properties. " + GetSelectedItemDescription() + ".");
            Repaint();
        }

        private void RefreshItems()
        {
            items.Clear();
            if (inspectedObject == null)
            {
                return;
            }

            Transform objectTransform = inspectedObject.transform;
            AddTransformItem("X position", () => objectTransform.localPosition.x, value => SetPositionAxis(0, value));
            AddTransformItem("Y position", () => objectTransform.localPosition.y, value => SetPositionAxis(1, value));
            AddTransformItem("Z position", () => objectTransform.localPosition.z, value => SetPositionAxis(2, value));
            AddTransformItem("X rotation", () => objectTransform.localEulerAngles.x, value => SetRotationAxis(0, value));
            AddTransformItem("Y rotation", () => objectTransform.localEulerAngles.y, value => SetRotationAxis(1, value));
            AddTransformItem("Z rotation", () => objectTransform.localEulerAngles.z, value => SetRotationAxis(2, value));

            Component[] components = inspectedObject.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                string componentName = component == null ? "Missing script" : component.GetType().Name;
                items.Add(new InspectorItem("Component " + (index + 1), componentName, null));
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, Math.Max(0, items.Count - 1));
        }

        private void AddTransformItem(string label, Func<float> getter, Action<float> setter)
        {
            items.Add(new InspectorItem(label, FormatFloat(getter()), setter));
        }

        private void SetPositionAxis(int axis, float value)
        {
            Vector3 position = inspectedObject.transform.localPosition;
            position[axis] = value;
            inspectedObject.transform.localPosition = position;
        }

        private void SetRotationAxis(int axis, float value)
        {
            Vector3 rotation = inspectedObject.transform.localEulerAngles;
            rotation[axis] = value;
            inspectedObject.transform.localEulerAngles = rotation;
        }

        private string GetSelectedItemDescription()
        {
            if (items.Count == 0 || selectedIndex < 0 || selectedIndex >= items.Count)
            {
                return "No inspectable properties";
            }

            InspectorItem item = items[selectedIndex];
            return item.Label + ", " + item.Value + (item.IsEditable ? ", editable" : ", read only");
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void SpeakSafely(string message)
        {
            try
            {
                NvdaApi.Speak(message);
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(InspectorAccessibilityWindow), exception);
            }
        }

        private sealed class InspectorItem
        {
            private readonly Action<float> setter;

            internal InspectorItem(string label, string value, Action<float> setter)
            {
                Label = label;
                Value = value;
                this.setter = setter;
            }

            internal string Label { get; private set; }

            internal string Value { get; private set; }

            internal bool IsEditable
            {
                get { return setter != null; }
            }

            internal void SetValue(float value)
            {
                if (setter == null)
                {
                    throw new InvalidOperationException("This inspector item is read only.");
                }

                setter(value);
            }
        }
    }

    /// <summary>
    /// Connects the shared selection event to the inspector without hierarchy coupling.
    /// </summary>
    [InitializeOnLoad]
    internal static class InspectorSelectionObserver
    {
        static InspectorSelectionObserver()
        {
            SharedSelection.SelectionChanged += InspectorAccessibilityWindow.OpenForObject;
        }
    }
}
