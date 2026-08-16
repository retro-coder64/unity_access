using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private readonly List<Type> componentTypes = new List<Type>();
        private bool isAddingComponent;
        private int selectedComponentTypeIndex;
        private Component inspectedComponent;
        private readonly List<ComponentPropertyItem> componentProperties = new List<ComponentPropertyItem>();
        private int selectedPropertyIndex;
        private bool isChoosingOption;
        private int selectedOptionIndex;

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

            EditorGUILayout.LabelField(inspectedComponent == null ? inspectedObject.name : inspectedComponent.GetType().Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(isAddingComponent
                ? "Choose a component with Up and Down. Enter adds it. Escape cancels."
                : inspectedComponent != null
                    ? "Up and Down navigate properties. Enter edits or activates. Escape returns to the inspector."
                    : "Up and Down navigate. Enter edits or activates. Backspace removes a component. Escape returns to the hierarchy.");
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (isAddingComponent)
            {
                DrawComponentList();
                EditorGUILayout.EndScrollView();
                return;
            }

            if (inspectedComponent != null)
            {
                DrawComponentProperties();
                EditorGUILayout.EndScrollView();
                return;
            }

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

        private void DrawComponentProperties()
        {
            for (int index = 0; index < componentProperties.Count; index++)
            {
                Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                if (index == selectedPropertyIndex)
                {
                    EditorGUI.DrawRect(row, new Color(0.24f, 0.49f, 0.90f, 0.45f));
                }

                ComponentPropertyItem item = componentProperties[index];
                if (isEditing && index == selectedPropertyIndex)
                {
                    GUI.SetNextControlName(EditControlName);
                    editValue = EditorGUI.TextField(row, item.Label, editValue);
                    EditorGUI.FocusTextInControl(EditControlName);
                }
                else
                {
                    string value = isChoosingOption && index == selectedPropertyIndex
                        ? item.Options[selectedOptionIndex]
                        : item.Value;
                    EditorGUI.LabelField(row, item.Label + ": " + value);
                }
            }
        }

        private void DrawComponentList()
        {
            for (int index = 0; index < componentTypes.Count; index++)
            {
                Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                if (index == selectedComponentTypeIndex)
                {
                    EditorGUI.DrawRect(row, new Color(0.24f, 0.49f, 0.90f, 0.45f));
                }

                EditorGUI.LabelField(row, GetComponentDisplayName(componentTypes[index]));
            }
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
                    SpeakSafely("Edit cancelled. " + (inspectedComponent == null
                        ? GetSelectedItemDescription()
                        : GetSelectedPropertyDescription()));
                    currentEvent.Use();
                }

                return;
            }

            if (inspectedComponent != null)
            {
                HandleComponentPropertyInput(currentEvent);
                return;
            }

            if (isAddingComponent)
            {
                HandleComponentListInput(currentEvent);
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
                ActivateSelectedItem();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Backspace)
            {
                RemoveSelectedComponent();
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

        private void HandleComponentPropertyInput(Event currentEvent)
        {
            if (isChoosingOption)
            {
                if (currentEvent.keyCode == KeyCode.UpArrow)
                {
                    MoveOptionSelection(-1);
                }
                else if (currentEvent.keyCode == KeyCode.DownArrow)
                {
                    MoveOptionSelection(1);
                }
                else if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
                {
                    CommitOption();
                }
                else if (currentEvent.keyCode == KeyCode.Escape)
                {
                    isChoosingOption = false;
                    SpeakSafely("Option selection cancelled. " + GetSelectedPropertyDescription());
                    Repaint();
                }
                else
                {
                    return;
                }

                currentEvent.Use();
                return;
            }

            if (currentEvent.keyCode == KeyCode.UpArrow)
            {
                MovePropertySelection(-1);
            }
            else if (currentEvent.keyCode == KeyCode.DownArrow)
            {
                MovePropertySelection(1);
            }
            else if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                ActivateSelectedProperty();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                string componentName = inspectedComponent.GetType().Name;
                inspectedComponent = null;
                componentProperties.Clear();
                selectedIndex = Mathf.Clamp(selectedIndex, 0, Math.Max(0, items.Count - 1));
                SpeakSafely(componentName + " closed. " + GetSelectedItemDescription() + ".");
                Repaint();
            }
            else
            {
                return;
            }

            currentEvent.Use();
        }

        private void HandleComponentListInput(Event currentEvent)
        {
            if (currentEvent.keyCode == KeyCode.UpArrow)
            {
                MoveComponentTypeSelection(-1);
            }
            else if (currentEvent.keyCode == KeyCode.DownArrow)
            {
                MoveComponentTypeSelection(1);
            }
            else if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                AddSelectedComponent();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                isAddingComponent = false;
                SpeakSafely("Add component cancelled. " + GetSelectedItemDescription());
                Repaint();
            }
            else
            {
                return;
            }

            currentEvent.Use();
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

        private void ActivateSelectedItem()
        {
            if (items.Count == 0 || selectedIndex < 0 || selectedIndex >= items.Count)
            {
                return;
            }

            InspectorItem item = items[selectedIndex];
            if (item.Kind == InspectorItemKind.AddComponent)
            {
                OpenComponentList();
                return;
            }

            if (item.Kind == InspectorItemKind.Component)
            {
                OpenComponentView(item.Component);
                return;
            }

            if (!item.IsEditable)
            {
                SpeakSafely(item.Label + ", read only.");
                return;
            }

            editValue = item.Value;
            isEditing = true;
            string inputDescription = item.Kind == InspectorItemKind.Name ? "Type a name" : "Type a number";
            SpeakSafely("Editing " + item.Label + ". Current value " + item.Value + ". " + inputDescription + " and press Enter. Escape cancels.");
            Repaint();
        }

        private void CommitEdit()
        {
            if (inspectedComponent != null)
            {
                CommitComponentPropertyEdit();
                return;
            }

            InspectorItem item = items[selectedIndex];
            if (item.Kind == InspectorItemKind.Name)
            {
                CommitNameEdit(item);
                return;
            }

            float parsedValue;
            if (!float.TryParse(editValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue) ||
                float.IsNaN(parsedValue) || float.IsInfinity(parsedValue))
            {
                SpeakSafely("Invalid number. Use digits, a minus sign, and a decimal point.");
                return;
            }

            Undo.RecordObject(inspectedObject.transform, "Unity Access edit " + item.Label);
            item.SetValue(parsedValue);
            EditorUtility.SetDirty(inspectedObject.transform);
            isEditing = false;
            RefreshItems();
            SpeakSafely(item.Label + " changed to " + FormatFloat(parsedValue) + ".");
            Repaint();
        }

        private void CommitNameEdit(InspectorItem item)
        {
            string newName = editValue.Trim();
            if (newName.Length == 0)
            {
                SpeakSafely("The object name cannot be empty.");
                return;
            }

            Undo.RecordObject(inspectedObject, "Unity Access rename object");
            inspectedObject.name = newName;
            EditorUtility.SetDirty(inspectedObject);
            isEditing = false;
            RefreshItems();
            SpeakSafely(item.Label + " changed to " + newName + ".");
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
            isAddingComponent = false;
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
            items.Add(new InspectorItem("Name", inspectedObject.name, InspectorItemKind.Name, null, null));
            AddTransformItem("X position", () => objectTransform.localPosition.x, value => SetPositionAxis(0, value));
            AddTransformItem("Y position", () => objectTransform.localPosition.y, value => SetPositionAxis(1, value));
            AddTransformItem("Z position", () => objectTransform.localPosition.z, value => SetPositionAxis(2, value));
            AddTransformItem("X rotation", () => objectTransform.localEulerAngles.x, value => SetRotationAxis(0, value));
            AddTransformItem("Y rotation", () => objectTransform.localEulerAngles.y, value => SetRotationAxis(1, value));
            AddTransformItem("Z rotation", () => objectTransform.localEulerAngles.z, value => SetRotationAxis(2, value));
            AddTransformItem("X scale", () => objectTransform.localScale.x, value => SetScaleAxis(0, value));
            AddTransformItem("Y scale", () => objectTransform.localScale.y, value => SetScaleAxis(1, value));
            AddTransformItem("Z scale", () => objectTransform.localScale.z, value => SetScaleAxis(2, value));

            Component[] components = inspectedObject.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                string componentName = component == null ? "Missing script" : component.GetType().Name;
                items.Add(new InspectorItem("Component " + (index + 1), componentName, InspectorItemKind.Component, null, component));
            }

            items.Add(new InspectorItem("Add component", "Press Enter", InspectorItemKind.AddComponent, null, null));

            selectedIndex = Mathf.Clamp(selectedIndex, 0, Math.Max(0, items.Count - 1));
        }

        private void AddTransformItem(string label, Func<float> getter, Action<float> setter)
        {
            items.Add(new InspectorItem(label, FormatFloat(getter()), InspectorItemKind.Number, setter, null));
        }

        private void OpenComponentView(Component component)
        {
            if (component == null)
            {
                SpeakSafely("The missing script has no properties to inspect.");
                return;
            }

            inspectedComponent = component;
            selectedPropertyIndex = 0;
            isEditing = false;
            isChoosingOption = false;
            RefreshComponentProperties();
            SpeakSafely(component.GetType().Name + " component. " + componentProperties.Count +
                " properties. " + GetSelectedPropertyDescription() + ".");
            Repaint();
        }

        private void RefreshComponentProperties()
        {
            componentProperties.Clear();
            if (inspectedComponent == null)
            {
                return;
            }

            Transform componentTransform = inspectedComponent as Transform;
            if (componentTransform != null)
            {
                AddTransformComponentProperties(componentTransform);
                selectedPropertyIndex = Mathf.Clamp(selectedPropertyIndex, 0, componentProperties.Count - 1);
                return;
            }

            SerializedObject serializedObject = new SerializedObject(inspectedComponent);
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool visitChildren = true;
            while (iterator.NextVisible(visitChildren))
            {
                visitChildren = false;
                if (iterator.propertyType == SerializedPropertyType.Vector2)
                {
                    componentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 0));
                    componentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 1));
                }
                else if (iterator.propertyType == SerializedPropertyType.Vector3)
                {
                    componentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 0));
                    componentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 1));
                    componentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 2));
                }
                else
                {
                    componentProperties.Add(ComponentPropertyItem.FromProperty(iterator));
                }
            }

            selectedPropertyIndex = Mathf.Clamp(selectedPropertyIndex, 0, Math.Max(0, componentProperties.Count - 1));
        }

        private void AddTransformComponentProperties(Transform componentTransform)
        {
            Vector3 position = componentTransform.localPosition;
            Vector3 rotation = componentTransform.localEulerAngles;
            Vector3 scale = componentTransform.localScale;
            componentProperties.Add(ComponentPropertyItem.FromTransformAxis("X position", "position", 0, position.x));
            componentProperties.Add(ComponentPropertyItem.FromTransformAxis("Y position", "position", 1, position.y));
            componentProperties.Add(ComponentPropertyItem.FromTransformAxis("Z position", "position", 2, position.z));
            componentProperties.Add(ComponentPropertyItem.FromTransformAxis("X rotation", "rotation", 0, rotation.x));
            componentProperties.Add(ComponentPropertyItem.FromTransformAxis("Y rotation", "rotation", 1, rotation.y));
            componentProperties.Add(ComponentPropertyItem.FromTransformAxis("Z rotation", "rotation", 2, rotation.z));
            componentProperties.Add(ComponentPropertyItem.FromTransformAxis("X scale", "scale", 0, scale.x));
            componentProperties.Add(ComponentPropertyItem.FromTransformAxis("Y scale", "scale", 1, scale.y));
            componentProperties.Add(ComponentPropertyItem.FromTransformAxis("Z scale", "scale", 2, scale.z));
        }

        private void MovePropertySelection(int direction)
        {
            if (componentProperties.Count == 0)
            {
                SpeakSafely("This component has no visible serialized properties.");
                return;
            }

            selectedPropertyIndex = Mathf.Clamp(selectedPropertyIndex + direction, 0, componentProperties.Count - 1);
            SpeakSafely(GetSelectedPropertyDescription() + ", " + (selectedPropertyIndex + 1) +
                " of " + componentProperties.Count + ".");
            Repaint();
        }

        private void ActivateSelectedProperty()
        {
            if (componentProperties.Count == 0)
            {
                SpeakSafely("This component has no visible serialized properties.");
                return;
            }

            ComponentPropertyItem item = componentProperties[selectedPropertyIndex];
            if (item.PropertyType == SerializedPropertyType.Boolean && item.IsEditable)
            {
                ToggleBooleanProperty(item);
                return;
            }

            if (item.PropertyType == SerializedPropertyType.Enum && item.IsEditable && item.Options.Length > 0)
            {
                selectedOptionIndex = Mathf.Clamp(item.OptionIndex, 0, item.Options.Length - 1);
                isChoosingOption = true;
                SpeakSafely("Choose " + item.Label + ". " + item.Options[selectedOptionIndex] + ", " +
                    (selectedOptionIndex + 1) + " of " + item.Options.Length + ".");
                Repaint();
                return;
            }

            if ((item.PropertyType == SerializedPropertyType.Integer ||
                item.PropertyType == SerializedPropertyType.Float ||
                item.PropertyType == SerializedPropertyType.Vector2 ||
                item.PropertyType == SerializedPropertyType.Vector3) && item.IsEditable)
            {
                editValue = item.Value;
                isEditing = true;
                SpeakSafely("Editing " + item.Label + ". Current value " + item.Value +
                    ". Type a number and press Enter. Escape cancels.");
                Repaint();
                return;
            }

            if (item.PropertyType == SerializedPropertyType.String && item.IsEditable)
            {
                editValue = item.Value;
                isEditing = true;
                SpeakSafely("Editing " + item.Label + ". Type text and press Enter. Escape cancels.");
                Repaint();
                return;
            }

            if (item.PropertyType == SerializedPropertyType.ObjectReference && item.IsEditable)
            {
                OpenObjectReferenceSelector(item);
                return;
            }

            SpeakSafely(item.Label + ", read only.");
        }

        private void CommitComponentPropertyEdit()
        {
            ComponentPropertyItem item = componentProperties[selectedPropertyIndex];
            if (inspectedComponent is Transform && item.IsTransformAxis)
            {
                CommitTransformComponentEdit(item);
                return;
            }

            SerializedObject serializedObject = new SerializedObject(inspectedComponent);
            SerializedProperty property = serializedObject.FindProperty(item.PropertyPath);
            if (property == null)
            {
                HandleMissingProperty();
                return;
            }

            serializedObject.Update();
            if (property.propertyType == SerializedPropertyType.String)
            {
                Undo.RecordObject(inspectedComponent, "Unity Access edit " + item.Label);
                property.stringValue = editValue;
            }
            else if (item.VectorAxis >= 0)
            {
                float axisValue;
                if (!float.TryParse(editValue, NumberStyles.Float, CultureInfo.InvariantCulture, out axisValue) ||
                    float.IsNaN(axisValue) || float.IsInfinity(axisValue))
                {
                    SpeakSafely("Invalid number. Use digits, a minus sign, and a decimal point.");
                    return;
                }

                Undo.RecordObject(inspectedComponent, "Unity Access edit " + item.Label);
                if (property.propertyType == SerializedPropertyType.Vector2)
                {
                    Vector2 value = property.vector2Value;
                    value[item.VectorAxis] = axisValue;
                    property.vector2Value = value;
                }
                else
                {
                    Vector3 value = property.vector3Value;
                    value[item.VectorAxis] = axisValue;
                    property.vector3Value = value;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                long integerValue;
                if (!long.TryParse(editValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue))
                {
                    SpeakSafely("Invalid whole number.");
                    return;
                }

                Undo.RecordObject(inspectedComponent, "Unity Access edit " + item.Label);
                property.longValue = integerValue;
            }
            else
            {
                double realValue;
                if (!double.TryParse(editValue, NumberStyles.Float, CultureInfo.InvariantCulture, out realValue) ||
                    double.IsNaN(realValue) || double.IsInfinity(realValue))
                {
                    SpeakSafely("Invalid number. Use digits, a minus sign, and a decimal point.");
                    return;
                }

                Undo.RecordObject(inspectedComponent, "Unity Access edit " + item.Label);
                property.doubleValue = realValue;
            }

            ApplyComponentPropertyChange(serializedObject, item.Label);
        }

        private void OpenObjectReferenceSelector(ComponentPropertyItem item)
        {
            SerializedObject serializedObject = new SerializedObject(inspectedComponent);
            SerializedProperty property = serializedObject.FindProperty(item.PropertyPath);
            if (property == null)
            {
                HandleMissingProperty();
                return;
            }

            UnityEngine.Object currentValue = property.objectReferenceValue;
            Type requiredType = currentValue == null
                ? ResolveObjectReferenceType(property.type)
                : currentValue.GetType();
            ObjectSelector.Open(requiredType, selectedObject => ApplyObjectReference(item.PropertyPath, item.Label, selectedObject), currentValue);
        }

        private static Type ResolveObjectReferenceType(string serializedType)
        {
            const string Prefix = "PPtr<$";
            if (serializedType.StartsWith(Prefix, StringComparison.Ordinal) && serializedType.EndsWith(">", StringComparison.Ordinal))
            {
                string typeName = serializedType.Substring(Prefix.Length, serializedType.Length - Prefix.Length - 1);
                foreach (Type candidate in TypeCache.GetTypesDerivedFrom<UnityEngine.Object>())
                {
                    if (candidate.Name == typeName)
                    {
                        return candidate;
                    }
                }
            }

            return typeof(UnityEngine.Object);
        }

        private void ApplyObjectReference(string propertyPath, string label, UnityEngine.Object selectedObject)
        {
            if (inspectedComponent == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(inspectedComponent);
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                HandleMissingProperty();
                return;
            }

            serializedObject.Update();
            Undo.RecordObject(inspectedComponent, "Unity Access choose " + label);
            property.objectReferenceValue = selectedObject;
            ApplyComponentPropertyChange(serializedObject, label);
        }

        private void CommitTransformComponentEdit(ComponentPropertyItem item)
        {
            float parsedValue;
            if (!float.TryParse(editValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue) ||
                float.IsNaN(parsedValue) || float.IsInfinity(parsedValue))
            {
                SpeakSafely("Invalid number. Use digits, a minus sign, and a decimal point.");
                return;
            }

            Transform componentTransform = (Transform)inspectedComponent;
            Undo.RecordObject(componentTransform, "Unity Access edit " + item.Label);
            Vector3 value;
            if (item.TransformGroup == "position")
            {
                value = componentTransform.localPosition;
                value[item.TransformAxis] = parsedValue;
                componentTransform.localPosition = value;
            }
            else if (item.TransformGroup == "rotation")
            {
                value = componentTransform.localEulerAngles;
                value[item.TransformAxis] = parsedValue;
                componentTransform.localEulerAngles = value;
            }
            else
            {
                value = componentTransform.localScale;
                value[item.TransformAxis] = parsedValue;
                componentTransform.localScale = value;
            }

            EditorUtility.SetDirty(componentTransform);
            isEditing = false;
            RefreshComponentProperties();
            SpeakSafely(item.Label + " changed to " + FormatFloat(parsedValue) + ".");
            Repaint();
        }

        private void ToggleBooleanProperty(ComponentPropertyItem item)
        {
            SerializedObject serializedObject = new SerializedObject(inspectedComponent);
            SerializedProperty property = serializedObject.FindProperty(item.PropertyPath);
            if (property == null)
            {
                HandleMissingProperty();
                return;
            }

            serializedObject.Update();
            Undo.RecordObject(inspectedComponent, "Unity Access toggle " + item.Label);
            property.boolValue = !property.boolValue;
            ApplyComponentPropertyChange(serializedObject, item.Label);
        }

        private void MoveOptionSelection(int direction)
        {
            ComponentPropertyItem item = componentProperties[selectedPropertyIndex];
            selectedOptionIndex = Mathf.Clamp(selectedOptionIndex + direction, 0, item.Options.Length - 1);
            SpeakSafely(item.Options[selectedOptionIndex] + ", " + (selectedOptionIndex + 1) +
                " of " + item.Options.Length + ".");
            Repaint();
        }

        private void CommitOption()
        {
            ComponentPropertyItem item = componentProperties[selectedPropertyIndex];
            SerializedObject serializedObject = new SerializedObject(inspectedComponent);
            SerializedProperty property = serializedObject.FindProperty(item.PropertyPath);
            if (property == null)
            {
                HandleMissingProperty();
                return;
            }

            serializedObject.Update();
            Undo.RecordObject(inspectedComponent, "Unity Access choose " + item.Label);
            property.enumValueIndex = selectedOptionIndex;
            ApplyComponentPropertyChange(serializedObject, item.Label);
        }

        private void ApplyComponentPropertyChange(SerializedObject serializedObject, string label)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(inspectedComponent);
            isEditing = false;
            isChoosingOption = false;
            RefreshComponentProperties();
            SpeakSafely(label + " changed to " + componentProperties[selectedPropertyIndex].Value + ".");
            Repaint();
        }

        private void HandleMissingProperty()
        {
            isEditing = false;
            isChoosingOption = false;
            RefreshComponentProperties();
            SpeakSafely("That property is no longer available. The component view was refreshed.");
            Repaint();
        }

        private string GetSelectedPropertyDescription()
        {
            if (componentProperties.Count == 0)
            {
                return "No visible serialized properties";
            }

            ComponentPropertyItem item = componentProperties[selectedPropertyIndex];
            return item.Label + ", " + item.Value + (item.IsEditable ? ", editable" : ", read only");
        }

        private void RemoveSelectedComponent()
        {
            if (items.Count == 0 || selectedIndex < 0 || selectedIndex >= items.Count)
            {
                return;
            }

            InspectorItem item = items[selectedIndex];
            if (item.Kind != InspectorItemKind.Component)
            {
                SpeakSafely(item.Label + " is not a removable component.");
                return;
            }

            if (item.Component == null)
            {
                SpeakSafely("Missing scripts cannot be removed individually from this view.");
                return;
            }

            if (item.Component is Transform)
            {
                SpeakSafely("The Transform component is required and cannot be removed.");
                return;
            }

            string componentName = item.Component.GetType().Name;
            try
            {
                Undo.DestroyObjectImmediate(item.Component);
                RefreshItems();
                SpeakSafely(componentName + " removed. " + GetSelectedItemDescription() + ".");
                Repaint();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(InspectorAccessibilityWindow), exception);
                SpeakSafely("Unity could not remove " + componentName + ". See debug.txt for details.");
            }
        }

        private void OpenComponentList()
        {
            componentTypes.Clear();
            componentTypes.AddRange(TypeCache.GetTypesDerivedFrom<Component>()
                .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition && type != typeof(Transform))
                .OrderBy(GetComponentDisplayName));
            selectedComponentTypeIndex = 0;
            isAddingComponent = true;
            SpeakSafely(componentTypes.Count == 0
                ? "No components are available to add."
                : "Add component list. " + componentTypes.Count + " components. " + GetComponentDisplayName(componentTypes[0]) + ", 1 of " + componentTypes.Count + ".");
            Repaint();
        }

        private void MoveComponentTypeSelection(int direction)
        {
            if (componentTypes.Count == 0)
            {
                SpeakSafely("No components are available to add.");
                return;
            }

            selectedComponentTypeIndex = Mathf.Clamp(selectedComponentTypeIndex + direction, 0, componentTypes.Count - 1);
            SpeakSafely(GetComponentDisplayName(componentTypes[selectedComponentTypeIndex]) + ", " + (selectedComponentTypeIndex + 1) + " of " + componentTypes.Count + ".");
            Repaint();
        }

        private void AddSelectedComponent()
        {
            if (componentTypes.Count == 0)
            {
                SpeakSafely("No components are available to add.");
                return;
            }

            Type componentType = componentTypes[selectedComponentTypeIndex];
            try
            {
                Undo.AddComponent(inspectedObject, componentType);
                isAddingComponent = false;
                RefreshItems();
                selectedIndex = items.Count - 2;
                SpeakSafely(GetComponentDisplayName(componentType) + " added. " + GetSelectedItemDescription() + ".");
                Repaint();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(InspectorAccessibilityWindow), exception);
                SpeakSafely("Unity could not add " + GetComponentDisplayName(componentType) + ". See debug.txt for details.");
            }
        }

        private static string GetComponentDisplayName(Type componentType)
        {
            AddComponentMenu menu = (AddComponentMenu)Attribute.GetCustomAttribute(componentType, typeof(AddComponentMenu));
            return menu != null && !string.IsNullOrWhiteSpace(menu.componentMenu) ? menu.componentMenu : componentType.Name;
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

        private void SetScaleAxis(int axis, float value)
        {
            Vector3 scale = inspectedObject.transform.localScale;
            scale[axis] = value;
            inspectedObject.transform.localScale = scale;
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

        private sealed class ComponentPropertyItem
        {
            private ComponentPropertyItem(
                string label,
                string value,
                string propertyPath,
                SerializedPropertyType propertyType,
                bool isEditable,
                string[] options,
                int optionIndex,
                string transformGroup,
                int transformAxis,
                int vectorAxis = -1)
            {
                Label = label;
                Value = value;
                PropertyPath = propertyPath;
                PropertyType = propertyType;
                IsEditable = isEditable;
                Options = options;
                OptionIndex = optionIndex;
                TransformGroup = transformGroup;
                TransformAxis = transformAxis;
                VectorAxis = vectorAxis;
            }

            internal string Label { get; private set; }

            internal string Value { get; private set; }

            internal string PropertyPath { get; private set; }

            internal SerializedPropertyType PropertyType { get; private set; }

            internal bool IsEditable { get; private set; }

            internal string[] Options { get; private set; }

            internal int OptionIndex { get; private set; }

            internal string TransformGroup { get; private set; }

            internal int TransformAxis { get; private set; }

            internal int VectorAxis { get; private set; }

            internal bool IsTransformAxis
            {
                get { return !string.IsNullOrEmpty(TransformGroup); }
            }

            internal static ComponentPropertyItem FromTransformAxis(
                string label,
                string transformGroup,
                int transformAxis,
                float value)
            {
                return new ComponentPropertyItem(
                    label,
                    FormatFloat(value),
                    string.Empty,
                    SerializedPropertyType.Float,
                    true,
                    Array.Empty<string>(),
                    -1,
                    transformGroup,
                    transformAxis);
            }

            internal static ComponentPropertyItem FromProperty(SerializedProperty property)
            {
                string[] options = property.propertyType == SerializedPropertyType.Enum
                    ? property.enumDisplayNames
                    : Array.Empty<string>();
                bool supportedType = property.propertyType == SerializedPropertyType.Integer ||
                    property.propertyType == SerializedPropertyType.Float ||
                    property.propertyType == SerializedPropertyType.String ||
                    property.propertyType == SerializedPropertyType.Boolean ||
                    property.propertyType == SerializedPropertyType.Enum ||
                    property.propertyType == SerializedPropertyType.ObjectReference;

                return new ComponentPropertyItem(
                    property.displayName,
                    GetPropertyValue(property),
                    property.propertyPath,
                    property.propertyType,
                    property.editable && supportedType,
                    options,
                    property.propertyType == SerializedPropertyType.Enum ? property.enumValueIndex : -1,
                    string.Empty,
                    -1);
            }

            internal static ComponentPropertyItem FromVectorProperty(SerializedProperty property, int axis)
            {
                string axisName = axis == 0 ? "X" : axis == 1 ? "Y" : "Z";
                float value = property.propertyType == SerializedPropertyType.Vector2
                    ? property.vector2Value[axis]
                    : property.vector3Value[axis];
                return new ComponentPropertyItem(
                    property.displayName + " " + axisName,
                    FormatFloat(value),
                    property.propertyPath,
                    property.propertyType,
                    property.editable,
                    Array.Empty<string>(),
                    -1,
                    string.Empty,
                    -1,
                    axis);
            }

            private static string GetPropertyValue(SerializedProperty property)
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        return property.longValue.ToString(CultureInfo.InvariantCulture);
                    case SerializedPropertyType.Boolean:
                        return property.boolValue ? "On" : "Off";
                    case SerializedPropertyType.Float:
                        return property.doubleValue.ToString("0.###", CultureInfo.InvariantCulture);
                    case SerializedPropertyType.String:
                        return property.stringValue;
                    case SerializedPropertyType.Enum:
                        return property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length
                            ? property.enumDisplayNames[property.enumValueIndex]
                            : "Unknown option";
                    case SerializedPropertyType.ObjectReference:
                        return property.objectReferenceValue == null ? "None" : property.objectReferenceValue.name;
                    case SerializedPropertyType.Vector2:
                        return property.vector2Value.ToString();
                    case SerializedPropertyType.Vector3:
                        return property.vector3Value.ToString();
                    case SerializedPropertyType.Color:
                        return property.colorValue.ToString();
                    default:
                        return property.type;
                }
            }
        }

        private sealed class InspectorItem
        {
            private readonly Action<float> setter;

            internal InspectorItem(string label, string value, InspectorItemKind kind, Action<float> setter, Component component)
            {
                Label = label;
                Value = value;
                Kind = kind;
                this.setter = setter;
                Component = component;
            }

            internal string Label { get; private set; }

            internal string Value { get; private set; }

            internal InspectorItemKind Kind { get; private set; }

            internal Component Component { get; private set; }

            internal bool IsEditable
            {
                get { return Kind == InspectorItemKind.Name || setter != null; }
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

        private enum InspectorItemKind
        {
            Name,
            Number,
            Component,
            AddComponent
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
