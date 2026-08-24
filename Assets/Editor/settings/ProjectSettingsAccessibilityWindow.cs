using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>Provides keyboard and NVDA access to project settings backed by public Unity APIs.</summary>
    public sealed class ProjectSettingsAccessibilityWindow : EditorWindow
    {
        private const string WindowTitle = "Accessible Project Settings";
        private const string EditControlName = "UnityAccessProjectSettingValue";
        private const float RowHeight = 20.0f;

        private readonly List<IProjectSettingsAdapter> adapters = new List<IProjectSettingsAdapter>();
        private readonly List<ProjectSettingItem> settings = new List<ProjectSettingItem>();
        private readonly AccessibleTextEdit textEdit = new AccessibleTextEdit();
        private Vector2 categoryScroll;
        private Vector2 settingScroll;
        private int selectedCategoryIndex;
        private int selectedSettingIndex;
        private int selectedOptionIndex;
        private bool categoryListActive = true;
        private bool optionListActive;

        /// <summary>Opens the accessible replacement for the supported Project Settings categories.</summary>
        [MenuItem("Unity Access/Project Settings", false, 20)]
        public static void Open()
        {
            try
            {
                ProjectSettingsAccessibilityWindow window = GetWindow<ProjectSettingsAccessibilityWindow>();
                window.titleContent = new GUIContent(WindowTitle);
                window.minSize = new Vector2(520.0f, 300.0f);
                window.Show();
                window.Focus();
                window.ResetToCategories();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(ProjectSettingsAccessibilityWindow), exception);
            }
        }

        private void OnEnable()
        {
            BuildAdapters();
            selectedCategoryIndex = AccessibleList.Clamp(selectedCategoryIndex, adapters.Count);
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

        private void BuildAdapters()
        {
            adapters.Clear();
            adapters.Add(new ApplicationSettingsAdapter());
            adapters.Add(new DisplaySettingsAdapter());
            adapters.Add(new RenderingSettingsAdapter());
        }

        private void ResetToCategories()
        {
            textEdit.End();
            optionListActive = false;
            categoryListActive = true;
            selectedCategoryIndex = AccessibleList.Clamp(selectedCategoryIndex, adapters.Count);
            Speak("Project Settings. Settings categories list. " + GetCategoryDescription() + ". Use Up and Down, then Enter to open a category.");
            Repaint();
        }

        private void DrawWindow()
        {
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(categoryListActive
                ? "Settings categories. Up and Down navigate; Enter opens."
                : "Tab and Shift Tab navigate settings; Enter edits or changes; Escape returns to categories.");
            EditorGUILayout.BeginHorizontal();
            DrawCategories();
            DrawSettings();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategories()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(190.0f));
            EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);
            categoryScroll = EditorGUILayout.BeginScrollView(categoryScroll);
            for (int index = 0; index < adapters.Count; index++)
            {
                bool selected = index == selectedCategoryIndex;
                if (AccessibleControls.Button(adapters[index].Name, selected && categoryListActive))
                {
                    selectedCategoryIndex = index;
                    OpenSelectedCategory();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical();
            string heading = categoryListActive || adapters.Count == 0
                ? "Select a category"
                : adapters[selectedCategoryIndex].Name;
            EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
            settingScroll = EditorGUILayout.BeginScrollView(settingScroll);
            if (!categoryListActive)
            {
                for (int index = 0; index < settings.Count; index++)
                {
                    DrawSetting(index);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSetting(int index)
        {
            ProjectSettingItem item = settings[index];
            bool selected = index == selectedSettingIndex;
            Rect row = EditorGUILayout.GetControlRect(false, RowHeight);
            AccessibleEditorStyles.DrawSelection(row, selected && !categoryListActive);
            if (selected && textEdit.IsEditing)
            {
                textEdit.Value = AccessibleControls.TextBox(
                    row, EditControlName, item.Label, textEdit.Value, true);
                return;
            }

            string displayText = item.Label + ": " + item.DisplayValue;
            if (AccessibleControls.Button(row, displayText, selected && !categoryListActive))
            {
                selectedSettingIndex = index;
                ActivateSelectedSetting();
            }
        }

        private void HandleKeyboard(Event currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (textEdit.IsEditing)
            {
                if (AccessibleKeyboard.IsConfirm(currentEvent))
                {
                    CommitTextEdit();
                    currentEvent.Use();
                }
                else if (AccessibleKeyboard.IsCancel(currentEvent))
                {
                    textEdit.End();
                    Speak("Edit cancelled. " + GetSettingDescription() + ".");
                    currentEvent.Use();
                    Repaint();
                }

                return;
            }

            int direction;
            if (optionListActive && AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                MoveOption(direction);
            }
            else if (optionListActive && AccessibleKeyboard.IsConfirm(currentEvent))
            {
                CommitOption();
            }
            else if (optionListActive && AccessibleKeyboard.IsCancel(currentEvent))
            {
                optionListActive = false;
                Speak("Choice cancelled. " + GetSettingDescription() + ".");
            }
            else if (categoryListActive && AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                MoveCategory(direction);
            }
            else if (categoryListActive && AccessibleKeyboard.IsConfirm(currentEvent))
            {
                OpenSelectedCategory();
            }
            else if (!categoryListActive && currentEvent.keyCode == KeyCode.Tab)
            {
                MoveSetting(currentEvent.shift ? -1 : 1);
            }
            else if (!categoryListActive && AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                MoveSetting(direction);
            }
            else if (!categoryListActive && AccessibleKeyboard.IsConfirm(currentEvent))
            {
                ActivateSelectedSetting();
            }
            else if (!categoryListActive && AccessibleKeyboard.IsCancel(currentEvent))
            {
                ResetToCategories();
            }
            else
            {
                return;
            }

            currentEvent.Use();
        }

        private void MoveCategory(int direction)
        {
            selectedCategoryIndex = AccessibleList.Move(selectedCategoryIndex, direction, adapters.Count);
            AccessibleList.KeepVisible(ref categoryScroll, selectedCategoryIndex, RowHeight);
            Speak(GetCategoryDescription() + ".");
            Repaint();
        }

        private void OpenSelectedCategory()
        {
            if (adapters.Count == 0)
            {
                Speak("No supported Project Settings categories are available.");
                return;
            }

            settings.Clear();
            settings.AddRange(adapters[selectedCategoryIndex].Read());
            selectedSettingIndex = AccessibleList.Clamp(0, settings.Count);
            categoryListActive = false;
            optionListActive = false;
            textEdit.End();
            settingScroll = Vector2.zero;
            Speak(adapters[selectedCategoryIndex].Name + " settings opened. " + GetSettingDescription() + ".");
            Repaint();
        }

        private void MoveSetting(int direction)
        {
            selectedSettingIndex = AccessibleList.Move(selectedSettingIndex, direction, settings.Count);
            AccessibleList.KeepVisible(ref settingScroll, selectedSettingIndex, RowHeight);
            Speak(GetSettingDescription() + ".");
            Repaint();
        }

        private void ActivateSelectedSetting()
        {
            if (selectedSettingIndex < 0 || selectedSettingIndex >= settings.Count)
            {
                Speak("This category has no supported settings.");
                return;
            }

            ProjectSettingItem item = settings[selectedSettingIndex];
            if (item.Kind == ProjectSettingKind.Boolean)
            {
                TryApply(item, item.DisplayValue == "On" ? "Off" : "On");
            }
            else if (item.Kind == ProjectSettingKind.Option)
            {
                selectedOptionIndex = item.CurrentOptionIndex;
                optionListActive = true;
                Speak(item.Label + " combo box opened. " + item.Options[selectedOptionIndex] + ", " +
                    AccessibleList.Position(selectedOptionIndex, item.Options.Length) + ". Use Up and Down, then Enter.");
            }
            else
            {
                textEdit.Begin(item.DisplayValue);
                Speak(item.Label + ", editable text box, " + item.DisplayValue + ". Enter saves; Escape cancels.");
                Repaint();
            }
        }

        private void MoveOption(int direction)
        {
            ProjectSettingItem item = settings[selectedSettingIndex];
            selectedOptionIndex = AccessibleList.Move(selectedOptionIndex, direction, item.Options.Length);
            Speak(item.Options[selectedOptionIndex] + ", " + AccessibleList.Position(selectedOptionIndex, item.Options.Length) + ".");
            Repaint();
        }

        private void CommitOption()
        {
            ProjectSettingItem item = settings[selectedSettingIndex];
            string value = item.Options[selectedOptionIndex];
            optionListActive = false;
            TryApply(item, value);
        }

        private void CommitTextEdit()
        {
            ProjectSettingItem item = settings[selectedSettingIndex];
            string value = textEdit.Value;
            if (TryApply(item, value))
            {
                textEdit.End();
            }
        }

        private bool TryApply(ProjectSettingItem item, string value)
        {
            try
            {
                string error;
                if (!item.TrySet(value, out error))
                {
                    Speak(error + " The previous value was kept.");
                    return false;
                }

                RefreshSettings();
                Speak(item.Label + " changed to " + settings[selectedSettingIndex].DisplayValue + ".");
                Repaint();
                return true;
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(ProjectSettingsAccessibilityWindow), exception);
                Speak("Unity could not change " + item.Label + ". The previous value was kept. See debug.txt for details.");
                return false;
            }
        }

        private void RefreshSettings()
        {
            settings.Clear();
            settings.AddRange(adapters[selectedCategoryIndex].Read());
            selectedSettingIndex = AccessibleList.Clamp(selectedSettingIndex, settings.Count);
        }

        private string GetCategoryDescription()
        {
            return adapters.Count == 0 || selectedCategoryIndex < 0
                ? "No supported categories"
                : adapters[selectedCategoryIndex].Name + ", " +
                    AccessibleList.Position(selectedCategoryIndex, adapters.Count);
        }

        private string GetSettingDescription()
        {
            if (settings.Count == 0 || selectedSettingIndex < 0)
            {
                return "No supported settings";
            }

            ProjectSettingItem item = settings[selectedSettingIndex];
            return item.Label + ", " + item.DisplayValue + ", " + item.AccessibleControlType + ", " +
                AccessibleList.Position(selectedSettingIndex, settings.Count);
        }

        private static void Speak(string message)
        {
            AccessibleSpeech.Speak(message, nameof(ProjectSettingsAccessibilityWindow));
        }

        private interface IProjectSettingsAdapter
        {
            string Name { get; }

            IList<ProjectSettingItem> Read();
        }

        private sealed class ApplicationSettingsAdapter : IProjectSettingsAdapter
        {
            public string Name { get { return "Application"; } }

            public IList<ProjectSettingItem> Read()
            {
                return new List<ProjectSettingItem>
                {
                    ProjectSettingItem.Text("Company name", PlayerSettings.companyName, SetCompanyName),
                    ProjectSettingItem.Text("Product name", PlayerSettings.productName, SetProductName),
                    ProjectSettingItem.Text("Version", PlayerSettings.bundleVersion, SetVersion)
                };
            }

            private static bool SetCompanyName(string value, out string error)
            {
                return SetRequiredText(value, "Company name", delegate(string validValue) { PlayerSettings.companyName = validValue; }, out error);
            }

            private static bool SetProductName(string value, out string error)
            {
                return SetRequiredText(value, "Product name", delegate(string validValue) { PlayerSettings.productName = validValue; }, out error);
            }

            private static bool SetVersion(string value, out string error)
            {
                return SetRequiredText(value, "Version", delegate(string validValue) { PlayerSettings.bundleVersion = validValue; }, out error);
            }
        }

        private sealed class DisplaySettingsAdapter : IProjectSettingsAdapter
        {
            public string Name { get { return "Display"; } }

            public IList<ProjectSettingItem> Read()
            {
                return new List<ProjectSettingItem>
                {
                    ProjectSettingItem.Integer("Default screen width", PlayerSettings.defaultScreenWidth,
                        delegate(int value) { PlayerSettings.defaultScreenWidth = value; }),
                    ProjectSettingItem.Integer("Default screen height", PlayerSettings.defaultScreenHeight,
                        delegate(int value) { PlayerSettings.defaultScreenHeight = value; }),
                    ProjectSettingItem.Boolean("Run in background", PlayerSettings.runInBackground,
                        delegate(bool value) { PlayerSettings.runInBackground = value; }),
                    ProjectSettingItem.Boolean("Resizable window", PlayerSettings.resizableWindow,
                        delegate(bool value) { PlayerSettings.resizableWindow = value; }),
                    ProjectSettingItem.Enum("Full screen mode", PlayerSettings.fullScreenMode,
                        delegate(FullScreenMode value) { PlayerSettings.fullScreenMode = value; })
                };
            }
        }

        private sealed class RenderingSettingsAdapter : IProjectSettingsAdapter
        {
            public string Name { get { return "Rendering"; } }

            public IList<ProjectSettingItem> Read()
            {
                return new List<ProjectSettingItem>
                {
                    ProjectSettingItem.Enum("Color space", PlayerSettings.colorSpace,
                        delegate(ColorSpace value) { PlayerSettings.colorSpace = value; }),
                    ProjectSettingItem.Boolean("Incremental garbage collection", PlayerSettings.gcIncremental,
                        delegate(bool value) { PlayerSettings.gcIncremental = value; }),
                    ProjectSettingItem.Boolean("Graphics jobs", PlayerSettings.graphicsJobs,
                        delegate(bool value) { PlayerSettings.graphicsJobs = value; })
                };
            }
        }

        private static bool SetRequiredText(string value, string label, Action<string> setter, out string error)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = label + " cannot be empty.";
                return false;
            }

            setter(value.Trim());
            error = string.Empty;
            return true;
        }

        private delegate bool SettingSetter(string value, out string error);

        private sealed class ProjectSettingItem
        {
            private readonly SettingSetter setter;

            private ProjectSettingItem(
                string label,
                string displayValue,
                ProjectSettingKind kind,
                string[] options,
                int currentOptionIndex,
                SettingSetter setter)
            {
                Label = label;
                DisplayValue = displayValue;
                Kind = kind;
                Options = options;
                CurrentOptionIndex = currentOptionIndex;
                this.setter = setter;
            }

            internal string Label { get; private set; }

            internal string DisplayValue { get; private set; }

            internal ProjectSettingKind Kind { get; private set; }

            internal string[] Options { get; private set; }

            internal int CurrentOptionIndex { get; private set; }

            internal string AccessibleControlType
            {
                get
                {
                    return Kind == ProjectSettingKind.Boolean ? "check box" :
                        Kind == ProjectSettingKind.Option ? "combo box" : "editable text box";
                }
            }

            internal bool TrySet(string value, out string error)
            {
                return setter(value, out error);
            }

            internal static ProjectSettingItem Text(string label, string value, SettingSetter setter)
            {
                return new ProjectSettingItem(label, value ?? string.Empty, ProjectSettingKind.Text,
                    Array.Empty<string>(), -1, setter);
            }

            internal static ProjectSettingItem Integer(string label, int value, Action<int> setter)
            {
                return new ProjectSettingItem(label, value.ToString(CultureInfo.InvariantCulture), ProjectSettingKind.Integer,
                    Array.Empty<string>(), -1, delegate(string text, out string error)
                    {
                        int parsedValue;
                        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue) || parsedValue <= 0)
                        {
                            error = label + " must be a whole number greater than zero.";
                            return false;
                        }

                        setter(parsedValue);
                        error = string.Empty;
                        return true;
                    });
            }

            internal static ProjectSettingItem Boolean(string label, bool value, Action<bool> setter)
            {
                return new ProjectSettingItem(label, value ? "On" : "Off", ProjectSettingKind.Boolean,
                    new[] { "Off", "On" }, value ? 1 : 0, delegate(string text, out string error)
                    {
                        setter(string.Equals(text, "On", StringComparison.Ordinal));
                        error = string.Empty;
                        return true;
                    });
            }

            internal static ProjectSettingItem Enum<TEnum>(string label, TEnum value, Action<TEnum> setter)
                where TEnum : struct
            {
                string[] names = System.Enum.GetNames(typeof(TEnum));
                int currentIndex = Array.IndexOf(names, value.ToString());
                return new ProjectSettingItem(label, value.ToString(), ProjectSettingKind.Option,
                    names, Mathf.Max(0, currentIndex), delegate(string text, out string error)
                    {
                        TEnum parsedValue;
                        if (!System.Enum.TryParse(text, out parsedValue))
                        {
                            error = text + " is not a valid choice for " + label + ".";
                            return false;
                        }

                        setter(parsedValue);
                        error = string.Empty;
                        return true;
                    });
            }
        }

        private enum ProjectSettingKind
        {
            Text,
            Integer,
            Boolean,
            Option
        }
    }
}
