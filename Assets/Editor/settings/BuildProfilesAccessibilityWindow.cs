using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAccess
{
    /// <summary>Keyboard and NVDA access to the Windows platform build profile workflow.</summary>
    public sealed class BuildProfilesAccessibilityWindow : EditorWindow
    {
        private const string WindowTitle = "Accessible Build Profiles";
        private const float RowHeight = 20.0f;
        private const string CompressionPreferencePrefix = "UnityAccess.WindowsCompression.";
        private static readonly string[] ArchitectureNames = { "Intel 32-bit", "Intel 64-bit", "ARM 64-bit" };
        private static readonly int[] ArchitectureValues = { 0, 1, 2 };
        private static readonly string[] CompressionNames = { "Default", "LZ4", "LZ4HC" };

        private readonly List<BuildRow> rows = new List<BuildRow>();
        private readonly List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        private readonly List<string> customProfileNames = new List<string>();
        private Vector2 scrollPosition;
        private WindowView view = WindowView.Main;
        private int selectedIndex;
        private int selectedSceneIndex = -1;
        private int optionIndex;
        private bool optionListOpen;

        /// <summary>Opens the accessible Windows Build Profiles window.</summary>
        [MenuItem("Unity Access/Build Profiles", false, 21)]
        public static void Open()
        {
            try
            {
                BuildProfilesAccessibilityWindow window = GetWindow<BuildProfilesAccessibilityWindow>();
                window.titleContent = new GUIContent(WindowTitle);
                window.minSize = new Vector2(640.0f, 380.0f);
                window.Show();
                window.Focus();
                window.OpenMain(true);
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(BuildProfilesAccessibilityWindow), exception);
            }
        }

        private void OnEnable()
        {
            if (view == WindowView.Main) RefreshMainRows();
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
            EditorGUILayout.LabelField(GetViewName(), EditorStyles.boldLabel);
            if (rows.Count == 0) EditorGUILayout.LabelField("No items are available.");
            for (int index = 0; index < rows.Count; index++)
            {
                BuildRow row = rows[index];
                if (AccessibleControls.Button(row.Label + ": " + row.Value, index == selectedIndex))
                {
                    selectedIndex = index;
                    RememberSelectedScene();
                    ActivateSelectedRow();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private string GetInstructions()
        {
            if (optionListOpen) return "Up and Down choose a value; Enter applies; Escape cancels.";
            if (view == WindowView.Scenes) return "Up and Down navigate scenes and actions; Enter activates; Escape returns.";
            return "Up and Down or Tab navigate; Enter activates; Escape returns.";
        }

        private void HandleKeyboard(Event currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown) return;
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

            if (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction) || TryGetTabDirection(currentEvent, out direction))
            {
                selectedIndex = AccessibleList.Move(selectedIndex, direction, rows.Count);
                RememberSelectedScene();
                AccessibleList.KeepVisible(ref scrollPosition, selectedIndex, RowHeight);
                Speak(DescribeSelectedRow() + ".");
            }
            else if (AccessibleKeyboard.IsConfirm(currentEvent)) ActivateSelectedRow();
            else if (AccessibleKeyboard.IsCancel(currentEvent) && view != WindowView.Main) OpenMain(false);
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

        private void OpenMain(bool announce)
        {
            view = WindowView.Main;
            optionListOpen = false;
            RefreshMainRows();
            selectedIndex = AccessibleList.Clamp(selectedIndex, rows.Count);
            scrollPosition = Vector2.zero;
            if (announce)
            {
                Speak("Build Profiles opened. Current active build target is " + EditorUserBuildSettings.activeBuildTarget +
                    ". " + DescribeSelectedRow() + ".");
            }
            else Speak("Windows platform build profile. " + DescribeSelectedRow() + ".");
            Repaint();
        }

        private void RefreshMainRows()
        {
            rows.Clear();
            bool supportInstalled = IsWindowsBuildSupportInstalled();
            bool windowsActive = IsWindowsTarget(EditorUserBuildSettings.activeBuildTarget);
            BuildProfile activeProfile = BuildProfile.GetActiveBuildProfile();
            rows.Add(BuildRow.Action("Target Platform", "Windows" + (supportInstalled ? "; Build Support installed" : "; Build Support missing"),
                BuildAction.AnnounceTarget, !supportInstalled));
            rows.Add(BuildRow.Action("Switch Active Target", windowsActive ? "Windows is active; disabled" : "Switch to Windows",
                BuildAction.SwitchToWindows, !supportInstalled || windowsActive));
            rows.Add(BuildRow.Action("Active Profile", activeProfile == null ? "Windows platform profile" : activeProfile.name + "; custom profile",
                BuildAction.ReturnToPlatformProfile, activeProfile == null));
            rows.Add(BuildRow.Action("Scene List", GetSceneSummary(), BuildAction.OpenScenes));
            rows.Add(BuildRow.Options("Architecture", GetArchitectureName(), ArchitectureNames, BuildAction.SetArchitecture));
            rows.Add(BuildRow.Options("Build and Run On", "Local Machine", new[] { "Local Machine" }, BuildAction.SetRunTarget));
            rows.Add(BuildRow.Boolean("Development Build", EditorUserBuildSettings.development, BuildAction.ToggleDevelopment));
            rows.Add(BuildRow.Options("Compression Method", GetCompressionName(), CompressionNames, BuildAction.SetCompression));
            rows.Add(BuildRow.Action("Build", "button", BuildAction.Build, !supportInstalled));
            rows.Add(BuildRow.Action("Clean Build", "button", BuildAction.CleanBuild, !supportInstalled));
            rows.Add(BuildRow.Action("Build and Run", "Local Machine", BuildAction.BuildAndRun, !supportInstalled));
            rows.Add(BuildRow.Action("Existing Custom Profiles", GetCustomProfileCount() + "; read-only", BuildAction.OpenCustomProfiles));
        }

        private string GetSceneSummary()
        {
            EditorBuildSettingsScene[] globalScenes = EditorBuildSettings.globalScenes;
            int enabledCount = globalScenes.Count(scene => scene.enabled);
            return enabledCount + " included of " + globalScenes.Length;
        }

        private void ActivateSelectedRow()
        {
            if (selectedIndex < 0 || selectedIndex >= rows.Count) { Speak("No item is available."); return; }
            BuildRow row = rows[selectedIndex];
            if (row.Disabled)
            {
                Speak(row.Label + " is disabled. " + row.Value + ".");
                if (row.ActionKind == BuildAction.AnnounceTarget && !IsWindowsBuildSupportInstalled())
                    Speak("Windows Build Support is missing. Install it for this Editor version with Unity Hub, Add modules.");
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

        private void PerformAction(BuildAction action)
        {
            switch (action)
            {
                case BuildAction.AnnounceTarget:
                    Speak("Windows target. Windows Build Support is installed."); break;
                case BuildAction.SwitchToWindows: SwitchToWindows(); break;
                case BuildAction.ReturnToPlatformProfile: ReturnToPlatformProfile(); break;
                case BuildAction.OpenScenes: OpenScenes(); break;
                case BuildAction.ToggleDevelopment:
                    EditorUserBuildSettings.development = !EditorUserBuildSettings.development;
                    RefreshAndAnnounce("Development Build changed to " + (EditorUserBuildSettings.development ? "On, checked" : "Off, not checked") + "."); break;
                case BuildAction.Build: BuildWindows(false, false); break;
                case BuildAction.CleanBuild: BuildWindows(false, true); break;
                case BuildAction.BuildAndRun: BuildWindows(true, false); break;
                case BuildAction.OpenCustomProfiles: OpenCustomProfiles(); break;
                case BuildAction.AddOpenScenes: AddOpenScenes(); break;
                case BuildAction.AddScene: AddScene(); break;
                case BuildAction.ToggleScene: ToggleScene(); break;
                case BuildAction.RemoveScene: RemoveScene(); break;
                case BuildAction.MoveSceneUp: MoveScene(-1); break;
                case BuildAction.MoveSceneDown: MoveScene(1); break;
            }
        }

        private void MoveOption(int direction)
        {
            BuildRow row = rows[selectedIndex];
            optionIndex = AccessibleList.Move(optionIndex, direction, row.OptionNames.Length);
            Speak(row.OptionNames[optionIndex] + ", " + AccessibleList.Position(optionIndex, row.OptionNames.Length) + ".");
            Repaint();
        }

        private void CommitOption()
        {
            BuildRow row = rows[selectedIndex];
            string value = row.OptionNames[optionIndex];
            optionListOpen = false;
            if (row.ActionKind == BuildAction.SetArchitecture)
            {
                int index = Array.IndexOf(ArchitectureNames, value);
                PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, ArchitectureValues[index]);
                RefreshAndAnnounce("Architecture changed to " + value + ".");
            }
            else if (row.ActionKind == BuildAction.SetCompression)
            {
                EditorPrefs.SetInt(GetCompressionPreferenceKey(), Array.IndexOf(CompressionNames, value));
                RefreshAndAnnounce("Compression Method changed to " + value + ".");
            }
            else Speak("Build and Run On remains Local Machine.");
        }

        private void RefreshAndAnnounce(string message)
        {
            int previousIndex = selectedIndex;
            RefreshMainRows();
            selectedIndex = AccessibleList.Clamp(previousIndex, rows.Count);
            Speak(message);
            Repaint();
        }

        private void SwitchToWindows()
        {
            try
            {
                BuildTarget target = GetConfiguredWindowsTarget();
                Speak("Switching active target to Windows. Unity may reimport assets and recompile scripts.");
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Standalone, target);
                if (!switched)
                {
                    Speak("Unity did not switch the active target to Windows.");
                    return;
                }
                EditorApplication.delayCall += delegate
                {
                    if (this == null) return;
                    RefreshMainRows();
                    Speak("Windows is now the active build target.");
                    Repaint();
                };
            }
            catch (Exception exception) { ReportError("switch the active build target", exception); }
        }

        private void ReturnToPlatformProfile()
        {
            try
            {
                BuildProfile.SetActiveBuildProfile(null);
                RefreshAndAnnounce("The Windows platform profile is now active.");
            }
            catch (Exception exception) { ReportError("activate the Windows platform profile", exception); }
        }

        private void OpenScenes()
        {
            scenes.Clear();
            scenes.AddRange(EditorBuildSettings.globalScenes);
            view = WindowView.Scenes;
            selectedIndex = 0;
            selectedSceneIndex = scenes.Count == 0 ? -1 : 0;
            RefreshSceneRows();
            scrollPosition = Vector2.zero;
            Speak("Global Scene List opened. " + DescribeSelectedRow() + ".");
            Repaint();
        }

        private void RefreshSceneRows()
        {
            rows.Clear();
            for (int index = 0; index < scenes.Count; index++)
            {
                EditorBuildSettingsScene scene = scenes[index];
                string name = string.IsNullOrEmpty(scene.path) ? "Missing Scene" : Path.GetFileNameWithoutExtension(scene.path);
                rows.Add(BuildRow.Action(name, (scene.enabled ? "Included, checked" : "Excluded, not checked") + "; " + scene.path,
                    BuildAction.ToggleScene, false, index));
            }
            rows.Add(BuildRow.Action("Add Open Scenes", "button", BuildAction.AddOpenScenes));
            rows.Add(BuildRow.Action("Add Scene", "button", BuildAction.AddScene));
            bool hasScene = scenes.Count > 0;
            rows.Add(BuildRow.Action("Remove Selected Scene", hasScene ? "button" : "disabled", BuildAction.RemoveScene, !hasScene));
            rows.Add(BuildRow.Action("Move Selected Scene Up", selectedSceneIndex > 0 ? "button" : "disabled",
                BuildAction.MoveSceneUp, selectedSceneIndex <= 0));
            rows.Add(BuildRow.Action("Move Selected Scene Down",
                selectedSceneIndex >= 0 && selectedSceneIndex < scenes.Count - 1 ? "button" : "disabled",
                BuildAction.MoveSceneDown, selectedSceneIndex < 0 || selectedSceneIndex >= scenes.Count - 1));
            selectedIndex = AccessibleList.Clamp(selectedIndex, rows.Count);
        }

        private int GetSelectedSceneIndex()
        {
            return selectedIndex >= 0 && selectedIndex < rows.Count ? rows[selectedIndex].SceneIndex : -1;
        }

        private void RememberSelectedScene()
        {
            int sceneIndex = GetSelectedSceneIndex();
            if (sceneIndex >= 0)
            {
                selectedSceneIndex = sceneIndex;
            }
        }

        private void AddOpenScenes()
        {
            int addedCount = 0;
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() || string.IsNullOrEmpty(scene.path) || scenes.Any(item =>
                    string.Equals(item.path, scene.path, StringComparison.OrdinalIgnoreCase))) continue;
                scenes.Add(new EditorBuildSettingsScene(scene.path, true));
                addedCount++;
            }
            SaveScenes();
            RefreshSceneRows();
            Speak(addedCount == 0 ? "No new saved open scenes were available." : addedCount + " open scenes added and included.");
            Repaint();
        }

        private void AddScene()
        {
            Speak("Scene selector opened.");
            ObjectSelector.Open(typeof(SceneAsset), this, null, OnSceneSelected);
        }

        private void OnSceneSelected(UnityEngine.Object selectedObject)
        {
            SceneAsset sceneAsset = selectedObject as SceneAsset;
            if (sceneAsset == null) { Speak("No scene was added."); Focus(); return; }
            string path = AssetDatabase.GetAssetPath(sceneAsset);
            if (scenes.Any(scene => string.Equals(scene.path, path, StringComparison.OrdinalIgnoreCase)))
            {
                Speak(sceneAsset.name + " is already in the Scene List."); Focus(); return;
            }
            scenes.Add(new EditorBuildSettingsScene(path, true));
            selectedSceneIndex = scenes.Count - 1;
            SaveScenes(); RefreshSceneRows(); selectedIndex = selectedSceneIndex;
            Speak(sceneAsset.name + " added and included, " + AccessibleList.Position(selectedIndex, scenes.Count) + ".");
            Focus(); Repaint();
        }

        private void ToggleScene()
        {
            int sceneIndex = selectedSceneIndex;
            if (sceneIndex < 0) { Speak("Select a scene first."); return; }
            EditorBuildSettingsScene scene = scenes[sceneIndex];
            scenes[sceneIndex] = new EditorBuildSettingsScene(scene.path, !scene.enabled);
            SaveScenes(); RefreshSceneRows(); selectedIndex = sceneIndex;
            Speak(Path.GetFileNameWithoutExtension(scene.path) + (scenes[sceneIndex].enabled ? " included, checked." : " excluded, not checked."));
            Repaint();
        }

        private void RemoveScene()
        {
            int sceneIndex = FindActionSceneIndex();
            if (sceneIndex < 0) { Speak("Select a scene before using Remove Selected Scene."); return; }
            string name = Path.GetFileNameWithoutExtension(scenes[sceneIndex].path);
            scenes.RemoveAt(sceneIndex);
            selectedSceneIndex = scenes.Count == 0 ? -1 : Mathf.Clamp(sceneIndex, 0, scenes.Count - 1);
            SaveScenes(); RefreshSceneRows(); selectedIndex = AccessibleList.Clamp(selectedSceneIndex, rows.Count);
            Speak(name + " removed. " + DescribeSelectedRow() + "."); Repaint();
        }

        private void MoveScene(int direction)
        {
            int sceneIndex = FindActionSceneIndex();
            int destination = sceneIndex + direction;
            if (sceneIndex < 0 || destination < 0 || destination >= scenes.Count)
            {
                Speak("The selected scene cannot move " + (direction < 0 ? "up" : "down") + "."); return;
            }
            EditorBuildSettingsScene scene = scenes[sceneIndex];
            selectedSceneIndex = destination;
            scenes.RemoveAt(sceneIndex); scenes.Insert(destination, scene); SaveScenes(); RefreshSceneRows(); selectedIndex = destination;
            Speak(Path.GetFileNameWithoutExtension(scene.path) + " moved " + (direction < 0 ? "up" : "down") + ", " +
                AccessibleList.Position(destination, scenes.Count) + "."); Repaint();
        }

        private int FindActionSceneIndex()
        {
            return selectedSceneIndex;
        }

        private void SaveScenes()
        {
            EditorBuildSettings.globalScenes = scenes.ToArray();
        }

        private void OpenCustomProfiles()
        {
            customProfileNames.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:BuildProfile"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
                if (profile != null) customProfileNames.Add(profile.name + "; " + path);
            }
            customProfileNames.Sort(StringComparer.OrdinalIgnoreCase);
            view = WindowView.CustomProfiles;
            rows.Clear();
            for (int index = 0; index < customProfileNames.Count; index++)
                rows.Add(BuildRow.Action(customProfileNames[index], "read-only; disabled", BuildAction.None, true));
            selectedIndex = AccessibleList.Clamp(0, rows.Count);
            scrollPosition = Vector2.zero;
            Speak("Existing custom profiles, read-only. " + DescribeSelectedRow() + ".");
            Repaint();
        }

        private void BuildWindows(bool runAfterBuild, bool cleanBuild)
        {
            if (!ValidateBuildRequest()) return;
            string location = EditorUtility.SaveFilePanel(
                runAfterBuild ? "Choose Build and Run Location" : cleanBuild ? "Choose Clean Build Location" : "Choose Build Location",
                string.Empty, PlayerSettings.productName + ".exe", "exe");
            Focus();
            if (string.IsNullOrEmpty(location)) { Speak("Build cancelled before it started."); return; }

            try
            {
                BuildOptions options = GetConfiguredBuildOptions(runAfterBuild, cleanBuild);
                BuildPlayerOptions playerOptions = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.globalScenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    locationPathName = location,
                    target = GetConfiguredWindowsTarget(),
                    targetGroup = BuildTargetGroup.Standalone,
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    options = options
                };
                Speak((cleanBuild ? "Clean build" : runAfterBuild ? "Build and Run" : "Build") + " started.");
                BuildReport report = BuildPipeline.BuildPlayer(playerOptions);
                AnnounceBuildReport(report);
            }
            catch (Exception exception) { ReportError("build the Windows player", exception); }
        }

        private bool ValidateBuildRequest()
        {
            if (!IsWindowsBuildSupportInstalled())
            {
                Speak("Windows Build Support is missing. Install it with Unity Hub before building."); return false;
            }
            if (!EditorBuildSettings.globalScenes.Any(scene => scene.enabled && !string.IsNullOrEmpty(scene.path)))
            {
                Speak("Build cannot start because the global Scene List has no included scenes."); return false;
            }
            BuildTarget configuredTarget = GetConfiguredWindowsTarget();
            if (EditorUserBuildSettings.activeBuildTarget != configuredTarget)
            {
                Speak("Switching to the configured Windows target before building.");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Standalone, configuredTarget))
                {
                    Speak("Unity did not switch to the configured Windows target. Build cancelled."); return false;
                }
            }
            if (BuildProfile.GetActiveBuildProfile() != null)
            {
                BuildProfile.SetActiveBuildProfile(null);
                Speak("The Windows platform profile was activated for this build.");
            }
            return true;
        }

        private BuildOptions GetConfiguredBuildOptions(bool runAfterBuild, bool cleanBuild)
        {
            BuildOptions options = BuildOptions.None;
            if (EditorUserBuildSettings.development) options |= BuildOptions.Development;
            if (runAfterBuild) options |= BuildOptions.AutoRunPlayer;
            if (cleanBuild) options |= BuildOptions.CleanBuildCache;
            int compression = GetCompressionIndex();
            if (compression == 1) options |= BuildOptions.CompressWithLz4;
            else if (compression == 2) options |= BuildOptions.CompressWithLz4HC;
            return options;
        }

        private static void AnnounceBuildReport(BuildReport report)
        {
            if (report == null) { Speak("Build failed because Unity did not return a build report. See the Unity Console."); return; }
            BuildSummary summary = report.summary;
            string result = summary.result == BuildResult.Succeeded ? "succeeded" :
                summary.result == BuildResult.Cancelled ? "cancelled" : "failed";
            Speak("Build " + result + ". Output: " + summary.outputPath + ". Duration: " + summary.totalTime +
                ". Size: " + summary.totalSize + " bytes. See the Unity Console for build details.");
        }

        private static bool IsWindowsBuildSupportInstalled()
        {
            return BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        }

        private static bool IsWindowsTarget(BuildTarget target)
        {
            return target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64;
        }

        private static BuildTarget GetConfiguredWindowsTarget()
        {
            return PlayerSettings.GetArchitecture(NamedBuildTarget.Standalone) == 0
                ? BuildTarget.StandaloneWindows : BuildTarget.StandaloneWindows64;
        }

        private static string GetArchitectureName()
        {
            int value = PlayerSettings.GetArchitecture(NamedBuildTarget.Standalone);
            int index = Array.IndexOf(ArchitectureValues, value);
            return index >= 0 ? ArchitectureNames[index] : "Unknown architecture " + value;
        }

        private static string GetCompressionPreferenceKey()
        {
            return CompressionPreferencePrefix + PlayerSettings.productGUID;
        }

        private static int GetCompressionIndex()
        {
            return Mathf.Clamp(EditorPrefs.GetInt(GetCompressionPreferenceKey(), 0), 0, CompressionNames.Length - 1);
        }

        private static string GetCompressionName() { return CompressionNames[GetCompressionIndex()]; }

        private static int GetCustomProfileCount() { return AssetDatabase.FindAssets("t:BuildProfile").Length; }

        private string DescribeSelectedRow()
        {
            if (rows.Count == 0 || selectedIndex < 0) return "No items";
            BuildRow row = rows[selectedIndex];
            return row.Label + ", " + row.Value + (row.Disabled ? ", disabled" : string.Empty) + ", " +
                AccessibleList.Position(selectedIndex, rows.Count);
        }

        private string GetViewName()
        {
            if (view == WindowView.Scenes) return "Global Scene List";
            if (view == WindowView.CustomProfiles) return "Existing Custom Profiles (Read-only)";
            return "Windows Platform Profile";
        }

        private static void ReportError(string operation, Exception exception)
        {
            PluginErrorLog.Write(nameof(BuildProfilesAccessibilityWindow), exception);
            Speak("Unity could not " + operation + ". See debug.txt and the Unity Console for details.");
        }

        private static void Speak(string message) { AccessibleSpeech.Speak(message, nameof(BuildProfilesAccessibilityWindow)); }

        private sealed class BuildRow
        {
            private BuildRow(string label, string value, BuildAction action, string[] options, bool disabled, int sceneIndex)
            { Label = label; Value = value; ActionKind = action; OptionNames = options; Disabled = disabled; SceneIndex = sceneIndex; }
            internal string Label { get; private set; }
            internal string Value { get; private set; }
            internal BuildAction ActionKind { get; private set; }
            internal string[] OptionNames { get; private set; }
            internal bool Disabled { get; private set; }
            internal int SceneIndex { get; private set; }
            internal static BuildRow Action(string label, string value, BuildAction action, bool disabled = false, int sceneIndex = -1)
            { return new BuildRow(label, value, action, Array.Empty<string>(), disabled, sceneIndex); }
            internal static BuildRow Boolean(string label, bool value, BuildAction action)
            { return Action(label, value ? "On, checked" : "Off, not checked", action); }
            internal static BuildRow Options(string label, string value, string[] options, BuildAction action)
            { return new BuildRow(label, value, action, options, false, -1); }
        }

        private enum WindowView { Main, Scenes, CustomProfiles }
        private enum BuildAction
        {
            None, AnnounceTarget, SwitchToWindows, ReturnToPlatformProfile, OpenScenes, SetArchitecture, SetRunTarget,
            ToggleDevelopment, SetCompression, Build, CleanBuild, BuildAndRun, OpenCustomProfiles, AddOpenScenes,
            AddScene, ToggleScene, RemoveScene, MoveSceneUp, MoveSceneDown
        }
    }
}
