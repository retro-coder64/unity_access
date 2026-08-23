using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityAccess
{
    /// <summary>Provides a keyboard- and NVDA-accessible material creator and editor.</summary>
    public sealed class MaterialWindow : EditorWindow
    {
        private const string SourceFile = "material_window.cs";
        private const float RowHeight = 22.0f;

        // This ordered list is the keyboard focus model. Unity IMGUI focus is not exposed
        // reliably to NVDA, so selection, speech, activation, and scrolling are explicit.
        private readonly List<ControlEntry> controls = new List<ControlEntry>();
        private readonly AccessibleTextEdit textEdit = new AccessibleTextEdit();
        private Material material;
        private Shader shader;
        private string materialName = "New Material";
        private string createPath = string.Empty;
        private string pendingCreatePath = string.Empty;
        private string originalEditValue = string.Empty;
        private bool createMode;
        private bool topLevel = true;
        private int selectedIndex;
        private int topLevelIndex;
        private Vector2 scrollPosition;

        [MenuItem("Unity Access/Material Window", false, 10)]
        public static void Open()
        {
            MaterialWindow window = GetWindow<MaterialWindow>("Accessible Material", true);
            window.minSize = new Vector2(460.0f, 380.0f);
            window.ShowTopLevel();
            window.Focus();
            EditorApplication.delayCall += window.SpeakOpeningState;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= SpeakOpeningState;
            EditorApplication.delayCall -= CompleteBeginCreate;
            if (createMode && material != null && !AssetDatabase.Contains(material)) DestroyImmediate(material);
        }

        private void OnGUI()
        {
            try
            {
                if (topLevel)
                {
                    HandleTopLevelKeyboard(Event.current);
                    DrawTopLevel();
                    return;
                }

                HandleEditorKeyboard(Event.current);
                DrawEditor();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(SourceFile, exception);
                Speak("The material window encountered an error. See Editor debug.txt.");
            }
        }

        private void ShowTopLevel()
        {
            ReleaseUnsavedMaterial();
            topLevel = true;
            createMode = false;
            createPath = string.Empty;
            pendingCreatePath = string.Empty;
            materialName = "New Material";
            shader = null;
            controls.Clear();
            textEdit.End();
            selectedIndex = 0;
            topLevelIndex = 0;
        }

        private void DrawTopLevel()
        {
            EditorGUILayout.LabelField("Accessible Material", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Up or Down moves, Enter selects, Escape closes.");
            if (AccessibleControls.Button("Create material", topLevelIndex == 0)) BeginCreate();
            if (AccessibleControls.Button("Edit material", topLevelIndex == 1)) BeginEdit();
        }

        private void HandleTopLevelKeyboard(Event currentEvent)
        {
            if (!IsKeyDown(currentEvent)) return;
            int direction;
            if (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                topLevelIndex = AccessibleList.Move(topLevelIndex, direction, 2);
                SpeakTopLevelSelection();
            }
            else if (AccessibleKeyboard.IsConfirm(currentEvent))
            {
                if (topLevelIndex == 0) BeginCreate(); else BeginEdit();
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent))
            {
                Close();
            }
            else return;
            currentEvent.Use();
        }

        private void BeginCreate()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Material", "New Material", "mat", "Choose where to save the material.", "Assets");
            if (string.IsNullOrEmpty(path))
            {
                Speak("Material creation cancelled. " + TopLevelDescription());
                return;
            }

            string normalisedPath = path.Replace('\\', '/');
            if (!normalisedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                !normalisedPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                ReportError("The material path must be a mat file inside Assets.");
                return;
            }

            // The save panel is modal. Complete the view transition on the next editor
            // tick so Unity has restored this window before its controls are rebuilt.
            pendingCreatePath = normalisedPath;
            EditorApplication.delayCall -= CompleteBeginCreate;
            EditorApplication.delayCall += CompleteBeginCreate;
        }

        private void CompleteBeginCreate()
        {
            EditorApplication.delayCall -= CompleteBeginCreate;
            if (this == null || string.IsNullOrEmpty(pendingCreatePath)) return;
            ReleaseUnsavedMaterial();
            topLevel = false;
            createMode = true;
            createPath = pendingCreatePath;
            pendingCreatePath = string.Empty;
            materialName = System.IO.Path.GetFileNameWithoutExtension(createPath);
            shader = null;
            RebuildControls();
            Focus();
            Repaint();
            Speak("Create material editor opened for " + materialName +
                ". Select a Shader before saving. " + CurrentControlDescription());
        }

        private void BeginEdit()
        {
            ObjectSelector.Open(typeof(Material), this, null, OnMaterialSelected);
        }

        private void OnMaterialSelected(UnityEngine.Object selectedObject)
        {
            Material selectedMaterial = selectedObject as Material;
            if (selectedMaterial == null)
            {
                Speak("No material selected. " + TopLevelDescription());
                return;
            }

            ReleaseUnsavedMaterial();
            material = selectedMaterial;
            shader = material.shader;
            materialName = material.name;
            createPath = string.Empty;
            createMode = false;
            topLevel = false;
            RebuildControls();
            Speak("Material editor opened for " + material.name + ". " + CurrentControlDescription());
            Repaint();
        }

        private void DrawEditor()
        {
            EditorGUILayout.LabelField(createMode ? "Create Material" : "Edit Material", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Up or Down moves, Enter edits or activates, Escape closes.");
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int index = 0; index < controls.Count; index++) DrawControl(controls[index], index);
            EditorGUILayout.EndScrollView();
        }

        private void DrawControl(ControlEntry entry, int index)
        {
            bool selected = index == selectedIndex;
            switch (entry.Kind)
            {
                case ControlKind.MaterialName:
                    DrawTextControl(entry, index, selected, materialName);
                    break;
                case ControlKind.Shader:
                    if (AccessibleControls.Button("Select shader: " + (shader == null ? "None" : shader.name), selected))
                        ActivateControl(index);
                    break;
                case ControlKind.Float:
                case ControlKind.Integer:
                case ControlKind.ColorChannel:
                case ControlKind.VectorComponent:
                    DrawTextControl(entry, index, selected, GetControlValue(entry));
                    break;
                case ControlKind.Texture:
                    Texture texture = material.GetTexture(entry.PropertyName);
                    if (AccessibleControls.Button(entry.Label + ": " + (texture == null ? "None" : texture.name), selected))
                        ActivateControl(index);
                    break;
                case ControlKind.Save:
                    if (AccessibleControls.Button(createMode ? "Create and save material" : "Save material", selected))
                        ActivateControl(index);
                    break;
            }
        }

        private void DrawTextControl(ControlEntry entry, int index, bool selected, string currentValue)
        {
            Rect row = EditorGUILayout.GetControlRect(false, RowHeight);
            AccessibleEditorStyles.DrawSelection(row, selected);
            bool editingThisControl = selected && textEdit.IsEditing;
            string displayedValue = editingThisControl ? textEdit.Value : currentValue;
            string updatedValue = AccessibleControls.TextBox(
                row, ControlName(index), entry.Label, displayedValue, editingThisControl);
            if (editingThisControl) textEdit.Value = updatedValue;
            if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
            {
                selectedIndex = index;
                BeginTextEdit(entry);
            }
        }

        private void HandleEditorKeyboard(Event currentEvent)
        {
            if (!IsKeyDown(currentEvent)) return;
            if (textEdit.IsEditing)
            {
                if (AccessibleKeyboard.IsConfirm(currentEvent))
                {
                    CommitTextEdit();
                    currentEvent.Use();
                }
                else if (AccessibleKeyboard.IsCancel(currentEvent))
                {
                    textEdit.Value = originalEditValue;
                    textEdit.End();
                    Speak("Edit cancelled. " + CurrentControlDescription());
                    currentEvent.Use();
                }
                return;
            }

            int direction;
            if (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                selectedIndex = AccessibleList.Move(selectedIndex, direction, controls.Count);
                AccessibleList.KeepVisible(ref scrollPosition, selectedIndex, RowHeight);
                Speak(CurrentControlDescription());
            }
            else if (AccessibleKeyboard.IsConfirm(currentEvent))
            {
                ActivateControl(selectedIndex);
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent))
            {
                Close();
            }
            else return;
            currentEvent.Use();
        }

        private void ActivateControl(int index)
        {
            if (index < 0 || index >= controls.Count) return;
            selectedIndex = index;
            ControlEntry entry = controls[index];
            switch (entry.Kind)
            {
                case ControlKind.MaterialName:
                case ControlKind.Float:
                case ControlKind.Integer:
                case ControlKind.ColorChannel:
                case ControlKind.VectorComponent:
                    BeginTextEdit(entry);
                    break;
                case ControlKind.Shader:
                    ObjectSelector.Open(typeof(Shader), material == null ? this : material, shader, OnShaderSelected);
                    break;
                case ControlKind.Texture:
                    OpenTextureSelector(entry);
                    break;
                case ControlKind.Save:
                    SaveMaterial();
                    break;
            }
        }

        private void BeginTextEdit(ControlEntry entry)
        {
            originalEditValue = GetControlValue(entry);
            textEdit.Begin(originalEditValue);
            Speak(entry.Label + ", editable text box, " + originalEditValue + ". Type a value, then press Enter.");
            Repaint();
        }

        private void CommitTextEdit()
        {
            ControlEntry entry = controls[selectedIndex];
            string value = textEdit.Value;
            if (!ApplyControlValue(entry, value))
            {
                Speak("Invalid value. " + entry.Label + " remains " + originalEditValue + ".");
                return;
            }
            textEdit.End();
            Speak(entry.Label + " set to " + GetControlValue(entry) + ".");
            Repaint();
        }

        private void OnShaderSelected(UnityEngine.Object selectedObject)
        {
            Shader selectedShader = selectedObject as Shader;
            if (selectedShader == null)
            {
                Speak("A valid Shader is required.");
                return;
            }

            shader = selectedShader;
            if (material == null) material = new Material(shader);
            else material.shader = shader;
            RebuildControls(ControlKind.Shader);
            Speak("Shader changed to " + shader.name + ". Properties refreshed. " + CurrentControlDescription());
            Repaint();
        }

        private void OpenTextureSelector(ControlEntry entry)
        {
            Texture current = material.GetTexture(entry.PropertyName);
            string propertyName = entry.PropertyName;
            string label = entry.Label;
            ObjectSelector.Open(typeof(Texture), material, current, value =>
            {
                if (value != null && !EditorUtility.IsPersistent(value))
                {
                    Speak("Select a Texture asset from the project, or None.");
                    return;
                }
                material.SetTexture(propertyName, value as Texture);
                Speak(label + " set to " + (value == null ? "None" : value.name) + ".");
                Repaint();
            });
        }

        private void RebuildControls(ControlKind preferredKind = ControlKind.MaterialName)
        {
            controls.Clear();
            controls.Add(new ControlEntry(ControlKind.MaterialName, "Material name"));
            controls.Add(new ControlEntry(ControlKind.Shader, "Shader"));
            if (material != null && shader != null)
            {
                // Discover properties only through Unity's public Shader API.
                int propertyCount = shader.GetPropertyCount();
                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                    AddPropertyControls(propertyIndex);
            }
            controls.Add(new ControlEntry(ControlKind.Save, createMode ? "Create and save material" : "Save material"));
            selectedIndex = controls.FindIndex(entry => entry.Kind == preferredKind);
            selectedIndex = selectedIndex < 0 ? 0 : selectedIndex;
            textEdit.End();
        }

        private void AddPropertyControls(int propertyIndex)
        {
            string name = shader.GetPropertyName(propertyIndex);
            string description = shader.GetPropertyDescription(propertyIndex);
            string label = string.IsNullOrWhiteSpace(description) ? name : description;
            ShaderPropertyType type = shader.GetPropertyType(propertyIndex);
            switch (type)
            {
                case ShaderPropertyType.Color:
                    controls.Add(new ControlEntry(ControlKind.ColorChannel, label + " red, 0 to 255", name, propertyIndex, 0));
                    controls.Add(new ControlEntry(ControlKind.ColorChannel, label + " green, 0 to 255", name, propertyIndex, 1));
                    controls.Add(new ControlEntry(ControlKind.ColorChannel, label + " blue, 0 to 255", name, propertyIndex, 2));
                    break;
                case ShaderPropertyType.Float:
                    controls.Add(new ControlEntry(ControlKind.Float, label, name, propertyIndex));
                    break;
                case ShaderPropertyType.Range:
                    controls.Add(new ControlEntry(ControlKind.Float, label, name, propertyIndex, 0, true));
                    break;
                case ShaderPropertyType.Int:
                    controls.Add(new ControlEntry(ControlKind.Integer, label, name, propertyIndex));
                    break;
                case ShaderPropertyType.Vector:
                    controls.Add(new ControlEntry(ControlKind.VectorComponent, label + " X", name, propertyIndex, 0));
                    controls.Add(new ControlEntry(ControlKind.VectorComponent, label + " Y", name, propertyIndex, 1));
                    controls.Add(new ControlEntry(ControlKind.VectorComponent, label + " Z", name, propertyIndex, 2));
                    controls.Add(new ControlEntry(ControlKind.VectorComponent, label + " W", name, propertyIndex, 3));
                    break;
                case ShaderPropertyType.Texture:
                    controls.Add(new ControlEntry(ControlKind.Texture, label, name, propertyIndex));
                    break;
            }
        }

        private string GetControlValue(ControlEntry entry)
        {
            switch (entry.Kind)
            {
                case ControlKind.MaterialName: return materialName;
                case ControlKind.Float: return material.GetFloat(entry.PropertyName).ToString("R", CultureInfo.InvariantCulture);
                case ControlKind.Integer: return material.GetInt(entry.PropertyName).ToString(CultureInfo.InvariantCulture);
                case ControlKind.ColorChannel:
                    Color color = material.GetColor(entry.PropertyName);
                    return Mathf.RoundToInt(GetColorChannel(color, entry.Component) * 255.0f).ToString(CultureInfo.InvariantCulture);
                case ControlKind.VectorComponent:
                    return material.GetVector(entry.PropertyName)[entry.Component].ToString("R", CultureInfo.InvariantCulture);
                default: return string.Empty;
            }
        }

        private bool ApplyControlValue(ControlEntry entry, string value)
        {
            if (entry.Kind == ControlKind.MaterialName)
            {
                if (string.IsNullOrWhiteSpace(value)) return false;
                materialName = value.Trim();
                return true;
            }

            if (entry.Kind == ControlKind.Integer)
            {
                int integerValue;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue)) return false;
                material.SetInt(entry.PropertyName, integerValue);
                return true;
            }

            float number;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || float.IsNaN(number) || float.IsInfinity(number))
                return false;
            if (entry.Kind == ControlKind.Float)
            {
                if (entry.IsRange)
                {
                    Vector2 limits = shader.GetPropertyRangeLimits(entry.PropertyIndex);
                    number = Mathf.Clamp(number, limits.x, limits.y);
                }
                material.SetFloat(entry.PropertyName, number);
                return true;
            }
            if (entry.Kind == ControlKind.ColorChannel)
            {
                Color color = material.GetColor(entry.PropertyName);
                SetColorChannel(ref color, entry.Component, Mathf.Clamp(number, 0.0f, 255.0f) / 255.0f);
                material.SetColor(entry.PropertyName, color);
                return true;
            }
            if (entry.Kind == ControlKind.VectorComponent)
            {
                Vector4 vector = material.GetVector(entry.PropertyName);
                vector[entry.Component] = number;
                material.SetVector(entry.PropertyName, vector);
                return true;
            }
            return false;
        }

        private void SaveMaterial()
        {
            if (shader == null || material == null)
            {
                Speak("Select a valid Shader before saving the material.");
                return;
            }
            if (string.IsNullOrWhiteSpace(materialName))
            {
                Speak("Enter a valid material name before saving.");
                return;
            }

            material.name = materialName.Trim();
            if (createMode)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(createPath) != null)
                {
                    Speak("An asset already exists at " + createPath + ". Choose Create material again and select another path.");
                    return;
                }
                AssetDatabase.CreateAsset(material, createPath);
                if (!AssetDatabase.Contains(material)) throw new InvalidOperationException("Unity did not create " + createPath + ".");
                createMode = false;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(createPath, ImportAssetOptions.ForceUpdate);
                Selection.activeObject = material;
                EditorGUIUtility.PingObject(material);
                RebuildControls(ControlKind.Save);
                Speak("Material created and saved at " + createPath + ". The material editor is open. " + CurrentControlDescription());
                Repaint();
                return;
            }

            string path = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("The selected material is not a project asset.");
            string newName = materialName.Trim();
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(fileName, newName, StringComparison.Ordinal))
            {
                string renameError = AssetDatabase.RenameAsset(path, newName);
                if (!string.IsNullOrEmpty(renameError)) throw new InvalidOperationException(renameError);
            }
            material.name = newName;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Speak("Material saved as " + materialName.Trim() + ".");
        }

        private string CurrentControlDescription()
        {
            if (selectedIndex < 0 || selectedIndex >= controls.Count) return "No control selected.";
            ControlEntry entry = controls[selectedIndex];
            string role = entry.IsText ? "editable text box" : "button";
            string value = entry.IsText ? ", " + GetControlValue(entry) : string.Empty;
            return entry.Label + ", " + role + value + ", " + AccessibleList.Position(selectedIndex, controls.Count) + ".";
        }

        private void SpeakOpeningState()
        {
            if (this != null) Speak(TopLevelDescription());
        }

        private string TopLevelDescription()
        {
            return "Material window opened. Up or Down moves, Enter selects, Escape closes. " +
                (topLevelIndex == 0 ? "Create material" : "Edit material") + ", button, " +
                AccessibleList.Position(topLevelIndex, 2) + ".";
        }

        private void SpeakTopLevelSelection()
        {
            Speak((topLevelIndex == 0 ? "Create material" : "Edit material") + ", button, " +
                AccessibleList.Position(topLevelIndex, 2) + ".");
        }

        private void ReleaseUnsavedMaterial()
        {
            // A create-mode Material remains memory-only until AssetDatabase.CreateAsset succeeds.
            if (createMode && material != null && !AssetDatabase.Contains(material)) DestroyImmediate(material);
            material = null;
        }

        private void ReportError(string message)
        {
            PluginErrorLog.Write(SourceFile, new InvalidOperationException(message));
            Speak(message);
        }

        private static bool IsKeyDown(Event currentEvent)
        {
            return currentEvent != null && currentEvent.type == EventType.KeyDown;
        }

        private static string ControlName(int index)
        {
            return "MaterialControl" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static float GetColorChannel(Color color, int component)
        {
            if (component == 0) return color.r;
            if (component == 1) return color.g;
            return color.b;
        }

        private static void SetColorChannel(ref Color color, int component, float value)
        {
            if (component == 0) color.r = value;
            else if (component == 1) color.g = value;
            else color.b = value;
        }

        private static void Speak(string message)
        {
            AccessibleSpeech.Speak(message, SourceFile);
        }

        private enum ControlKind
        {
            MaterialName,
            Shader,
            Float,
            Integer,
            ColorChannel,
            VectorComponent,
            Texture,
            Save
        }

        private sealed class ControlEntry
        {
            internal ControlEntry(ControlKind kind, string label, string propertyName = "", int propertyIndex = -1,
                int component = 0, bool isRange = false)
            {
                Kind = kind;
                Label = label;
                PropertyName = propertyName;
                PropertyIndex = propertyIndex;
                Component = component;
                IsRange = isRange;
            }

            internal ControlKind Kind { get; private set; }
            internal string Label { get; private set; }
            internal string PropertyName { get; private set; }
            internal int PropertyIndex { get; private set; }
            internal int Component { get; private set; }
            internal bool IsRange { get; private set; }
            internal bool IsText
            {
                get
                {
                    return Kind == ControlKind.MaterialName || Kind == ControlKind.Float || Kind == ControlKind.Integer ||
                        Kind == ControlKind.ColorChannel || Kind == ControlKind.VectorComponent;
                }
            }
        }
    }
}
