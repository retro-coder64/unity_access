using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.SceneTemplate;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAccess
{
    /// <summary>Provides an NVDA-accessible scene template picker and scene creation workflow.</summary>
    public sealed class SceneCreationWindow : EditorWindow
    {
        private const float RowHeight = 20.0f;
        private const string SourceFile = "seen_creater.cs";
        private const string AccessibleShortcutId = "Unity Access/Create Scene";

        private readonly List<SceneCreationEntry> entries = new List<SceneCreationEntry>();
        private EditorWindow returnWindow;
        private int selectedIndex = -1;
        private Vector2 scrollPosition;
        private bool waitingForScene;

        /// <summary>Opens the accessible scene creator from Unity's File menu.</summary>
        [MenuItem("File/New Scene (Accessible)", false, 150)]
        public static void Open()
        {
            try
            {
                EditorWindow previousWindow = focusedWindow;
                SceneCreationWindow window = GetWindow<SceneCreationWindow>(true, "Create Scene", true);
                window.minSize = new Vector2(360.0f, 240.0f);
                window.returnWindow = previousWindow == window ? null : previousWindow;
                window.BuildEntries();
                window.Focus();
                window.SpeakOpeningState();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(SourceFile, exception);
                throw;
            }
        }

        /// <summary>Receives Ctrl+N after the conflicting built-in binding has been removed.</summary>
        [Shortcut(AccessibleShortcutId)]
        private static void OpenFromShortcut()
        {
            Open();
        }

        private void OnDisable()
        {
            StopWaitingForScene();
        }

        private void OnGUI()
        {
            try
            {
                HandleKeyboard(Event.current);
                EditorGUILayout.LabelField("Create Scene", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Up or Down moves, Enter creates, Escape cancels.");

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                for (int index = 0; index < entries.Count; index++)
                {
                    DrawEntry(index);
                }

                EditorGUILayout.EndScrollView();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(SourceFile, exception);
                Speak("The scene creator encountered an error.");
                CloseAndRestoreFocus();
            }
        }

        private void HandleKeyboard(Event currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown || waitingForScene)
            {
                return;
            }

            int direction;
            if (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                MoveSelection(direction);
            }
            else if (AccessibleKeyboard.IsConfirm(currentEvent))
            {
                CreateSelectedScene();
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent))
            {
                Speak("Scene creation cancelled.");
                CloseAndRestoreFocus();
            }
            else
            {
                return;
            }

            currentEvent.Use();
        }

        private void MoveSelection(int direction)
        {
            selectedIndex = AccessibleList.Move(selectedIndex, direction, entries.Count);
            AccessibleList.KeepVisible(ref scrollPosition, selectedIndex, RowHeight);
            Speak(CurrentEntryDescription());
            Repaint();
        }

        private void CreateSelectedScene()
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                Speak("No scene template is selected.");
                return;
            }

            // Unity presents its Save/Don't Save/Cancel prompt when the active scene is dirty.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Speak("Scene creation cancelled.");
                return;
            }

            SceneCreationEntry entry = entries[selectedIndex];
            waitingForScene = true;
            EditorSceneManager.sceneOpened += OnSceneOpened;

            try
            {
                if (entry.Template != null)
                {
                    SceneTemplateService.Instantiate(entry.Template, false);
                }
                else
                {
                    EditorSceneManager.NewScene(entry.Setup, NewSceneMode.Single);
                }

                // Some Unity versions do not raise sceneOpened for a newly instantiated unsaved scene.
                if (waitingForScene)
                {
                    Scene activeScene = EditorSceneManager.GetActiveScene();
                    CompleteSceneCreation(activeScene);
                }
            }
            catch (Exception exception)
            {
                StopWaitingForScene();
                PluginErrorLog.Write(SourceFile, exception);
                Speak("Unity could not create the selected scene.");
            }
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (waitingForScene)
            {
                CompleteSceneCreation(scene);
            }
        }

        private void CompleteSceneCreation(Scene scene)
        {
            StopWaitingForScene();
            string sceneName = string.IsNullOrWhiteSpace(scene.name) ? "Untitled" : scene.name;
            Speak(sceneName + " scene created and opened.");
            CloseAndRestoreFocus();
        }

        private void StopWaitingForScene()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            waitingForScene = false;
        }

        private void DrawEntry(int index)
        {
            SceneCreationEntry entry = entries[index];
            Rect row = AccessibleList.DrawLabelRow(entry.Name, index == selectedIndex, null, RowHeight);
            if (GUI.Button(row, GUIContent.none, GUIStyle.none) && !waitingForScene)
            {
                selectedIndex = index;
                CreateSelectedScene();
            }
        }

        private void BuildEntries()
        {
            entries.Clear();

            // Always expose Unity's two built-in scene setups as dependable fallbacks.
            entries.Add(new SceneCreationEntry("Basic Scene", NewSceneSetup.DefaultGameObjects));
            entries.Add(new SceneCreationEntry("Empty Scene", NewSceneSetup.EmptyScene));

            string[] templateGuids = AssetDatabase.FindAssets("t:SceneTemplateAsset");
            foreach (string guid in templateGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SceneTemplateAsset template = AssetDatabase.LoadAssetAtPath<SceneTemplateAsset>(path);
                if (template != null)
                {
                    string displayName = string.IsNullOrWhiteSpace(template.templateName)
                        ? template.name
                        : template.templateName;
                    entries.Add(new SceneCreationEntry(displayName, template, path));
                }
            }

            entries.Sort(SceneCreationEntryComparer.Instance);
            selectedIndex = entries.Count == 0 ? -1 : 0;
            scrollPosition = Vector2.zero;
        }

        private void SpeakOpeningState()
        {
            Speak("Create Scene opened. " + entries.Count +
                (entries.Count == 1 ? " option. " : " options. ") +
                "Up or Down moves, Enter creates, Escape cancels. " + CurrentEntryDescription());
        }

        private string CurrentEntryDescription()
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                return "No scene templates are available.";
            }

            return entries[selectedIndex].Name + ", " + AccessibleList.Position(selectedIndex, entries.Count) + ".";
        }

        private void CloseAndRestoreFocus()
        {
            EditorWindow previousWindow = returnWindow;
            returnWindow = null;
            Close();
            if (previousWindow != null)
            {
                EditorApplication.delayCall += previousWindow.Focus;
            }
        }

        private static void Speak(string message)
        {
            AccessibleSpeech.Speak(message, SourceFile);
        }

        private sealed class SceneCreationEntry
        {
            internal SceneCreationEntry(string name, NewSceneSetup setup)
            {
                Name = name;
                Setup = setup;
                Path = string.Empty;
            }

            internal SceneCreationEntry(string name, SceneTemplateAsset template, string path)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Unnamed Template" : name;
                Template = template;
                Path = path ?? string.Empty;
                Setup = NewSceneSetup.EmptyScene;
            }

            internal string Name { get; private set; }
            internal string Path { get; private set; }
            internal NewSceneSetup Setup { get; private set; }
            internal SceneTemplateAsset Template { get; private set; }
        }

        private sealed class SceneCreationEntryComparer : IComparer<SceneCreationEntry>
        {
            internal static readonly SceneCreationEntryComparer Instance = new SceneCreationEntryComparer();

            public int Compare(SceneCreationEntry left, SceneCreationEntry right)
            {
                int nameComparison = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                return nameComparison != 0
                    ? nameComparison
                    : string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
