using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
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
        private const string ComponentSearchControlName = "AccessibleInspectorComponentSearch";
        private readonly List<InspectorItem> items = new List<InspectorItem>();
        private GameObject inspectedObject;
        private Vector2 scrollPosition;
        private int selectedIndex;
        private readonly AccessibleTextEdit textEdit = new AccessibleTextEdit();
        private readonly List<Type> componentTypes = new List<Type>();
        private bool isAddingComponent;
        private int selectedComponentTypeIndex;
        private Component inspectedComponent;
        private readonly List<ComponentPropertyItem> allComponentProperties = new List<ComponentPropertyItem>();
        private readonly List<ComponentPropertyItem> componentProperties = new List<ComponentPropertyItem>();
        private string componentSearchText = string.Empty;
        private string appliedComponentSearchText = string.Empty;
        private bool focusComponentSearch;
        private int selectedPropertyIndex;
        private bool isChoosingOption;
        private int selectedOptionIndex;
        private readonly List<string> layerNames = new List<string>();
        private bool isChoosingLayer;
        private int selectedLayerIndex;

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
            EditorGUILayout.LabelField(isChoosingLayer
                ? "Choose a layer with Up and Down. Enter assigns it. Escape cancels."
                : isAddingComponent
                ? "Choose a component with Up and Down. Enter adds it. Escape cancels."
                : inspectedComponent != null
                    ? "Type to search. Up and Down navigate properties. Enter edits or activates. Escape returns to the inspector."
                    : "Up and Down navigate. Enter edits or activates. Backspace removes a component. Escape returns to the hierarchy.");
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (isChoosingLayer)
            {
                DrawLayerList();
                EditorGUILayout.EndScrollView();
                return;
            }

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

            // Object properties are intentionally presented before component navigation.
            EditorGUILayout.LabelField("Object properties", EditorStyles.boldLabel);
            bool componentHeadingDrawn = false;
            for (int index = 0; index < items.Count; index++)
            {
                InspectorItem item = items[index];
                if (!componentHeadingDrawn &&
                    (item.Kind == InspectorItemKind.Component || item.Kind == InspectorItemKind.AddComponent))
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
                    componentHeadingDrawn = true;
                }

                Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                if (index == selectedIndex)
                {
                    AccessibleEditorStyles.DrawSelection(row, true);
                }

                if (textEdit.IsEditing && index == selectedIndex && item.IsEditable)
                {
                    textEdit.Value = AccessibleControls.TextBox(row, EditControlName, item.Label, textEdit.Value, true);
                }
                else
                {
                    string displayText = item.Label + ": " + item.Value;
                    bool isButton = item.Kind == InspectorItemKind.Component ||
                        item.Kind == InspectorItemKind.AddComponent ||
                        item.Kind == InspectorItemKind.Layer;
                    if (isButton && AccessibleControls.Button(row, displayText, index == selectedIndex))
                    {
                        selectedIndex = index;
                        ActivateSelectedItem();
                    }
                    else if (!isButton)
                    {
                        EditorGUI.LabelField(row, displayText);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawComponentProperties()
        {
            bool searchEnabled = !textEdit.IsEditing && !isChoosingOption;
            EditorGUI.BeginDisabledGroup(!searchEnabled);
            string updatedSearchText = AccessibleControls.ToolbarSearch(
                ComponentSearchControlName,
                "Search properties",
                componentSearchText,
                focusComponentSearch && searchEnabled);
            EditorGUI.EndDisabledGroup();
            focusComponentSearch = false;
            if (searchEnabled && !string.Equals(updatedSearchText, componentSearchText, StringComparison.Ordinal))
            {
                componentSearchText = updatedSearchText;
                ApplyComponentPropertySearch(false, true);
            }

            if (componentProperties.Count == 0)
            {
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(componentSearchText)
                    ? "No editable properties."
                    : "No matching properties.");
                return;
            }

            for (int index = 0; index < componentProperties.Count; index++)
            {
                Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                if (index == selectedPropertyIndex)
                {
                    AccessibleEditorStyles.DrawSelection(row, true);
                }

                ComponentPropertyItem item = componentProperties[index];
                if (textEdit.IsEditing && index == selectedPropertyIndex)
                {
                    textEdit.Value = AccessibleControls.TextBox(row, EditControlName, item.Label, textEdit.Value, true);
                }
                else
                {
                    string value = isChoosingOption && index == selectedPropertyIndex
                        ? item.Options[selectedOptionIndex]
                        : item.Value;
                    string displayText = GetPropertyControlText(item, value);
                    bool isInteractiveControl = item.IsEditable &&
                        (item.PropertyType == SerializedPropertyType.Boolean ||
                        item.PropertyType == SerializedPropertyType.Enum ||
                        item.PropertyType == SerializedPropertyType.ObjectReference);
                    if (isInteractiveControl && AccessibleControls.Button(row, displayText, index == selectedPropertyIndex))
                    {
                        selectedPropertyIndex = index;
                        ActivateSelectedProperty();
                    }
                    else if (!isInteractiveControl)
                    {
                        EditorGUI.LabelField(row, displayText);
                    }
                }
            }
        }

        private static string GetPropertyControlText(ComponentPropertyItem item, string value)
        {
            if (item.PropertyType == SerializedPropertyType.Boolean)
            {
                return item.Label + ": " + value + ", check box";
            }

            if (item.PropertyType == SerializedPropertyType.Enum)
            {
                return item.Label + ": " + value + ", combo box";
            }

            if (item.PropertyType == SerializedPropertyType.ObjectReference)
            {
                return item.Label + ": " + value + ", object selector";
            }

            return item.Label + ": " + value;
        }

        private void DrawComponentList()
        {
            for (int index = 0; index < componentTypes.Count; index++)
            {
                Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                if (index == selectedComponentTypeIndex)
                {
                    AccessibleEditorStyles.DrawSelection(row, true);
                }

                EditorGUI.LabelField(row, GetComponentDisplayName(componentTypes[index]));
            }
        }

        /// <summary>Draws the project-defined layers as an accessible selectable list.</summary>
        private void DrawLayerList()
        {
            for (int index = 0; index < layerNames.Count; index++)
            {
                string layerName = layerNames[index];
                if (AccessibleControls.Button(layerName, index == selectedLayerIndex))
                {
                    selectedLayerIndex = index;
                    CommitLayerSelection();
                }
            }
        }

        private void HandleKeyboardInput(Event currentEvent)
        {
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (textEdit.IsEditing)
            {
                if (AccessibleKeyboard.IsConfirm(currentEvent))
                {
                    CommitEdit();
                    currentEvent.Use();
                }
                else if (AccessibleKeyboard.IsCancel(currentEvent))
                {
                    textEdit.End();
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

            if (isChoosingLayer)
            {
                HandleLayerListInput(currentEvent);
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

        /// <summary>Handles keyboard navigation and activation for the accessible layer list.</summary>
        private void HandleLayerListInput(Event currentEvent)
        {
            if (currentEvent.keyCode == KeyCode.UpArrow)
            {
                MoveLayerSelection(-1);
            }
            else if (currentEvent.keyCode == KeyCode.DownArrow)
            {
                MoveLayerSelection(1);
            }
            else if (AccessibleKeyboard.IsConfirm(currentEvent))
            {
                CommitLayerSelection();
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent))
            {
                isChoosingLayer = false;
                SpeakSafely("Layer selection cancelled. " + GetSelectedItemDescription() + ".");
                Repaint();
            }
            else
            {
                return;
            }

            currentEvent.Use();
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

            if (TryHandleComponentSearchInput(currentEvent))
            {
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

        private bool TryHandleComponentSearchInput(Event currentEvent)
        {
            string updatedSearchText = componentSearchText;
            if (currentEvent.keyCode == KeyCode.Backspace)
            {
                if (updatedSearchText.Length == 0)
                {
                    return true;
                }

                updatedSearchText = updatedSearchText.Substring(0, updatedSearchText.Length - 1);
            }
            else if ((currentEvent.control || currentEvent.command) && currentEvent.keyCode == KeyCode.V)
            {
                updatedSearchText += EditorGUIUtility.systemCopyBuffer ?? string.Empty;
            }
            else if (currentEvent.character != '\0' && !char.IsControl(currentEvent.character))
            {
                updatedSearchText += currentEvent.character;
            }
            else
            {
                return false;
            }

            componentSearchText = updatedSearchText;
            focusComponentSearch = true;
            ApplyComponentPropertySearch(false, true);
            return true;
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

            selectedIndex = AccessibleList.Move(selectedIndex, direction, items.Count);
            SpeakSafely(GetSelectedItemDescription() + ", " + AccessibleList.Position(selectedIndex, items.Count) + ".");
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

            if (item.Kind == InspectorItemKind.Layer)
            {
                OpenLayerList();
                return;
            }

            if (!item.IsEditable)
            {
                SpeakSafely(item.Label + ", read only.");
                return;
            }

            textEdit.Begin(item.Value);
            string inputDescription = item.Kind == InspectorItemKind.Name
                ? "Type a name"
                : item.Kind == InspectorItemKind.Tag
                    ? "Type an existing tag name"
                    : "Type a number";
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

            if (item.Kind == InspectorItemKind.Tag)
            {
                CommitTagEdit(item);
                return;
            }

            float parsedValue;
            if (!float.TryParse(textEdit.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue) ||
                float.IsNaN(parsedValue) || float.IsInfinity(parsedValue))
            {
                SpeakSafely("Invalid number. Use digits, a minus sign, and a decimal point.");
                return;
            }

            Undo.RecordObject(inspectedObject.transform, "Unity Access edit " + item.Label);
            item.SetValue(parsedValue);
            EditorUtility.SetDirty(inspectedObject.transform);
            textEdit.End();
            RefreshItems();
            SpeakSafely(item.Label + " changed to " + FormatFloat(parsedValue) + ".");
            Repaint();
        }

        private void CommitNameEdit(InspectorItem item)
        {
            string newName = textEdit.Value.Trim();
            if (newName.Length == 0)
            {
                SpeakSafely("The object name cannot be empty.");
                return;
            }

            Undo.RecordObject(inspectedObject, "Unity Access rename object");
            inspectedObject.name = newName;
            EditorUtility.SetDirty(inspectedObject);
            textEdit.End();
            RefreshItems();
            SpeakSafely(item.Label + " changed to " + newName + ".");
            Repaint();
        }

        /// <summary>Assigns an existing project tag entered through the accessible text box.</summary>
        private void CommitTagEdit(InspectorItem item)
        {
            string newTag = textEdit.Value.Trim();
            if (newTag.Length == 0)
            {
                SpeakSafely("The tag name cannot be empty.");
                return;
            }

            if (!InternalEditorUtility.tags.Contains(newTag))
            {
                SpeakSafely("Tag " + newTag + " does not exist. Enter an existing project tag.");
                return;
            }

            Undo.RecordObject(inspectedObject, "Unity Access assign tag");
            inspectedObject.tag = newTag;
            EditorUtility.SetDirty(inspectedObject);
            textEdit.End();
            RefreshItems();
            SpeakSafely(item.Label + " changed to " + newTag + ".");
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
            textEdit.End();
            isAddingComponent = false;
            isChoosingLayer = false;
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
            items.Add(new InspectorItem("Tag", inspectedObject.tag, InspectorItemKind.Tag, null, null));
            items.Add(new InspectorItem("Layer", GetLayerDisplayName(inspectedObject.layer), InspectorItemKind.Layer, null, null));
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

        /// <summary>Opens the accessible list of project-defined layers.</summary>
        private void OpenLayerList()
        {
            layerNames.Clear();
            layerNames.AddRange(InternalEditorUtility.layers);
            if (layerNames.Count == 0)
            {
                SpeakSafely("No project layers are available.");
                return;
            }

            string currentLayerName = LayerMask.LayerToName(inspectedObject.layer);
            int currentIndex = layerNames.IndexOf(currentLayerName);
            selectedLayerIndex = currentIndex >= 0 ? currentIndex : 0;
            isChoosingLayer = true;
            SpeakSafely("Layer list. " + layerNames.Count + " layers. " + layerNames[selectedLayerIndex] + ", " +
                AccessibleList.Position(selectedLayerIndex, layerNames.Count) + ".");
            Repaint();
        }

        /// <summary>Moves the selected layer and announces its list position through NVDA.</summary>
        private void MoveLayerSelection(int direction)
        {
            selectedLayerIndex = AccessibleList.Move(selectedLayerIndex, direction, layerNames.Count);
            SpeakSafely(layerNames[selectedLayerIndex] + ", " +
                AccessibleList.Position(selectedLayerIndex, layerNames.Count) + ".");
            Repaint();
        }

        /// <summary>Assigns the selected layer to the inspected object with Unity Undo support.</summary>
        private void CommitLayerSelection()
        {
            if (selectedLayerIndex < 0 || selectedLayerIndex >= layerNames.Count)
            {
                return;
            }

            string layerName = layerNames[selectedLayerIndex];
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                SpeakSafely("Layer " + layerName + " is no longer available.");
                isChoosingLayer = false;
                RefreshItems();
                Repaint();
                return;
            }

            Undo.RecordObject(inspectedObject, "Unity Access assign layer");
            inspectedObject.layer = layer;
            EditorUtility.SetDirty(inspectedObject);
            isChoosingLayer = false;
            RefreshItems();
            SpeakSafely("Layer changed to " + layerName + ".");
            Repaint();
        }

        private static string GetLayerDisplayName(int layer)
        {
            string layerName = LayerMask.LayerToName(layer);
            return string.IsNullOrEmpty(layerName) ? "Layer " + layer : layerName;
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
            textEdit.End();
            isChoosingOption = false;
            componentSearchText = string.Empty;
            appliedComponentSearchText = string.Empty;
            focusComponentSearch = true;
            RefreshComponentProperties();
            SpeakSafely(component.GetType().Name + " component. " + componentProperties.Count +
                " properties. Search properties, editable text box, empty. " + GetSelectedPropertyDescription() + ".");
            Repaint();
        }

        private void RefreshComponentProperties()
        {
            allComponentProperties.Clear();
            componentProperties.Clear();
            if (inspectedComponent == null)
            {
                return;
            }

            Transform componentTransform = inspectedComponent as Transform;
            if (componentTransform != null)
            {
                AddTransformComponentProperties(componentTransform);
                HashSet<string> transformMemberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    NormalizeMemberName("localPosition"),
                    NormalizeMemberName("localEulerAngles"),
                    NormalizeMemberName("localScale")
                };
                AddReflectedComponentProperties(transformMemberNames);
                ApplyComponentPropertySearch(true, false);
                return;
            }

            SerializedObject serializedObject = new SerializedObject(inspectedComponent);
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty iterator = serializedObject.GetIterator();
            HashSet<string> serializedMemberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool enterChildren = true;
            // Traverse every serialized property, including values Unity hides from its
            // standard Inspector, so supported references such as sprites are not omitted.
            while (iterator.Next(enterChildren))
            {
                // Public component properties correspond to root serialized members;
                // nested names must not suppress an unrelated reflected property.
                if (iterator.depth == 0)
                {
                    serializedMemberNames.Add(NormalizeMemberName(iterator.name));
                }
                // Descend through serialized containers so fields in nested serializable
                // objects are detected. Supported values are leaves in this view.
                enterChildren = !IsSupportedComponentProperty(iterator) && iterator.hasVisibleChildren;

                // m_Script is Unity metadata rather than an editable object variable.
                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                if (iterator.propertyType == SerializedPropertyType.Vector2 && iterator.editable)
                {
                    allComponentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 0));
                    allComponentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 1));
                }
                else if (iterator.propertyType == SerializedPropertyType.Vector3 && iterator.editable)
                {
                    allComponentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 0));
                    allComponentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 1));
                    allComponentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 2));
                }
                else if (iterator.propertyType == SerializedPropertyType.Color && iterator.editable)
                {
                    allComponentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 0));
                    allComponentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 1));
                    allComponentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 2));
                    allComponentProperties.Add(ComponentPropertyItem.FromVectorProperty(iterator, 3));
                }
                else if (IsSupportedComponentProperty(iterator) && iterator.editable)
                {
                    allComponentProperties.Add(ComponentPropertyItem.FromProperty(iterator));
                }
            }

            AddReflectedComponentProperties(serializedMemberNames);
            ApplyComponentPropertySearch(true, false);
        }

        private void AddReflectedComponentProperties(HashSet<string> existingMemberNames)
        {
            PropertyInfo[] publicProperties = inspectedComponent.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo property in publicProperties.OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                if (!IsEditableReflectedProperty(property) ||
                    existingMemberNames.Contains(NormalizeMemberName(property.Name)))
                {
                    continue;
                }

                try
                {
                    object value = property.GetValue(inspectedComponent, null);
                    if (property.PropertyType == typeof(Vector2))
                    {
                        allComponentProperties.Add(ComponentPropertyItem.FromReflectedProperty(property, value, 0));
                        allComponentProperties.Add(ComponentPropertyItem.FromReflectedProperty(property, value, 1));
                    }
                    else if (property.PropertyType == typeof(Vector3))
                    {
                        allComponentProperties.Add(ComponentPropertyItem.FromReflectedProperty(property, value, 0));
                        allComponentProperties.Add(ComponentPropertyItem.FromReflectedProperty(property, value, 1));
                        allComponentProperties.Add(ComponentPropertyItem.FromReflectedProperty(property, value, 2));
                    }
                    else if (property.PropertyType == typeof(Color))
                    {
                        allComponentProperties.Add(ComponentPropertyItem.FromReflectedProperty(property, value, 0));
                        allComponentProperties.Add(ComponentPropertyItem.FromReflectedProperty(property, value, 1));
                        allComponentProperties.Add(ComponentPropertyItem.FromReflectedProperty(property, value, 2));
                        allComponentProperties.Add(ComponentPropertyItem.FromReflectedProperty(property, value, 3));
                    }
                    else
                    {
                        allComponentProperties.Add(ComponentPropertyItem.FromReflectedProperty(property, value));
                    }
                }
                catch (Exception exception)
                {
                    PluginErrorLog.Write(nameof(InspectorAccessibilityWindow), exception);
                }
            }
        }

        private static bool IsEditableReflectedProperty(PropertyInfo property)
        {
            MethodInfo getter = property.GetGetMethod(false);
            MethodInfo setter = property.GetSetMethod(false);
            Type propertyType = property.PropertyType;
            bool supportedType = propertyType == typeof(bool) ||
                propertyType == typeof(int) ||
                propertyType == typeof(float) ||
                propertyType == typeof(string) ||
                propertyType == typeof(Vector2) ||
                propertyType == typeof(Vector3) ||
                propertyType == typeof(Color) ||
                propertyType.IsEnum ||
                typeof(UnityEngine.Object).IsAssignableFrom(propertyType);
            return getter != null && setter != null &&
                !getter.IsStatic && !setter.IsStatic &&
                property.GetIndexParameters().Length == 0 && supportedType;
        }

        private static string NormalizeMemberName(string memberName)
        {
            string normalizedName = memberName.StartsWith("m_", StringComparison.Ordinal)
                ? memberName.Substring(2)
                : memberName;
            return normalizedName.Replace("_", string.Empty);
        }

        private void ApplyComponentPropertySearch(bool force, bool announce)
        {
            if (!force && string.Equals(componentSearchText, appliedComponentSearchText, StringComparison.Ordinal))
            {
                return;
            }

            appliedComponentSearchText = componentSearchText;
            componentProperties.Clear();
            string query = componentSearchText.Trim();
            if (query.Length == 0)
            {
                componentProperties.AddRange(allComponentProperties);
            }
            else
            {
                componentProperties.AddRange(allComponentProperties
                    .Select((item, index) => new
                    {
                        Item = item,
                        OriginalIndex = index,
                        MatchScore = GetComponentPropertyMatchScore(item.Label, query)
                    })
                    .Where(match => match.MatchScore >= 0)
                    .OrderBy(match => match.MatchScore)
                    .ThenBy(match => match.OriginalIndex)
                    .Select(match => match.Item));
            }

            selectedPropertyIndex = announce
                ? 0
                : Mathf.Clamp(selectedPropertyIndex, 0, Math.Max(0, componentProperties.Count - 1));
            if (!announce)
            {
                return;
            }

            if (componentProperties.Count == 0)
            {
                SpeakSafely("Search properties, " + (query.Length == 0 ? "empty" : query) + ". No results.");
            }
            else
            {
                SpeakSafely("Search properties, " + (query.Length == 0 ? "empty" : query) + ". " +
                    componentProperties.Count + (componentProperties.Count == 1 ? " result. " : " results. ") +
                    GetSelectedPropertyDescription() + ".");
            }

            Repaint();
        }

        private static int GetComponentPropertyMatchScore(string label, string query)
        {
            if (string.Equals(label, query, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (label.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 10;
            }

            int matchIndex = label.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0)
            {
                return -1;
            }

            bool beginsWord = matchIndex == 0 || !char.IsLetterOrDigit(label[matchIndex - 1]);
            return (beginsWord ? 20 : 100) + matchIndex;
        }

        // component.md defines the serialized field types exposed by the accessible view.
        private static bool IsSupportedComponentProperty(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Boolean ||
                property.propertyType == SerializedPropertyType.Integer ||
                property.propertyType == SerializedPropertyType.Float ||
                property.propertyType == SerializedPropertyType.String ||
                property.propertyType == SerializedPropertyType.Vector2 ||
                property.propertyType == SerializedPropertyType.Vector3 ||
                property.propertyType == SerializedPropertyType.Color ||
                property.propertyType == SerializedPropertyType.Enum ||
                property.propertyType == SerializedPropertyType.ObjectReference;
        }

        private void AddTransformComponentProperties(Transform componentTransform)
        {
            Vector3 position = componentTransform.localPosition;
            Vector3 rotation = componentTransform.localEulerAngles;
            Vector3 scale = componentTransform.localScale;
            allComponentProperties.Add(ComponentPropertyItem.FromTransformAxis("X position", "position", 0, position.x));
            allComponentProperties.Add(ComponentPropertyItem.FromTransformAxis("Y position", "position", 1, position.y));
            allComponentProperties.Add(ComponentPropertyItem.FromTransformAxis("Z position", "position", 2, position.z));
            allComponentProperties.Add(ComponentPropertyItem.FromTransformAxis("X rotation", "rotation", 0, rotation.x));
            allComponentProperties.Add(ComponentPropertyItem.FromTransformAxis("Y rotation", "rotation", 1, rotation.y));
            allComponentProperties.Add(ComponentPropertyItem.FromTransformAxis("Z rotation", "rotation", 2, rotation.z));
            allComponentProperties.Add(ComponentPropertyItem.FromTransformAxis("X scale", "scale", 0, scale.x));
            allComponentProperties.Add(ComponentPropertyItem.FromTransformAxis("Y scale", "scale", 1, scale.y));
            allComponentProperties.Add(ComponentPropertyItem.FromTransformAxis("Z scale", "scale", 2, scale.z));
        }

        private void MovePropertySelection(int direction)
        {
            if (componentProperties.Count == 0)
            {
                SpeakSafely(string.IsNullOrWhiteSpace(componentSearchText)
                    ? "This component has no editable properties."
                    : "No search results.");
                return;
            }

            selectedPropertyIndex = AccessibleList.Move(selectedPropertyIndex, direction, componentProperties.Count);
            SpeakSafely(GetSelectedPropertyDescription() + ", " +
                AccessibleList.Position(selectedPropertyIndex, componentProperties.Count) + ".");
            Repaint();
        }

        private void ActivateSelectedProperty()
        {
            if (componentProperties.Count == 0)
            {
                SpeakSafely(string.IsNullOrWhiteSpace(componentSearchText)
                    ? "This component has no editable properties."
                    : "No search results.");
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
                item.PropertyType == SerializedPropertyType.Vector3 ||
                item.PropertyType == SerializedPropertyType.Color) && item.IsEditable)
            {
                textEdit.Begin(item.Value);
                SpeakSafely("Editing " + item.Label + ". Current value " + item.Value +
                    ". Type a number and press Enter. Escape cancels.");
                Repaint();
                return;
            }

            if (item.PropertyType == SerializedPropertyType.String && item.IsEditable)
            {
                textEdit.Begin(item.Value);
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

            if (item.IsReflectedProperty)
            {
                CommitReflectedPropertyEdit(item);
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
                property.stringValue = textEdit.Value;
            }
            else if (item.VectorAxis >= 0)
            {
                float axisValue;
                if (!float.TryParse(textEdit.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out axisValue) ||
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
                else if (property.propertyType == SerializedPropertyType.Vector3)
                {
                    Vector3 value = property.vector3Value;
                    value[item.VectorAxis] = axisValue;
                    property.vector3Value = value;
                }
                else
                {
                    Color value = property.colorValue;
                    value[item.VectorAxis] = axisValue;
                    property.colorValue = value;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                long integerValue;
                if (!long.TryParse(textEdit.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue))
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
                if (!double.TryParse(textEdit.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out realValue) ||
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
            if (item.IsReflectedProperty)
            {
                UnityEngine.Object reflectedValue = item.ReflectedProperty.GetValue(inspectedComponent, null) as UnityEngine.Object;
                ObjectSelector.Open(
                    item.ReflectedProperty.PropertyType,
                    inspectedComponent,
                    reflectedValue,
                    selectedObject => ApplyReflectedPropertyValue(item.ReflectedProperty, item.Label, selectedObject));
                return;
            }

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
            ObjectSelector.Open(
                requiredType,
                inspectedComponent,
                currentValue,
                selectedObject => ApplyObjectReference(item.PropertyPath, item.Label, selectedObject));
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

        private void CommitReflectedPropertyEdit(ComponentPropertyItem item)
        {
            Type propertyType = item.ReflectedProperty.PropertyType;
            object value;
            if (propertyType == typeof(string))
            {
                value = textEdit.Value;
            }
            else if (propertyType == typeof(int))
            {
                int integerValue;
                if (!int.TryParse(textEdit.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue))
                {
                    SpeakSafely("Invalid whole number.");
                    return;
                }

                value = integerValue;
            }
            else
            {
                float floatValue;
                if (!float.TryParse(textEdit.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue) ||
                    float.IsNaN(floatValue) || float.IsInfinity(floatValue))
                {
                    SpeakSafely("Invalid number. Use digits, a minus sign, and a decimal point.");
                    return;
                }

                if (item.VectorAxis >= 0)
                {
                    object currentValue = item.ReflectedProperty.GetValue(inspectedComponent, null);
                    if (propertyType == typeof(Vector2))
                    {
                        Vector2 vector = (Vector2)currentValue;
                        vector[item.VectorAxis] = floatValue;
                        value = vector;
                    }
                    else if (propertyType == typeof(Vector3))
                    {
                        Vector3 vector = (Vector3)currentValue;
                        vector[item.VectorAxis] = floatValue;
                        value = vector;
                    }
                    else
                    {
                        Color color = (Color)currentValue;
                        color[item.VectorAxis] = floatValue;
                        value = color;
                    }
                }
                else
                {
                    value = floatValue;
                }
            }

            ApplyReflectedPropertyValue(item.ReflectedProperty, item.Label, value);
        }

        private void ApplyReflectedPropertyValue(PropertyInfo property, string label, object value)
        {
            try
            {
                Undo.RecordObject(inspectedComponent, "Unity Access edit " + label);
                property.SetValue(inspectedComponent, value, null);
                EditorUtility.SetDirty(inspectedComponent);
                PrefabUtility.RecordPrefabInstancePropertyModifications(inspectedComponent);
                textEdit.End();
                isChoosingOption = false;
                RefreshComponentProperties();
                SpeakSafely(label + " changed to " + componentProperties[selectedPropertyIndex].Value + ".");
                Repaint();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(InspectorAccessibilityWindow), exception);
                SpeakSafely("Unity could not change " + label + ". See debug.txt for details.");
            }
        }

        private void CommitTransformComponentEdit(ComponentPropertyItem item)
        {
            float parsedValue;
            if (!float.TryParse(textEdit.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue) ||
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
            textEdit.End();
            RefreshComponentProperties();
            SpeakSafely(item.Label + " changed to " + FormatFloat(parsedValue) + ".");
            Repaint();
        }

        private void ToggleBooleanProperty(ComponentPropertyItem item)
        {
            if (item.IsReflectedProperty)
            {
                bool currentValue = (bool)item.ReflectedProperty.GetValue(inspectedComponent, null);
                ApplyReflectedPropertyValue(item.ReflectedProperty, item.Label, !currentValue);
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
            Undo.RecordObject(inspectedComponent, "Unity Access toggle " + item.Label);
            property.boolValue = !property.boolValue;
            ApplyComponentPropertyChange(serializedObject, item.Label);
        }

        private void MoveOptionSelection(int direction)
        {
            ComponentPropertyItem item = componentProperties[selectedPropertyIndex];
            selectedOptionIndex = AccessibleList.Move(selectedOptionIndex, direction, item.Options.Length);
            SpeakSafely(item.Options[selectedOptionIndex] + ", " +
                AccessibleList.Position(selectedOptionIndex, item.Options.Length) + ".");
            Repaint();
        }

        private void CommitOption()
        {
            ComponentPropertyItem item = componentProperties[selectedPropertyIndex];
            if (item.IsReflectedProperty)
            {
                object value = Enum.Parse(item.ReflectedProperty.PropertyType, item.Options[selectedOptionIndex]);
                ApplyReflectedPropertyValue(item.ReflectedProperty, item.Label, value);
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
            Undo.RecordObject(inspectedComponent, "Unity Access choose " + item.Label);
            property.enumValueIndex = selectedOptionIndex;
            ApplyComponentPropertyChange(serializedObject, item.Label);
        }

        private void ApplyComponentPropertyChange(SerializedObject serializedObject, string label)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(inspectedComponent);
            textEdit.End();
            isChoosingOption = false;
            RefreshComponentProperties();
            SpeakSafely(label + " changed to " + componentProperties[selectedPropertyIndex].Value + ".");
            Repaint();
        }

        private void HandleMissingProperty()
        {
            textEdit.End();
            isChoosingOption = false;
            RefreshComponentProperties();
            SpeakSafely("That property is no longer available. The component view was refreshed.");
            Repaint();
        }

        private string GetSelectedPropertyDescription()
        {
            if (componentProperties.Count == 0)
            {
                return "No editable properties";
            }

            ComponentPropertyItem item = componentProperties[selectedPropertyIndex];
            string controlType = item.PropertyType == SerializedPropertyType.Boolean
                ? ", check box"
                : item.PropertyType == SerializedPropertyType.Enum
                    ? ", combo box"
                    : item.PropertyType == SerializedPropertyType.ObjectReference
                        ? ", object selector"
                        : ", editable field";
            return item.Label + ", " + item.Value +
                (item.IsEditable ? controlType + ", editable" : ", read only");
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

            selectedComponentTypeIndex = AccessibleList.Move(selectedComponentTypeIndex, direction, componentTypes.Count);
            SpeakSafely(GetComponentDisplayName(componentTypes[selectedComponentTypeIndex]) + ", " +
                AccessibleList.Position(selectedComponentTypeIndex, componentTypes.Count) + ".");
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
            string controlType = item.Kind == InspectorItemKind.Tag
                ? ", text box"
                : item.Kind == InspectorItemKind.Layer
                    ? ", combo box"
                    : string.Empty;
            return item.Label + ", " + item.Value + controlType +
                (item.IsEditable ? ", editable" : ", read only");
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void SpeakSafely(string message)
        {
            AccessibleSpeech.Speak(message, nameof(InspectorAccessibilityWindow));
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
                int vectorAxis = -1,
                PropertyInfo reflectedProperty = null)
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
                ReflectedProperty = reflectedProperty;
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

            internal PropertyInfo ReflectedProperty { get; private set; }

            internal bool IsReflectedProperty
            {
                get { return ReflectedProperty != null; }
            }

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
                string axisName = property.propertyType == SerializedPropertyType.Color
                    ? axis == 0 ? "Red" : axis == 1 ? "Green" : axis == 2 ? "Blue" : "Alpha"
                    : axis == 0 ? "X" : axis == 1 ? "Y" : "Z";
                float value = property.propertyType == SerializedPropertyType.Vector2
                    ? property.vector2Value[axis]
                    : property.propertyType == SerializedPropertyType.Vector3
                        ? property.vector3Value[axis]
                        : property.colorValue[axis];
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

            internal static ComponentPropertyItem FromReflectedProperty(PropertyInfo property, object value, int vectorAxis = -1)
            {
                Type propertyType = property.PropertyType;
                SerializedPropertyType displayedType = GetDisplayedPropertyType(propertyType);
                string label = ObjectNames.NicifyVariableName(property.Name);
                string[] options = propertyType.IsEnum ? Enum.GetNames(propertyType) : Array.Empty<string>();
                int optionIndex = propertyType.IsEnum ? Array.IndexOf(options, value.ToString()) : -1;
                string displayedValue;
                if (vectorAxis >= 0)
                {
                    label += propertyType == typeof(Color)
                        ? vectorAxis == 0 ? " Red" : vectorAxis == 1 ? " Green" : vectorAxis == 2 ? " Blue" : " Alpha"
                        : vectorAxis == 0 ? " X" : vectorAxis == 1 ? " Y" : " Z";
                    float axisValue = propertyType == typeof(Vector2)
                        ? ((Vector2)value)[vectorAxis]
                        : propertyType == typeof(Vector3)
                            ? ((Vector3)value)[vectorAxis]
                            : ((Color)value)[vectorAxis];
                    displayedValue = FormatFloat(axisValue);
                }
                else if (propertyType == typeof(bool))
                {
                    displayedValue = (bool)value ? "On" : "Off";
                }
                else if (typeof(UnityEngine.Object).IsAssignableFrom(propertyType))
                {
                    UnityEngine.Object objectValue = value as UnityEngine.Object;
                    displayedValue = objectValue == null ? "None" : objectValue.name;
                }
                else if (propertyType == typeof(float))
                {
                    displayedValue = FormatFloat((float)value);
                }
                else
                {
                    displayedValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                }

                return new ComponentPropertyItem(
                    label,
                    displayedValue,
                    string.Empty,
                    displayedType,
                    true,
                    options,
                    optionIndex,
                    string.Empty,
                    -1,
                    vectorAxis,
                    property);
            }

            private static SerializedPropertyType GetDisplayedPropertyType(Type propertyType)
            {
                if (propertyType == typeof(bool))
                {
                    return SerializedPropertyType.Boolean;
                }

                if (propertyType == typeof(int))
                {
                    return SerializedPropertyType.Integer;
                }

                if (propertyType == typeof(float))
                {
                    return SerializedPropertyType.Float;
                }

                if (propertyType == typeof(string))
                {
                    return SerializedPropertyType.String;
                }

                if (propertyType == typeof(Vector2))
                {
                    return SerializedPropertyType.Vector2;
                }

                if (propertyType == typeof(Vector3))
                {
                    return SerializedPropertyType.Vector3;
                }

                if (propertyType == typeof(Color))
                {
                    return SerializedPropertyType.Color;
                }

                if (propertyType.IsEnum)
                {
                    return SerializedPropertyType.Enum;
                }

                return SerializedPropertyType.ObjectReference;
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
                get
                {
                    return Kind == InspectorItemKind.Name ||
                        Kind == InspectorItemKind.Tag ||
                        Kind == InspectorItemKind.Layer ||
                        setter != null;
                }
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
            Tag,
            Layer,
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
