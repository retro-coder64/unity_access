using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>Identifies how an addable asset was found by the AssetDatabase.</summary>
    public enum AddObjectAssetType
    {
        Model,
        Prefab
    }

    /// <summary>Provides a keyboard-driven, NVDA-accessible window for adding assets to a scene.</summary>
    public sealed class AddObjectWindow : EditorWindow
    {
        private const float RowHeight = 20.0f;

        private readonly List<AddObjectEntry> entries = new List<AddObjectEntry>();
        private EditorWindow returnWindow;
        private GameObject parentObject;
        private int selectedIndex = -1;
        private Vector2 scrollPosition;

        /// <summary>Opens the window and optionally parents new objects below the supplied scene object.</summary>
        public static void Open(GameObject parent = null)
        {
            try
            {
                EditorWindow previousWindow = focusedWindow;
                AddObjectWindow window = CreateInstance<AddObjectWindow>();
                window.titleContent = new GUIContent("Add Object");
                window.minSize = new Vector2(320.0f, 220.0f);
                window.returnWindow = previousWindow;
                window.parentObject = parent;
                window.BuildEntries();
                window.ShowAuxWindow();
                window.Focus();
                window.SpeakOpeningState();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(AddObjectWindow), exception);
                throw;
            }
        }

        private void OnGUI()
        {
            try
            {
                HandleKeyboard(Event.current);
                EditorGUILayout.LabelField("Add Object", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Up or Down moves, Enter adds, Escape cancels.");

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                if (entries.Count == 0)
                {
                    EditorGUILayout.LabelField("No models or prefabs are available.");
                }
                else
                {
                    for (int index = 0; index < entries.Count; index++)
                    {
                        DrawEntry(index);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(AddObjectWindow), exception);
                SpeakSafely("The Add Object window encountered an error.");
                CloseAndRestoreFocus();
            }
        }

        private void HandleKeyboard(Event currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
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
                AddSelectedObject();
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent))
            {
                SpeakSafely("Add Object cancelled.");
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
            if (entries.Count == 0)
            {
                SpeakSafely("No models or prefabs are available.");
                return;
            }

            selectedIndex = AccessibleList.Move(selectedIndex, direction, entries.Count);
            AccessibleList.KeepVisible(ref scrollPosition, selectedIndex, RowHeight);
            SpeakCurrentEntry();
            Repaint();
        }

        private void AddSelectedObject()
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                SpeakSafely("No object is selected.");
                return;
            }

            AddObjectEntry entry = entries[selectedIndex];
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(entry.Path);
            if (asset == null)
            {
                ReportError("The selected asset can no longer be loaded as a GameObject: " + entry.Path);
                return;
            }

            GameObject resolvedParent = ResolveParent();

            UnityEngine.Object createdObject = PrefabUtility.InstantiatePrefab(asset);
            GameObject createdGameObject = createdObject as GameObject;
            if (createdGameObject == null)
            {
                ReportError("Unity could not instantiate the selected " + entry.Type + " asset.");
                return;
            }

            // Match Unity's built-in object creation placement and parenting behaviour.
            ObjectFactory.PlaceGameObject(createdGameObject, resolvedParent);
            Undo.RegisterCreatedObjectUndo(createdGameObject, "Add " + entry.Name);
            Selection.activeGameObject = createdGameObject;
            EditorGUIUtility.PingObject(createdGameObject);
            SpeakSafely(entry.Name + " added" +
                (resolvedParent == null ? " to the scene." : " under " + resolvedParent.name + "."));
            CloseAndRestoreFocus();
        }

        private GameObject ResolveParent()
        {
            return parentObject;
        }

        private void DrawEntry(int index)
        {
            AddObjectEntry entry = entries[index];
            Rect row = AccessibleList.DrawLabelRow(entry.Name, index == selectedIndex, null, RowHeight);
            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                selectedIndex = index;
                AddSelectedObject();
            }
        }

        private void BuildEntries()
        {
            entries.Clear();
            Dictionary<string, AddObjectEntry> entriesByPath =
                new Dictionary<string, AddObjectEntry>(StringComparer.OrdinalIgnoreCase);

            AddAssets("t:Model", AddObjectAssetType.Model, entriesByPath);
            // Prefab classification wins when Unity returns the same path for both queries.
            AddAssets("t:Prefab", AddObjectAssetType.Prefab, entriesByPath);

            entries.AddRange(entriesByPath.Values);
            entries.Sort(AddObjectEntryComparer.Instance);
            selectedIndex = entries.Count == 0 ? -1 : 0;
        }

        private static void AddAssets(
            string filter,
            AddObjectAssetType type,
            Dictionary<string, AddObjectEntry> entriesByPath)
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null)
                {
                    entriesByPath[path] = new AddObjectEntry(asset.name, type, path);
                }
            }
        }

        private void SpeakOpeningState()
        {
            if (entries.Count == 0)
            {
                SpeakSafely("Add Object opened. No models or prefabs are available. Escape cancels.");
                return;
            }

            SpeakSafely("Add Object opened. " + entries.Count +
                (entries.Count == 1 ? " object. " : " objects. ") +
                "Up or Down moves, Enter adds, Escape cancels. " + CurrentEntryDescription());
        }

        private void SpeakCurrentEntry()
        {
            SpeakSafely(CurrentEntryDescription());
        }

        private string CurrentEntryDescription()
        {
            AddObjectEntry entry = entries[selectedIndex];
            return entry.Name + ", " + AccessibleList.Position(selectedIndex, entries.Count) + ".";
        }

        private void ReportError(string message)
        {
            PluginErrorLog.Write(nameof(AddObjectWindow), new InvalidOperationException(message));
            SpeakSafely(message);
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

        private static void SpeakSafely(string message)
        {
            AccessibleSpeech.Speak(message, nameof(AddObjectWindow));
        }

        private sealed class AddObjectEntry
        {
            internal AddObjectEntry(string name, AddObjectAssetType type, string path)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name;
                Type = type;
                Path = path;
            }

            internal string Name { get; private set; }
            internal AddObjectAssetType Type { get; private set; }
            internal string Path { get; private set; }
        }

        private sealed class AddObjectEntryComparer : IComparer<AddObjectEntry>
        {
            internal static readonly AddObjectEntryComparer Instance = new AddObjectEntryComparer();

            public int Compare(AddObjectEntry left, AddObjectEntry right)
            {
                int nameComparison = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                return nameComparison != 0
                    ? nameComparison
                    : string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
