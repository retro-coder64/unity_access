using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>Keyboard and NVDA access to the phase-one Player and Tags and Layers settings.</summary>
    public sealed class ProjectSettingsAccessibilityWindow : EditorWindow
    {
        private const string WindowTitle = "Accessible Project Settings";
        private const string TextControlName = "UnityAccessProjectSettingsText";
        private const float RowHeight = 20.0f;
        private static readonly string[] CategoryNames = { "Player", "Tags and Layers" };

        private readonly List<SettingRow> rows = new List<SettingRow>();
        private readonly AccessibleTextEdit textEdit = new AccessibleTextEdit();
        private Vector2 scrollPosition;
        private WindowView view = WindowView.Categories;
        private TextOperation textOperation;
        private int selectedIndex;
        private int optionIndex;
        private int selectedSortingLayerIndex;
        private bool optionListOpen;

        /// <summary>Opens the accessible phase-one Project Settings window.</summary>
        [MenuItem("Unity Access/Project Settings", false, 20)]
        public static void Open()
        {
            try
            {
                ProjectSettingsAccessibilityWindow window = GetWindow<ProjectSettingsAccessibilityWindow>();
                window.titleContent = new GUIContent(WindowTitle);
                window.minSize = new Vector2(600.0f, 360.0f);
                window.Show();
                window.Focus();
                window.OpenCategories(true);
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(ProjectSettingsAccessibilityWindow), exception);
            }
        }

        private void OnGUI()
        {
            try
            {
                HandleKeyboard(Event.current);
                DrawWindow();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(ProjectSettingsAccessibilityWindow), exception);
                Speak("Project Settings encountered an error. See debug.txt for details.");
            }
        }

        private void DrawWindow()
        {
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(GetInstructions());
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (view == WindowView.Categories)
            {
                EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);
                for (int index = 0; index < CategoryNames.Length; index++)
                {
                    if (AccessibleControls.Button(CategoryNames[index], index == selectedIndex))
                    {
                        selectedIndex = index;
                        OpenSelectedCategory();
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField(GetViewName(), EditorStyles.boldLabel);
                DrawRows();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawRows()
        {
            if (rows.Count == 0)
            {
                EditorGUILayout.LabelField("No items are available.");
            }

            for (int index = 0; index < rows.Count; index++)
            {
                SettingRow row = rows[index];
                Rect rect = EditorGUILayout.GetControlRect(false, RowHeight);
                if (textEdit.IsEditing && index == selectedIndex)
                {
                    textEdit.Value = AccessibleControls.TextBox(rect, TextControlName, row.Label, textEdit.Value, true);
                }
                else if (AccessibleControls.Button(rect, row.Label + ": " + row.Value, index == selectedIndex))
                {
                    selectedIndex = index;
                    ActivateSelectedRow();
                }
            }
        }

        private string GetInstructions()
        {
            if (textEdit.IsEditing) return "Enter saves; Escape cancels the edit.";
            if (optionListOpen) return "Up and Down choose a value; Enter applies; Escape cancels.";
            if (view == WindowView.Categories) return "Up and Down navigate categories; Enter opens the selected category.";
            return "Up and Down or Tab navigate; Enter activates; Escape returns.";
        }

        private void HandleKeyboard(Event currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown) return;

            if (textEdit.IsEditing)
            {
                if (AccessibleKeyboard.IsConfirm(currentEvent)) CommitTextEdit();
                else if (AccessibleKeyboard.IsCancel(currentEvent)) CancelTextEdit();
                else return;
                currentEvent.Use();
                return;
            }

            int direction;
            if (optionListOpen)
            {
                if (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction)) MoveOption(direction);
                else if (AccessibleKeyboard.IsConfirm(currentEvent)) CommitOption();
                else if (AccessibleKeyboard.IsCancel(currentEvent))
                {
                    optionListOpen = false;
                    Speak("Choice cancelled. " + DescribeSelectedRow() + ".");
                }
                else return;
                currentEvent.Use();
                return;
            }

            int itemCount = view == WindowView.Categories ? CategoryNames.Length : rows.Count;
            if (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction) || TryGetTabDirection(currentEvent, out direction))
            {
                selectedIndex = AccessibleList.Move(selectedIndex, direction, itemCount);
                AccessibleList.KeepVisible(ref scrollPosition, selectedIndex, RowHeight);
                Speak(view == WindowView.Categories ? DescribeCategory() + "." : DescribeSelectedRow() + ".");
            }
            else if (AccessibleKeyboard.IsConfirm(currentEvent))
            {
                if (view == WindowView.Categories) OpenSelectedCategory(); else ActivateSelectedRow();
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent) && view != WindowView.Categories) ReturnFromCurrentView();
            else return;
            currentEvent.Use();
        }

        private static bool TryGetTabDirection(Event currentEvent, out int direction)
        {
            direction = 0;
            if (currentEvent.keyCode != KeyCode.Tab) return false;
            direction = currentEvent.shift ? -1 : 1;
            return true;
        }

        private void OpenCategories(bool announce)
        {
            view = WindowView.Categories;
            rows.Clear();
            selectedIndex = AccessibleList.Clamp(selectedIndex, CategoryNames.Length);
            scrollPosition = Vector2.zero;
            textEdit.End();
            optionListOpen = false;
            if (announce) Speak("Project Settings categories. " + DescribeCategory() + ". Use Up and Down, then Enter.");
            Repaint();
        }

        private string DescribeCategory()
        {
            return CategoryNames[selectedIndex] + ", " + AccessibleList.Position(selectedIndex, CategoryNames.Length);
        }

        private void OpenSelectedCategory()
        {
            if (selectedIndex == 0)
            {
                view = WindowView.Player;
                RefreshPlayerRows();
            }
            else
            {
                view = WindowView.TagsAndLayers;
                RefreshTagsAndLayersActions();
            }

            selectedIndex = AccessibleList.Clamp(0, rows.Count);
            scrollPosition = Vector2.zero;
            Speak(GetViewName() + " opened. " + DescribeSelectedRow() + ".");
            Repaint();
        }

        private void RefreshPlayerRows()
        {
            rows.Clear();
            rows.Add(SettingRow.Text("Company Name", PlayerSettings.companyName, TextOperation.CompanyName));
            rows.Add(SettingRow.Text("Product Name", PlayerSettings.productName, TextOperation.ProductName));
            rows.Add(SettingRow.Text("Version", PlayerSettings.bundleVersion, TextOperation.Version));
            Texture2D icon = GetDefaultIcon();
            rows.Add(SettingRow.Action("Default Icon", icon == null ? "None" : icon.name, RowAction.SelectIcon));
            rows.Add(SettingRow.Boolean("Run In Background", PlayerSettings.runInBackground, RowAction.ToggleRunInBackground));
            rows.Add(SettingRow.Options("Fullscreen Mode", PlayerSettings.fullScreenMode.ToString(),
                Enum.GetNames(typeof(FullScreenMode)), RowAction.SetFullscreenMode));

            // These controls are displayed only in modes where Unity uses them.
            if (PlayerSettings.fullScreenMode != FullScreenMode.Windowed)
            {
                rows.Add(SettingRow.Boolean("Default Is Native Resolution", PlayerSettings.defaultIsNativeResolution,
                    RowAction.ToggleNativeResolution));
            }
            else
            {
                rows.Add(SettingRow.Integer("Default Screen Width", PlayerSettings.defaultScreenWidth, TextOperation.ScreenWidth));
                rows.Add(SettingRow.Integer("Default Screen Height", PlayerSettings.defaultScreenHeight, TextOperation.ScreenHeight));
                rows.Add(SettingRow.Boolean("Resizable Window", PlayerSettings.resizableWindow, RowAction.ToggleResizableWindow));
            }
        }

        private void RefreshTagsAndLayersActions()
        {
            rows.Clear();
            rows.Add(SettingRow.Action("Tags", ProjectSettingsTagManager.TagCount + " tags", RowAction.OpenTags));
            rows.Add(SettingRow.Action("Sorting Layers", ProjectSettingsTagManager.SortingLayerCount + " sorting layers", RowAction.OpenSortingLayers));
            rows.Add(SettingRow.Action("Layers", "32 layer slots", RowAction.OpenLayers));
        }

        private void RefreshTags()
        {
            rows.Clear();
            IReadOnlyList<string> tags = ProjectSettingsTagManager.GetTags();
            for (int index = 0; index < tags.Count; index++)
            {
                bool removable = ProjectSettingsTagManager.CanRemoveTag(tags[index]);
                rows.Add(SettingRow.Action(tags[index], removable ? "custom; Enter removes" : "built-in; disabled",
                    RowAction.RemoveTag, !removable));
            }
            rows.Add(SettingRow.Action("Add Tag", "button", RowAction.AddTag));
        }

        private void RefreshSortingLayers()
        {
            rows.Clear();
            IReadOnlyList<string> layers = ProjectSettingsTagManager.GetSortingLayers();
            for (int index = 0; index < layers.Count; index++)
            {
                rows.Add(SettingRow.Action(layers[index], index == 0 ? "protected" : "Enter opens actions",
                    RowAction.OpenSortingLayerActions));
            }
            rows.Add(SettingRow.Action("Add Sorting Layer", "button", RowAction.AddSortingLayer));
        }

        private void RefreshLayers()
        {
            rows.Clear();
            IReadOnlyList<string> layers = ProjectSettingsTagManager.GetLayers();
            for (int index = 0; index < 32; index++)
            {
                string layerName = index < layers.Count ? layers[index] : string.Empty;
                bool editable = ProjectSettingsTagManager.CanEditLayer(index);
                rows.Add(SettingRow.Action("Layer " + index,
                    (string.IsNullOrEmpty(layerName) ? "Empty" : layerName) + (editable ? "; editable" : "; built-in; disabled"),
                    RowAction.EditLayer, !editable));
            }
        }

        private void OpenSortingLayerActions(int sortingLayerIndex)
        {
            selectedSortingLayerIndex = sortingLayerIndex;
            view = WindowView.SortingLayerActions;
            rows.Clear();
            string name = ProjectSettingsTagManager.GetSortingLayers()[sortingLayerIndex];
            bool protectedLayer = sortingLayerIndex == 0;
            int lastIndex = ProjectSettingsTagManager.SortingLayerCount - 1;
            rows.Add(SettingRow.Action("Rename", protectedLayer ? "disabled" : name, RowAction.RenameSortingLayer, protectedLayer));
            rows.Add(SettingRow.Action("Remove", protectedLayer ? "disabled" : "button", RowAction.RemoveSortingLayer, protectedLayer));
            rows.Add(SettingRow.Action("Move Up", sortingLayerIndex <= 1 ? "disabled" : "button",
                RowAction.MoveSortingLayerUp, sortingLayerIndex <= 1));
            rows.Add(SettingRow.Action("Move Down", sortingLayerIndex == 0 || sortingLayerIndex >= lastIndex ? "disabled" : "button",
                RowAction.MoveSortingLayerDown, sortingLayerIndex == 0 || sortingLayerIndex >= lastIndex));
            selectedIndex = 0;
            scrollPosition = Vector2.zero;
            Speak(name + " sorting layer actions. " + DescribeSelectedRow() + ".");
            Repaint();
        }

        private void ActivateSelectedRow()
        {
            if (selectedIndex < 0 || selectedIndex >= rows.Count) { Speak("No item is available."); return; }
            SettingRow row = rows[selectedIndex];
            if (row.Disabled) { Speak(row.Label + " is disabled. " + row.Value + "."); return; }
            if (row.TextOperation != TextOperation.None)
            {
                BeginTextEdit(row.TextOperation, row.Value, row.Label);
                return;
            }
            if (row.OptionNames.Length > 0)
            {
                optionIndex = Math.Max(0, Array.IndexOf(row.OptionNames, row.Value));
                optionListOpen = true;
                Speak(row.Label + " combo box opened. " + row.OptionNames[optionIndex] + ", " +
                    AccessibleList.Position(optionIndex, row.OptionNames.Length) + ".");
                return;
            }
            PerformAction(row.ActionKind);
        }

        private void PerformAction(RowAction action)
        {
            switch (action)
            {
                case RowAction.SelectIcon: SelectDefaultIcon(); break;
                case RowAction.ToggleRunInBackground: ApplyPlayerChange("Run In Background", delegate { PlayerSettings.runInBackground = !PlayerSettings.runInBackground; }); break;
                case RowAction.ToggleNativeResolution: ApplyPlayerChange("Default Is Native Resolution", delegate { PlayerSettings.defaultIsNativeResolution = !PlayerSettings.defaultIsNativeResolution; }); break;
                case RowAction.ToggleResizableWindow: ApplyPlayerChange("Resizable Window", delegate { PlayerSettings.resizableWindow = !PlayerSettings.resizableWindow; }); break;
                case RowAction.OpenTags: OpenList(WindowView.Tags, RefreshTags); break;
                case RowAction.OpenSortingLayers: OpenList(WindowView.SortingLayers, RefreshSortingLayers); break;
                case RowAction.OpenLayers: OpenList(WindowView.Layers, RefreshLayers); break;
                case RowAction.AddTag: BeginTextEdit(TextOperation.AddTag, string.Empty, "New Tag"); break;
                case RowAction.RemoveTag: RemoveSelectedTag(); break;
                case RowAction.AddSortingLayer: BeginTextEdit(TextOperation.AddSortingLayer, string.Empty, "New Sorting Layer"); break;
                case RowAction.OpenSortingLayerActions: OpenSortingLayerActions(selectedIndex); break;
                case RowAction.RenameSortingLayer: BeginTextEdit(TextOperation.RenameSortingLayer,
                    ProjectSettingsTagManager.GetSortingLayers()[selectedSortingLayerIndex], "Sorting Layer Name"); break;
                case RowAction.RemoveSortingLayer: RemoveSortingLayer(); break;
                case RowAction.MoveSortingLayerUp: MoveSortingLayer(-1); break;
                case RowAction.MoveSortingLayerDown: MoveSortingLayer(1); break;
                case RowAction.EditLayer: BeginTextEdit(TextOperation.EditLayer,
                    ProjectSettingsTagManager.GetLayers()[selectedIndex], "Layer " + selectedIndex + " Name"); break;
            }
        }

        private void OpenList(WindowView targetView, Action refresh)
        {
            view = targetView;
            refresh();
            selectedIndex = AccessibleList.Clamp(0, rows.Count);
            scrollPosition = Vector2.zero;
            Speak(GetViewName() + " opened. " + DescribeSelectedRow() + ".");
            Repaint();
        }

        private void MoveOption(int direction)
        {
            SettingRow row = rows[selectedIndex];
            optionIndex = AccessibleList.Move(optionIndex, direction, row.OptionNames.Length);
            Speak(row.OptionNames[optionIndex] + ", " + AccessibleList.Position(optionIndex, row.OptionNames.Length) + ".");
            Repaint();
        }

        private void CommitOption()
        {
            SettingRow row = rows[selectedIndex];
            string value = row.OptionNames[optionIndex];
            optionListOpen = false;
            FullScreenMode parsedMode;
            if (row.ActionKind == RowAction.SetFullscreenMode && Enum.TryParse(value, out parsedMode))
            {
                ApplyPlayerChange("Fullscreen Mode", delegate { PlayerSettings.fullScreenMode = parsedMode; });
            }
        }

        private void BeginTextEdit(TextOperation operation, string value, string label)
        {
            textOperation = operation;
            textEdit.Begin(value);
            Speak(label + ", editable text box, " + (string.IsNullOrEmpty(value) ? "empty" : value) + ". Enter saves; Escape cancels.");
            Repaint();
        }

        private void CancelTextEdit()
        {
            textEdit.End();
            textOperation = TextOperation.None;
            Speak("Edit cancelled. " + DescribeSelectedRow() + ".");
            Repaint();
        }

        private void CommitTextEdit()
        {
            string value = textEdit.Value.Trim();
            if (string.IsNullOrEmpty(value) && textOperation != TextOperation.EditLayer)
            {
                Speak("The value cannot be empty. The previous value was kept.");
                return;
            }

            try
            {
                switch (textOperation)
                {
                    case TextOperation.CompanyName: PlayerSettings.companyName = value; FinishPlayerTextChange("Company Name"); break;
                    case TextOperation.ProductName: PlayerSettings.productName = value; FinishPlayerTextChange("Product Name"); break;
                    case TextOperation.Version: PlayerSettings.bundleVersion = value; FinishPlayerTextChange("Version"); break;
                    case TextOperation.ScreenWidth: CommitPositiveInteger(value, true); break;
                    case TextOperation.ScreenHeight: CommitPositiveInteger(value, false); break;
                    case TextOperation.AddTag:
                        ProjectSettingsTagManager.AddTag(value); FinishManagerTextChange("Tag " + value + " added.", RefreshTags); break;
                    case TextOperation.AddSortingLayer:
                        ProjectSettingsTagManager.AddSortingLayer(value); FinishManagerTextChange("Sorting Layer " + value + " added.", RefreshSortingLayers); break;
                    case TextOperation.RenameSortingLayer:
                        ProjectSettingsTagManager.RenameSortingLayer(selectedSortingLayerIndex, value);
                        textEdit.End(); textOperation = TextOperation.None; OpenSortingLayerActions(selectedSortingLayerIndex);
                        Speak("Sorting Layer renamed to " + value + "."); break;
                    case TextOperation.EditLayer:
                        ProjectSettingsTagManager.SetLayerName(selectedIndex, value);
                        FinishManagerTextChange("Layer " + selectedIndex + " changed to " + (string.IsNullOrEmpty(value) ? "Empty" : value) + ".", RefreshLayers); break;
                }
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(ProjectSettingsAccessibilityWindow), exception);
                Speak(exception.Message + " The previous value was kept.");
            }
        }

        private void CommitPositiveInteger(string text, bool isWidth)
        {
            int value;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0)
            {
                Speak("The value must be a whole number greater than zero. The previous value was kept.");
                return;
            }
            if (isWidth) PlayerSettings.defaultScreenWidth = value; else PlayerSettings.defaultScreenHeight = value;
            FinishPlayerTextChange(isWidth ? "Default Screen Width" : "Default Screen Height");
        }

        private void FinishPlayerTextChange(string label)
        {
            textEdit.End(); textOperation = TextOperation.None;
            int previousIndex = selectedIndex;
            AssetDatabase.SaveAssets();
            RefreshPlayerRows();
            selectedIndex = AccessibleList.Clamp(previousIndex, rows.Count);
            Speak(label + " changed to " + rows[selectedIndex].Value + ".");
            Repaint();
        }

        private void FinishManagerTextChange(string message, Action refresh)
        {
            textEdit.End(); textOperation = TextOperation.None;
            refresh();
            selectedIndex = AccessibleList.Clamp(selectedIndex, rows.Count);
            Speak(message + " " + DescribeSelectedRow() + ".");
            Repaint();
        }

        private void ApplyPlayerChange(string label, Action change)
        {
            try
            {
                int previousIndex = selectedIndex;
                change();
                AssetDatabase.SaveAssets();
                RefreshPlayerRows();
                selectedIndex = AccessibleList.Clamp(previousIndex, rows.Count);
                Speak(label + " changed. " + DescribeSelectedRow() + ".");
                Repaint();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(ProjectSettingsAccessibilityWindow), exception);
                Speak("Unity could not change " + label + ". The previous value was kept. See debug.txt for details.");
            }
        }

        private static Texture2D GetDefaultIcon()
        {
            Texture2D[] icons = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Any);
            return icons.Length == 0 ? null : icons[0];
        }

        private void SelectDefaultIcon()
        {
            Speak("Default Icon object selector opened.");
            ObjectSelector.Open(typeof(Texture2D), this, GetDefaultIcon(), OnDefaultIconSelected);
        }

        private void OnDefaultIconSelected(UnityEngine.Object selectedObject)
        {
            try
            {
                Texture2D icon = selectedObject as Texture2D;
                int iconCount = Math.Max(1, PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any).Length);
                Texture2D[] icons = new Texture2D[iconCount];
                for (int index = 0; index < icons.Length; index++) icons[index] = icon;
                PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Any);
                AssetDatabase.SaveAssets();
                RefreshPlayerRows();
                Speak("Default Icon changed to " + (icon == null ? "None" : icon.name) + ".");
                Focus(); Repaint();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(ProjectSettingsAccessibilityWindow), exception);
                Speak("Unity could not change Default Icon. The previous value was kept. See debug.txt for details.");
            }
        }

        private void RemoveSelectedTag()
        {
            string tag = rows[selectedIndex].Label;
            Speak("Confirm removal of tag " + tag + ".");
            if (!EditorUtility.DisplayDialog("Remove Tag", "Remove the custom tag '" + tag + "'?", "Remove", "Cancel"))
            {
                Speak("Tag removal cancelled."); return;
            }
            ProjectSettingsTagManager.RemoveTag(tag);
            RefreshTags(); selectedIndex = AccessibleList.Clamp(selectedIndex, rows.Count);
            Speak(tag + " removed. " + DescribeSelectedRow() + "."); Repaint();
        }

        private void RemoveSortingLayer()
        {
            string name = ProjectSettingsTagManager.GetSortingLayers()[selectedSortingLayerIndex];
            Speak("Confirm removal of Sorting Layer " + name + ".");
            if (!EditorUtility.DisplayDialog("Remove Sorting Layer", "Remove the Sorting Layer '" + name + "'?", "Remove", "Cancel"))
            {
                Speak("Sorting Layer removal cancelled."); return;
            }
            ProjectSettingsTagManager.RemoveSortingLayer(selectedSortingLayerIndex);
            view = WindowView.SortingLayers; RefreshSortingLayers();
            selectedIndex = AccessibleList.Clamp(selectedSortingLayerIndex, rows.Count);
            Speak(name + " removed. " + DescribeSelectedRow() + "."); Repaint();
        }

        private void MoveSortingLayer(int direction)
        {
            ProjectSettingsTagManager.MoveSortingLayer(selectedSortingLayerIndex, direction);
            selectedSortingLayerIndex += direction;
            OpenSortingLayerActions(selectedSortingLayerIndex);
            Speak("Sorting Layer moved " + (direction < 0 ? "up" : "down") + ".");
        }

        private void ReturnFromCurrentView()
        {
            if (view == WindowView.Tags || view == WindowView.SortingLayers || view == WindowView.Layers)
            {
                view = WindowView.TagsAndLayers; RefreshTagsAndLayersActions(); selectedIndex = 0;
                Speak("Tags and Layers. " + DescribeSelectedRow() + ".");
            }
            else if (view == WindowView.SortingLayerActions)
            {
                view = WindowView.SortingLayers; RefreshSortingLayers();
                selectedIndex = AccessibleList.Clamp(selectedSortingLayerIndex, rows.Count);
                Speak("Sorting Layers. " + DescribeSelectedRow() + ".");
            }
            else
            {
                selectedIndex = view == WindowView.Player ? 0 : 1;
                OpenCategories(false);
                Speak("Project Settings categories. " + DescribeCategory() + ".");
            }
            scrollPosition = Vector2.zero; Repaint();
        }

        private string DescribeSelectedRow()
        {
            if (rows.Count == 0 || selectedIndex < 0) return "No items";
            SettingRow row = rows[selectedIndex];
            return row.Label + ", " + row.Value + (row.Disabled ? ", disabled" : string.Empty) + ", " +
                AccessibleList.Position(selectedIndex, rows.Count);
        }

        private string GetViewName()
        {
            switch (view)
            {
                case WindowView.Player: return "Player";
                case WindowView.TagsAndLayers: return "Tags and Layers";
                case WindowView.Tags: return "Tags";
                case WindowView.SortingLayers: return "Sorting Layers";
                case WindowView.SortingLayerActions: return "Sorting Layer Actions";
                case WindowView.Layers: return "Layers";
                default: return "Categories";
            }
        }

        private static void Speak(string message) { AccessibleSpeech.Speak(message, nameof(ProjectSettingsAccessibilityWindow)); }

        private sealed class SettingRow
        {
            private SettingRow(string label, string value, RowAction action, TextOperation operation, string[] options, bool disabled)
            { Label = label; Value = value; ActionKind = action; TextOperation = operation; OptionNames = options; Disabled = disabled; }
            internal string Label { get; private set; }
            internal string Value { get; private set; }
            internal RowAction ActionKind { get; private set; }
            internal TextOperation TextOperation { get; private set; }
            internal string[] OptionNames { get; private set; }
            internal bool Disabled { get; private set; }
            internal static SettingRow Text(string label, string value, TextOperation operation)
            { return new SettingRow(label, value ?? string.Empty, RowAction.None, operation, Array.Empty<string>(), false); }
            internal static SettingRow Integer(string label, int value, TextOperation operation)
            { return Text(label, value.ToString(CultureInfo.InvariantCulture), operation); }
            internal static SettingRow Boolean(string label, bool value, RowAction action)
            { return Action(label, value ? "On, checked" : "Off, not checked", action); }
            internal static SettingRow Options(string label, string value, string[] options, RowAction action)
            { return new SettingRow(label, value, action, TextOperation.None, options, false); }
            internal static SettingRow Action(string label, string value, RowAction action, bool disabled = false)
            { return new SettingRow(label, value, action, TextOperation.None, Array.Empty<string>(), disabled); }
        }

        private enum WindowView { Categories, Player, TagsAndLayers, Tags, SortingLayers, SortingLayerActions, Layers }
        private enum TextOperation { None, CompanyName, ProductName, Version, ScreenWidth, ScreenHeight, AddTag, AddSortingLayer, RenameSortingLayer, EditLayer }
        private enum RowAction
        {
            None, SelectIcon, ToggleRunInBackground, SetFullscreenMode, ToggleNativeResolution, ToggleResizableWindow,
            OpenTags, OpenSortingLayers, OpenLayers, AddTag, RemoveTag, AddSortingLayer, OpenSortingLayerActions,
            RenameSortingLayer, RemoveSortingLayer, MoveSortingLayerUp, MoveSortingLayerDown, EditLayer
        }
    }
}
