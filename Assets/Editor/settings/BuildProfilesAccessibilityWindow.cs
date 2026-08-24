using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>Provides keyboard and NVDA access to Unity Build Profile assets.</summary>
    public sealed class BuildProfilesAccessibilityWindow : EditorWindow
    {
        private const string WindowTitle = "Accessible Build Profiles";
        private const string EditControlName = "UnityAccessBuildProfileText";
        private const float RowHeight = 20.0f;

        private readonly List<ProfileEntry> profiles = new List<ProfileEntry>();
        private readonly List<ProfileAction> actions = new List<ProfileAction>();
        private readonly List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        private readonly List<string> defines = new List<string>();
        private readonly AccessibleTextEdit textEdit = new AccessibleTextEdit();
        private Vector2 scrollPosition;
        private BuildProfilesView currentView = BuildProfilesView.Profiles;
        private TextOperation textOperation;
        private int selectedProfileIndex;
        private int selectedActionIndex;
        private int selectedSceneIndex;
        private int selectedDefineIndex;

        /// <summary>Opens the accessible Build Profiles interface.</summary>
        [MenuItem("Unity Access/Build Profiles", false, 21)]
        public static void Open()
        {
            try
            {
                BuildProfilesAccessibilityWindow window = GetWindow<BuildProfilesAccessibilityWindow>();
                window.titleContent = new GUIContent(WindowTitle);
                window.minSize = new Vector2(560.0f, 320.0f);
                window.Show();
                window.Focus();
                window.RefreshProfiles(true);
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(BuildProfilesAccessibilityWindow), exception);
            }
        }

        private void OnEnable()
        {
            RefreshProfiles(false);
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
                PluginErrorLog.Write(nameof(BuildProfilesAccessibilityWindow), exception);
                Speak("Build Profiles encountered an error. See debug.txt for details.");
            }
        }

        private void DrawWindow()
        {
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(GetInstructions());
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (currentView == BuildProfilesView.Profiles)
            {
                DrawProfiles();
            }
            else if (currentView == BuildProfilesView.Actions)
            {
                DrawActions();
            }
            else if (currentView == BuildProfilesView.Scenes)
            {
                DrawScenes();
            }
            else
            {
                DrawDefines();
            }

            EditorGUILayout.EndScrollView();
        }

        private string GetInstructions()
        {
            if (textEdit.IsEditing)
            {
                return "Enter saves the text; Escape cancels.";
            }

            switch (currentView)
            {
                case BuildProfilesView.Profiles:
                    return "Up and Down navigate profiles; Enter opens the selected profile.";
                case BuildProfilesView.Actions:
                    return "Up and Down or Tab navigate actions; Enter activates; Escape returns to profiles.";
                case BuildProfilesView.Scenes:
                    return "Up and Down navigate; Enter toggles; A adds; Delete removes; Ctrl+Up or Ctrl+Down reorders; O changes scene source; Escape returns.";
                default:
                    return "Up and Down navigate; Enter edits; Insert adds; Delete removes; Escape returns.";
            }
        }

        private void DrawProfiles()
        {
            if (profiles.Count == 0)
            {
                EditorGUILayout.LabelField("No profiles are available.");
                return;
            }

            for (int index = 0; index < profiles.Count; index++)
            {
                ProfileEntry entry = profiles[index];
                string label = entry.Name + (entry.IsActive ? " (Active)" : string.Empty) +
                    (entry.Profile == null ? " - Platform profile" : " - Custom profile");
                if (AccessibleControls.Button(label, index == selectedProfileIndex))
                {
                    selectedProfileIndex = index;
                    OpenActions();
                }
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField(GetSelectedProfile().Name, EditorStyles.boldLabel);
            for (int index = 0; index < actions.Count; index++)
            {
                ProfileAction action = actions[index];
                Rect row = EditorGUILayout.GetControlRect(false, RowHeight);
                if (textEdit.IsEditing && index == selectedActionIndex)
                {
                    textEdit.Value = AccessibleControls.TextBox(
                        row, EditControlName, action.Label, textEdit.Value, true);
                }
                else if (AccessibleControls.Button(row, action.Label, index == selectedActionIndex))
                {
                    selectedActionIndex = index;
                    ActivateSelectedAction();
                }
            }
        }

        private void DrawScenes()
        {
            ProfileEntry entry = GetSelectedProfile();
            EditorGUILayout.LabelField(entry.Name + " scenes", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(entry.Profile != null && entry.Profile.overrideGlobalScenes
                ? "Scene source: This profile"
                : "Scene source: Global build scenes");
            if (scenes.Count == 0)
            {
                EditorGUILayout.LabelField("No scenes. Press A to add one.");
            }

            for (int index = 0; index < scenes.Count; index++)
            {
                EditorBuildSettingsScene scene = scenes[index];
                string sceneName = string.IsNullOrWhiteSpace(scene.path)
                    ? "Missing scene"
                    : Path.GetFileNameWithoutExtension(scene.path);
                string label = sceneName + " - " + (scene.enabled ? "Enabled" : "Disabled") + " - " + scene.path;
                if (AccessibleControls.Button(label, index == selectedSceneIndex))
                {
                    selectedSceneIndex = index;
                    ToggleSelectedScene();
                }
            }

            EditorGUILayout.Space();
            if (AccessibleControls.Button("Add scene (A)", false))
            {
                AddScene();
            }

            using (new EditorGUI.DisabledScope(scenes.Count == 0))
            {
                if (AccessibleControls.Button("Remove selected scene (Delete)", false))
                {
                    RemoveSelectedScene();
                }

                if (AccessibleControls.Button("Move selected scene up (Ctrl+Up)", false))
                {
                    MoveSelectedScene(-1);
                }

                if (AccessibleControls.Button("Move selected scene down (Ctrl+Down)", false))
                {
                    MoveSelectedScene(1);
                }
            }

            if (entry.Profile != null && AccessibleControls.Button("Change scene source (O)", false))
            {
                ToggleSceneOverride();
            }
        }

        private void DrawDefines()
        {
            EditorGUILayout.LabelField(GetSelectedProfile().Name + " scripting defines", EditorStyles.boldLabel);
            if (defines.Count == 0 && !textEdit.IsEditing)
            {
                EditorGUILayout.LabelField("No scripting defines. Press Insert to add one.");
            }

            for (int index = 0; index < defines.Count; index++)
            {
                Rect row = EditorGUILayout.GetControlRect(false, RowHeight);
                if (textEdit.IsEditing && textOperation == TextOperation.EditDefine && index == selectedDefineIndex)
                {
                    textEdit.Value = AccessibleControls.TextBox(
                        row, EditControlName, "Scripting define", textEdit.Value, true);
                }
                else if (AccessibleControls.Button(row, defines[index], index == selectedDefineIndex))
                {
                    selectedDefineIndex = index;
                    BeginEditDefine();
                }
            }

            if (textEdit.IsEditing && textOperation == TextOperation.AddDefine)
            {
                Rect row = EditorGUILayout.GetControlRect(false, RowHeight);
                textEdit.Value = AccessibleControls.TextBox(
                    row, EditControlName, "New scripting define", textEdit.Value, true);
            }

            EditorGUILayout.Space();
            if (AccessibleControls.Button("Add scripting define (Insert)", false))
            {
                BeginAddDefine();
            }

            using (new EditorGUI.DisabledScope(defines.Count == 0))
            {
                if (AccessibleControls.Button("Remove selected define (Delete)", false))
                {
                    RemoveSelectedDefine();
                }
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
                    CommitTextOperation();
                    currentEvent.Use();
                }
                else if (AccessibleKeyboard.IsCancel(currentEvent))
                {
                    textEdit.End();
                    textOperation = TextOperation.None;
                    Speak("Edit cancelled.");
                    currentEvent.Use();
                    Repaint();
                }

                return;
            }

            int direction;
            if (currentView == BuildProfilesView.Profiles && AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                MoveProfile(direction);
            }
            else if (currentView == BuildProfilesView.Profiles && AccessibleKeyboard.IsConfirm(currentEvent))
            {
                OpenActions();
            }
            else if (currentView == BuildProfilesView.Actions &&
                (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction) || TryGetTabDirection(currentEvent, out direction)))
            {
                MoveAction(direction);
            }
            else if (currentView == BuildProfilesView.Actions && AccessibleKeyboard.IsConfirm(currentEvent))
            {
                ActivateSelectedAction();
            }
            else if (currentView == BuildProfilesView.Scenes)
            {
                if (!HandleSceneKeyboard(currentEvent))
                {
                    return;
                }
            }
            else if (currentView == BuildProfilesView.Defines)
            {
                if (!HandleDefineKeyboard(currentEvent))
                {
                    return;
                }
            }
            else if (currentView == BuildProfilesView.Actions && AccessibleKeyboard.IsCancel(currentEvent))
            {
                ReturnToProfiles();
            }
            else
            {
                return;
            }

            currentEvent.Use();
        }

        private bool HandleSceneKeyboard(Event currentEvent)
        {
            int direction;
            if (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                if (currentEvent.control)
                {
                    MoveSelectedScene(direction);
                }
                else
                {
                    MoveSceneSelection(direction);
                }
            }
            else if (AccessibleKeyboard.IsConfirm(currentEvent))
            {
                ToggleSelectedScene();
            }
            else if (currentEvent.keyCode == KeyCode.A)
            {
                AddScene();
            }
            else if (currentEvent.keyCode == KeyCode.Delete || currentEvent.keyCode == KeyCode.Backspace)
            {
                RemoveSelectedScene();
            }
            else if (currentEvent.keyCode == KeyCode.O)
            {
                ToggleSceneOverride();
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent))
            {
                ReturnToActions();
            }
            else
            {
                return false;
            }

            return true;
        }

        private bool HandleDefineKeyboard(Event currentEvent)
        {
            int direction;
            if (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                MoveDefineSelection(direction);
            }
            else if (AccessibleKeyboard.IsConfirm(currentEvent))
            {
                BeginEditDefine();
            }
            else if (currentEvent.keyCode == KeyCode.Insert)
            {
                BeginAddDefine();
            }
            else if (currentEvent.keyCode == KeyCode.Delete || currentEvent.keyCode == KeyCode.Backspace)
            {
                RemoveSelectedDefine();
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent))
            {
                ReturnToActions();
            }
            else
            {
                return false;
            }

            return true;
        }

        private static bool TryGetTabDirection(Event currentEvent, out int direction)
        {
            direction = 0;
            if (currentEvent.keyCode != KeyCode.Tab)
            {
                return false;
            }

            direction = currentEvent.shift ? -1 : 1;
            return true;
        }

        private void RefreshProfiles(bool announce)
        {
            BuildProfile previouslySelected = profiles.Count > 0 && selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count
                ? profiles[selectedProfileIndex].Profile
                : null;
            profiles.Clear();
            BuildProfile activeProfile = BuildProfile.GetActiveBuildProfile();
            profiles.Add(new ProfileEntry(
                EditorUserBuildSettings.activeBuildTarget + " platform profile",
                string.Empty,
                null,
                activeProfile == null));

            string[] guids = AssetDatabase.FindAssets("t:BuildProfile");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
                if (profile != null)
                {
                    profiles.Add(new ProfileEntry(profile.name, path, profile, profile == activeProfile));
                }
            }

            ProfileEntry platformEntry = profiles[0];
            List<ProfileEntry> customProfiles = profiles.Skip(1)
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            profiles.Clear();
            profiles.Add(platformEntry);
            profiles.AddRange(customProfiles);
            selectedProfileIndex = 0;
            if (previouslySelected != null)
            {
                int previousIndex = profiles.FindIndex(entry => entry.Profile == previouslySelected);
                selectedProfileIndex = previousIndex >= 0 ? previousIndex : 0;
            }

            currentView = BuildProfilesView.Profiles;
            scrollPosition = Vector2.zero;
            if (announce)
            {
                Speak("Build profiles list. " + GetProfileDescription() + ". Use Up and Down, then Enter.");
            }

            Repaint();
        }

        private void MoveProfile(int direction)
        {
            selectedProfileIndex = AccessibleList.Move(selectedProfileIndex, direction, profiles.Count);
            AccessibleList.KeepVisible(ref scrollPosition, selectedProfileIndex, RowHeight);
            Speak(GetProfileDescription() + ".");
            Repaint();
        }

        private string GetProfileDescription()
        {
            if (profiles.Count == 0 || selectedProfileIndex < 0)
            {
                return "No profiles";
            }

            ProfileEntry entry = profiles[selectedProfileIndex];
            return entry.Name + (entry.IsActive ? ", active" : ", inactive") +
                (entry.Profile == null ? ", platform profile, " : ", custom profile, ") +
                AccessibleList.Position(selectedProfileIndex, profiles.Count);
        }

        private void OpenActions()
        {
            if (profiles.Count == 0)
            {
                Speak("No profiles are available.");
                return;
            }

            actions.Clear();
            ProfileEntry entry = GetSelectedProfile();
            actions.Add(new ProfileAction(entry.IsActive ? "Active profile" : "Activate profile", ProfileActionKind.Activate));
            actions.Add(new ProfileAction("Edit scenes", ProfileActionKind.EditScenes));
            if (entry.Profile != null)
            {
                actions.Add(new ProfileAction("Edit scripting defines", ProfileActionKind.EditDefines));
                actions.Add(new ProfileAction("Duplicate profile", ProfileActionKind.Duplicate));
                actions.Add(new ProfileAction("Rename profile", ProfileActionKind.Rename));
                actions.Add(new ProfileAction("Delete profile", ProfileActionKind.Delete));
                actions.Add(new ProfileAction("Build", ProfileActionKind.Build));
                actions.Add(new ProfileAction("Build and run", ProfileActionKind.BuildAndRun));
            }

            selectedActionIndex = 0;
            currentView = BuildProfilesView.Actions;
            scrollPosition = Vector2.zero;
            Speak(entry.Name + " actions. " + GetActionDescription() + ".");
            Repaint();
        }

        private void MoveAction(int direction)
        {
            selectedActionIndex = AccessibleList.Move(selectedActionIndex, direction, actions.Count);
            AccessibleList.KeepVisible(ref scrollPosition, selectedActionIndex, RowHeight);
            Speak(GetActionDescription() + ".");
            Repaint();
        }

        private string GetActionDescription()
        {
            return actions.Count == 0 ? "No actions" : actions[selectedActionIndex].Label + ", button, " +
                AccessibleList.Position(selectedActionIndex, actions.Count);
        }

        private void ActivateSelectedAction()
        {
            if (actions.Count == 0)
            {
                return;
            }

            switch (actions[selectedActionIndex].Kind)
            {
                case ProfileActionKind.Activate:
                    ActivateProfile();
                    break;
                case ProfileActionKind.EditScenes:
                    OpenScenes();
                    break;
                case ProfileActionKind.EditDefines:
                    OpenDefines();
                    break;
                case ProfileActionKind.Duplicate:
                    DuplicateProfile();
                    break;
                case ProfileActionKind.Rename:
                    BeginRename();
                    break;
                case ProfileActionKind.Delete:
                    DeleteProfile();
                    break;
                case ProfileActionKind.Build:
                    BuildSelectedProfile(false);
                    break;
                case ProfileActionKind.BuildAndRun:
                    BuildSelectedProfile(true);
                    break;
            }
        }

        private void ActivateProfile()
        {
            ProfileEntry entry = GetSelectedProfile();
            if (entry.IsActive)
            {
                Speak(entry.Name + " is already active.");
                return;
            }

            try
            {
                Speak("Activating " + entry.Name + ". Unity may reimport assets and recompile scripts.");
                BuildProfile.SetActiveBuildProfile(entry.Profile);
                EditorApplication.delayCall += delegate { RefreshProfiles(true); };
            }
            catch (Exception exception)
            {
                ReportError("activate " + entry.Name, exception);
            }
        }

        private void OpenScenes()
        {
            ProfileEntry entry = GetSelectedProfile();
            BuildProfile profile = entry.Profile;
            bool usesProfileScenes = profile != null && profile.overrideGlobalScenes;
            scenes.Clear();
            scenes.AddRange(usesProfileScenes ? profile.scenes : EditorBuildSettings.globalScenes);
            selectedSceneIndex = AccessibleList.Clamp(0, scenes.Count);
            currentView = BuildProfilesView.Scenes;
            scrollPosition = Vector2.zero;
            Speak(entry.Name + " scenes. " + (usesProfileScenes ? "Using this profile's scene list. " : "Using the global scene list. ") + GetSceneDescription() + ".");
            Repaint();
        }

        private void MoveSceneSelection(int direction)
        {
            selectedSceneIndex = AccessibleList.Move(selectedSceneIndex, direction, scenes.Count);
            AccessibleList.KeepVisible(ref scrollPosition, selectedSceneIndex, RowHeight);
            Speak(GetSceneDescription() + ".");
            Repaint();
        }

        private string GetSceneDescription()
        {
            if (scenes.Count == 0 || selectedSceneIndex < 0)
            {
                return "No scenes";
            }

            EditorBuildSettingsScene scene = scenes[selectedSceneIndex];
            return Path.GetFileNameWithoutExtension(scene.path) + ", " + (scene.enabled ? "enabled" : "disabled") +
                ", " + scene.path + ", " + AccessibleList.Position(selectedSceneIndex, scenes.Count);
        }

        private void AddScene()
        {
            Speak("Scene selector opened.");
            UnityEngine.Object selectorOwner = GetSelectedProfile().Profile != null
                ? GetSelectedProfile().Profile
                : this;
            ObjectSelector.Open(typeof(SceneAsset), selectorOwner, null, OnSceneSelected);
        }

        private void OnSceneSelected(UnityEngine.Object selectedObject)
        {
            SceneAsset sceneAsset = selectedObject as SceneAsset;
            if (sceneAsset == null)
            {
                Speak("No scene was added.");
                Focus();
                return;
            }

            string path = AssetDatabase.GetAssetPath(sceneAsset);
            if (scenes.Any(scene => string.Equals(scene.path, path, StringComparison.OrdinalIgnoreCase)))
            {
                Speak(sceneAsset.name + " is already in the scene list.");
                Focus();
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            selectedSceneIndex = scenes.Count - 1;
            SaveScenes("Add build scene");
            Speak(sceneAsset.name + " added, enabled, " + AccessibleList.Position(selectedSceneIndex, scenes.Count) + ".");
            Focus();
            Repaint();
        }

        private void ToggleSelectedScene()
        {
            if (scenes.Count == 0 || selectedSceneIndex < 0)
            {
                Speak("There is no scene to toggle.");
                return;
            }

            EditorBuildSettingsScene existing = scenes[selectedSceneIndex];
            scenes[selectedSceneIndex] = new EditorBuildSettingsScene(existing.path, !existing.enabled);
            SaveScenes("Toggle build scene");
            Speak(GetSceneDescription() + ".");
            Repaint();
        }

        private void RemoveSelectedScene()
        {
            if (scenes.Count == 0 || selectedSceneIndex < 0)
            {
                Speak("There is no scene to remove.");
                return;
            }

            string sceneName = Path.GetFileNameWithoutExtension(scenes[selectedSceneIndex].path);
            scenes.RemoveAt(selectedSceneIndex);
            selectedSceneIndex = AccessibleList.Clamp(selectedSceneIndex, scenes.Count);
            SaveScenes("Remove build scene");
            Speak(sceneName + " removed. " + GetSceneDescription() + ".");
            Repaint();
        }

        private void MoveSelectedScene(int direction)
        {
            if (scenes.Count == 0 || selectedSceneIndex < 0)
            {
                Speak("There is no scene to move.");
                return;
            }

            int destination = Mathf.Clamp(selectedSceneIndex + direction, 0, scenes.Count - 1);
            if (destination == selectedSceneIndex)
            {
                Speak(direction < 0 ? "The scene is already first." : "The scene is already last.");
                return;
            }

            EditorBuildSettingsScene scene = scenes[selectedSceneIndex];
            scenes.RemoveAt(selectedSceneIndex);
            scenes.Insert(destination, scene);
            selectedSceneIndex = destination;
            SaveScenes("Reorder build scenes");
            Speak(GetSceneDescription() + ".");
            Repaint();
        }

        private void ToggleSceneOverride()
        {
            BuildProfile profile = GetSelectedProfile().Profile;
            if (profile == null)
            {
                Speak("Platform profiles always use the global build scene list.");
                return;
            }

            Undo.RecordObject(profile, "Change build profile scene source");
            profile.overrideGlobalScenes = !profile.overrideGlobalScenes;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            OpenScenes();
            Speak(profile.overrideGlobalScenes
                ? "Now using this profile's scene list. " + GetSceneDescription() + "."
                : "Now using the global scene list. " + GetSceneDescription() + ".");
        }

        private void SaveScenes(string undoName)
        {
            BuildProfile profile = GetSelectedProfile().Profile;
            if (profile != null && profile.overrideGlobalScenes)
            {
                Undo.RecordObject(profile, undoName);
                profile.scenes = scenes.ToArray();
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
            else
            {
                EditorBuildSettings.globalScenes = scenes.ToArray();
            }
        }

        private void OpenDefines()
        {
            BuildProfile profile = RequireCustomProfile();
            if (profile == null)
            {
                return;
            }

            defines.Clear();
            defines.AddRange(profile.scriptingDefines ?? Array.Empty<string>());
            selectedDefineIndex = AccessibleList.Clamp(0, defines.Count);
            currentView = BuildProfilesView.Defines;
            scrollPosition = Vector2.zero;
            Speak(profile.name + " scripting defines. " + GetDefineDescription() + ".");
            Repaint();
        }

        private void MoveDefineSelection(int direction)
        {
            selectedDefineIndex = AccessibleList.Move(selectedDefineIndex, direction, defines.Count);
            AccessibleList.KeepVisible(ref scrollPosition, selectedDefineIndex, RowHeight);
            Speak(GetDefineDescription() + ".");
            Repaint();
        }

        private string GetDefineDescription()
        {
            return defines.Count == 0 || selectedDefineIndex < 0
                ? "No scripting defines"
                : defines[selectedDefineIndex] + ", editable, " + AccessibleList.Position(selectedDefineIndex, defines.Count);
        }

        private void BeginAddDefine()
        {
            textOperation = TextOperation.AddDefine;
            textEdit.Begin(string.Empty);
            Speak("New scripting define, editable text box, empty. Enter adds; Escape cancels.");
            Repaint();
        }

        private void BeginEditDefine()
        {
            if (defines.Count == 0 || selectedDefineIndex < 0)
            {
                Speak("There is no scripting define to edit. Press Insert to add one.");
                return;
            }

            textOperation = TextOperation.EditDefine;
            textEdit.Begin(defines[selectedDefineIndex]);
            Speak("Scripting define, editable text box, " + textEdit.Value + ". Enter saves; Escape cancels.");
            Repaint();
        }

        private void RemoveSelectedDefine()
        {
            if (defines.Count == 0 || selectedDefineIndex < 0)
            {
                Speak("There is no scripting define to remove.");
                return;
            }

            string removed = defines[selectedDefineIndex];
            defines.RemoveAt(selectedDefineIndex);
            selectedDefineIndex = AccessibleList.Clamp(selectedDefineIndex, defines.Count);
            SaveDefines("Remove scripting define");
            Speak(removed + " removed. " + GetDefineDescription() + ".");
            Repaint();
        }

        private void SaveDefines(string undoName)
        {
            BuildProfile profile = RequireCustomProfile();
            if (profile == null)
            {
                return;
            }

            Undo.RecordObject(profile, undoName);
            profile.scriptingDefines = defines.ToArray();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        private void BeginRename()
        {
            textOperation = TextOperation.RenameProfile;
            textEdit.Begin(GetSelectedProfile().Name);
            Speak("Profile name, editable text box, " + textEdit.Value + ". Enter renames; Escape cancels.");
            Repaint();
        }

        private void CommitTextOperation()
        {
            if (textOperation == TextOperation.RenameProfile)
            {
                CommitRename();
            }
            else
            {
                CommitDefine();
            }
        }

        private void CommitRename()
        {
            ProfileEntry entry = GetSelectedProfile();
            string newName = textEdit.Value.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                Speak("Profile name cannot be empty. The previous name was kept.");
                return;
            }

            string error = AssetDatabase.RenameAsset(entry.Path, newName);
            if (!string.IsNullOrEmpty(error))
            {
                PluginErrorLog.Write(nameof(BuildProfilesAccessibilityWindow), new InvalidOperationException(error));
                Speak("Unity could not rename the profile. " + error);
                return;
            }

            textEdit.End();
            textOperation = TextOperation.None;
            AssetDatabase.SaveAssets();
            RefreshProfiles(false);
            Speak("Profile renamed to " + newName + ". " + GetProfileDescription() + ".");
        }

        private void CommitDefine()
        {
            string value = textEdit.Value.Trim();
            if (string.IsNullOrEmpty(value))
            {
                Speak("A scripting define cannot be empty.");
                return;
            }

            int duplicateIndex = defines.FindIndex(define => string.Equals(define, value, StringComparison.Ordinal));
            if (duplicateIndex >= 0 && (textOperation == TextOperation.AddDefine || duplicateIndex != selectedDefineIndex))
            {
                Speak(value + " is already in the scripting define list.");
                return;
            }

            if (textOperation == TextOperation.AddDefine)
            {
                defines.Add(value);
                selectedDefineIndex = defines.Count - 1;
                SaveDefines("Add scripting define");
                Speak(value + " added, " + AccessibleList.Position(selectedDefineIndex, defines.Count) + ".");
            }
            else
            {
                defines[selectedDefineIndex] = value;
                SaveDefines("Edit scripting define");
                Speak("Scripting define changed to " + value + ".");
            }

            textEdit.End();
            textOperation = TextOperation.None;
            Repaint();
        }

        private void DuplicateProfile()
        {
            ProfileEntry entry = GetSelectedProfile();
            string directory = Path.GetDirectoryName(entry.Path) ?? "Assets";
            string destination = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, entry.Name + " Copy.asset").Replace('\\', '/'));
            if (!AssetDatabase.CopyAsset(entry.Path, destination))
            {
                Speak("Unity could not duplicate " + entry.Name + ".");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshProfiles(false);
            BuildProfile duplicate = AssetDatabase.LoadAssetAtPath<BuildProfile>(destination);
            int duplicateIndex = profiles.FindIndex(profile => profile.Profile == duplicate);
            selectedProfileIndex = duplicateIndex >= 0 ? duplicateIndex : selectedProfileIndex;
            Speak(entry.Name + " duplicated as " + (duplicate == null ? Path.GetFileNameWithoutExtension(destination) : duplicate.name) + ". " + GetProfileDescription() + ".");
            Repaint();
        }

        private void DeleteProfile()
        {
            ProfileEntry entry = GetSelectedProfile();
            Speak("Confirm deletion of " + entry.Name + ".");
            if (!EditorUtility.DisplayDialog(
                "Delete Build Profile",
                "Delete the build profile '" + entry.Name + "'? This cannot be undone.",
                "Delete",
                "Cancel"))
            {
                Speak("Deletion cancelled.");
                return;
            }

            if (entry.IsActive)
            {
                BuildProfile.SetActiveBuildProfile(null);
            }

            if (!AssetDatabase.DeleteAsset(entry.Path))
            {
                Speak("Unity could not delete " + entry.Name + ".");
                return;
            }

            RefreshProfiles(false);
            Speak(entry.Name + " deleted. " + GetProfileDescription() + ".");
        }

        private void BuildSelectedProfile(bool runAfterBuild)
        {
            BuildProfile profile = RequireCustomProfile();
            if (profile == null)
            {
                return;
            }

            string location = EditorUtility.SaveFilePanel(
                runAfterBuild ? "Choose Build and Run Location" : "Choose Build Location",
                string.Empty,
                profile.name,
                string.Empty);
            Focus();
            if (string.IsNullOrEmpty(location))
            {
                Speak("Build cancelled before it started.");
                return;
            }

            try
            {
                Speak((runAfterBuild ? "Build and run" : "Build") + " started for " + profile.name + ".");
                BuildPlayerWithProfileOptions options = new BuildPlayerWithProfileOptions
                {
                    buildProfile = profile,
                    locationPathName = location,
                    options = runAfterBuild ? BuildOptions.AutoRunPlayer : BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                string result = report == null ? "did not return a report" : report.summary.result.ToString();
                Speak(profile.name + " build " + result + ". See the Unity Console for build details.");
            }
            catch (Exception exception)
            {
                ReportError("build " + profile.name, exception);
            }
        }

        private void ReturnToActions()
        {
            textEdit.End();
            textOperation = TextOperation.None;
            OpenActions();
        }

        private void ReturnToProfiles()
        {
            currentView = BuildProfilesView.Profiles;
            scrollPosition = Vector2.zero;
            Speak("Build profiles list. " + GetProfileDescription() + ".");
            Repaint();
        }

        private ProfileEntry GetSelectedProfile()
        {
            return profiles[selectedProfileIndex];
        }

        private BuildProfile RequireCustomProfile()
        {
            ProfileEntry entry = GetSelectedProfile();
            if (entry.Profile == null)
            {
                Speak("This action requires a custom build profile.");
            }

            return entry.Profile;
        }

        private static void ReportError(string operation, Exception exception)
        {
            PluginErrorLog.Write(nameof(BuildProfilesAccessibilityWindow), exception);
            Speak("Unity could not " + operation + ". See debug.txt and the Unity Console for details.");
        }

        private static void Speak(string message)
        {
            AccessibleSpeech.Speak(message, nameof(BuildProfilesAccessibilityWindow));
        }

        private sealed class ProfileEntry
        {
            internal ProfileEntry(string name, string path, BuildProfile profile, bool isActive)
            {
                Name = name;
                Path = path;
                Profile = profile;
                IsActive = isActive;
            }

            internal string Name { get; private set; }

            internal string Path { get; private set; }

            internal BuildProfile Profile { get; private set; }

            internal bool IsActive { get; private set; }
        }

        private sealed class ProfileAction
        {
            internal ProfileAction(string label, ProfileActionKind kind)
            {
                Label = label;
                Kind = kind;
            }

            internal string Label { get; private set; }

            internal ProfileActionKind Kind { get; private set; }
        }

        private enum BuildProfilesView
        {
            Profiles,
            Actions,
            Scenes,
            Defines
        }

        private enum ProfileActionKind
        {
            Activate,
            EditScenes,
            EditDefines,
            Duplicate,
            Rename,
            Delete,
            Build,
            BuildAndRun
        }

        private enum TextOperation
        {
            None,
            RenameProfile,
            AddDefine,
            EditDefine
        }
    }
}
