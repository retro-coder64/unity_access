using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>
    /// Provides a keyboard-driven, NVDA-accessible picker for Unity object references.
    /// </summary>
    public sealed class ObjectSelector : EditorWindow
    {
        private const float RowHeight = 20.0f;

        private readonly List<SelectorEntry> entries = new List<SelectorEntry>();
        private Type requiredType;
        private Action<UnityEngine.Object> selectionCallback;
        private EditorWindow returnWindow;
        private int selectedIndex;
        private Vector2 scrollPosition;

        /// <summary>
        /// Opens a selector containing objects assignable to <paramref name="objectType"/>.
        /// </summary>
        /// <param name="objectType">The required Unity object type.</param>
        /// <param name="onSelected">Called with the chosen object, or null when None is chosen.</param>
        /// <param name="currentValue">The field's current value, used as the initial selection.</param>
        public static void Open(
            Type objectType,
            Action<UnityEngine.Object> onSelected,
            UnityEngine.Object currentValue = null)
        {
            if (objectType == null)
            {
                throw new ArgumentNullException(nameof(objectType));
            }

            if (!typeof(UnityEngine.Object).IsAssignableFrom(objectType))
            {
                throw new ArgumentException("The selector type must derive from UnityEngine.Object.", nameof(objectType));
            }

            if (onSelected == null)
            {
                throw new ArgumentNullException(nameof(onSelected));
            }

            try
            {
                EditorWindow previousWindow = focusedWindow;
                ObjectSelector window = CreateInstance<ObjectSelector>();
                window.titleContent = new GUIContent("Select " + objectType.Name);
                window.minSize = new Vector2(320.0f, 220.0f);
                window.requiredType = objectType;
                window.selectionCallback = onSelected;
                window.returnWindow = previousWindow;
                window.BuildEntries(currentValue);
                window.ShowAuxWindow();
                window.Focus();
                window.SpeakCurrentEntry("Object selector opened. ");
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(ObjectSelector), exception);
                throw;
            }
        }

        private void OnGUI()
        {
            try
            {
                HandleKeyboard(Event.current);

                EditorGUILayout.LabelField("Select " + requiredType.Name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Up or Down moves, Enter selects, Escape cancels.");
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                for (int index = 0; index < entries.Count; index++)
                {
                    DrawEntry(index);
                }

                EditorGUILayout.EndScrollView();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(ObjectSelector), exception);
                CloseAndRestoreFocus();
            }
        }

        private void HandleKeyboard(Event currentEvent)
        {
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (currentEvent.keyCode == KeyCode.UpArrow)
            {
                MoveSelection(-1);
            }
            else if (currentEvent.keyCode == KeyCode.DownArrow)
            {
                MoveSelection(1);
            }
            else if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                CommitSelection();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                SpeakSafely("Object selection cancelled.");
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
            selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, entries.Count - 1);
            scrollPosition.y = Mathf.Max(0.0f, (selectedIndex - 2) * RowHeight);
            SpeakCurrentEntry(string.Empty);
            Repaint();
        }

        private void CommitSelection()
        {
            SelectorEntry selectedEntry = entries[selectedIndex];
            Action<UnityEngine.Object> callback = selectionCallback;
            selectionCallback = null;
            SpeakSafely(selectedEntry.Value == null
                ? "None selected."
                : selectedEntry.DisplayName + " selected.");

            try
            {
                callback(selectedEntry.Value);
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(ObjectSelector), exception);
            }
            finally
            {
                CloseAndRestoreFocus();
            }
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

        private void DrawEntry(int index)
        {
            GUIStyle style = index == selectedIndex ? EditorStyles.selectionRect : EditorStyles.label;
            Rect row = EditorGUILayout.GetControlRect(false, RowHeight);
            if (GUI.Button(row, entries[index].DisplayName, style))
            {
                selectedIndex = index;
                CommitSelection();
            }
        }

        private void BuildEntries(UnityEngine.Object currentValue)
        {
            entries.Clear();
            entries.Add(new SelectorEntry(null, "None"));

            HashSet<int> knownInstanceIds = new HashSet<int>();
            AddLoadedObjects(knownInstanceIds);
            AddAssetObjects(knownInstanceIds);
            entries.Sort(1, entries.Count - 1, SelectorEntryComparer.Instance);

            selectedIndex = 0;
            if (currentValue == null)
            {
                return;
            }

            for (int index = 1; index < entries.Count; index++)
            {
                if (entries[index].Value == currentValue)
                {
                    selectedIndex = index;
                    break;
                }
            }
        }

        private void AddLoadedObjects(HashSet<int> knownInstanceIds)
        {
            UnityEngine.Object[] loadedObjects = Resources.FindObjectsOfTypeAll(requiredType);
            foreach (UnityEngine.Object candidate in loadedObjects)
            {
                if (candidate != null && !EditorUtility.IsPersistent(candidate) && IsVisibleSceneObject(candidate))
                {
                    AddEntry(candidate, "Scene", knownInstanceIds);
                }
            }
        }

        private void AddAssetObjects(HashSet<int> knownInstanceIds)
        {
            string[] assetGuids = AssetDatabase.FindAssets(GetAssetSearchFilter());
            foreach (string assetGuid in assetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (typeof(Component).IsAssignableFrom(requiredType))
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (prefab == null)
                    {
                        continue;
                    }

                    Component[] components = prefab.GetComponentsInChildren(requiredType, true);
                    foreach (Component component in components)
                    {
                        AddEntry(component, assetPath, knownInstanceIds);
                    }
                }
                else
                {
                    UnityEngine.Object[] assetObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    foreach (UnityEngine.Object assetObject in assetObjects)
                    {
                        if (assetObject != null && requiredType.IsAssignableFrom(assetObject.GetType()))
                        {
                            AddEntry(assetObject, assetPath, knownInstanceIds);
                        }
                    }
                }
            }
        }

        private string GetAssetSearchFilter()
        {
            if (typeof(Component).IsAssignableFrom(requiredType) || typeof(GameObject).IsAssignableFrom(requiredType))
            {
                return "t:Prefab";
            }

            return "t:" + requiredType.Name;
        }

        private static bool IsVisibleSceneObject(UnityEngine.Object candidate)
        {
            GameObject gameObject = candidate as GameObject;
            Component component = candidate as Component;
            if (component != null)
            {
                gameObject = component.gameObject;
            }

            return gameObject == null || gameObject.scene.IsValid();
        }

        private static string GetObjectLocation(UnityEngine.Object candidate, string source)
        {
            Component component = candidate as Component;
            GameObject gameObject = component != null ? component.gameObject : candidate as GameObject;
            if (source == "Scene" && gameObject != null)
            {
                return gameObject.scene.name + "/" + GetHierarchyPath(gameObject.transform);
            }

            return source;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private void AddEntry(UnityEngine.Object candidate, string source, HashSet<int> knownInstanceIds)
        {
            if (!knownInstanceIds.Add(candidate.GetInstanceID()))
            {
                return;
            }

            string objectName = string.IsNullOrWhiteSpace(candidate.name) ? "Unnamed" : candidate.name;
            string location = GetObjectLocation(candidate, source);
            entries.Add(new SelectorEntry(candidate, objectName + " — " + location));
        }

        private void SpeakCurrentEntry(string prefix)
        {
            SpeakSafely(prefix + entries[selectedIndex].DisplayName + ", " +
                (selectedIndex + 1) + " of " + entries.Count + ".");
        }

        private static void SpeakSafely(string message)
        {
            try
            {
                NvdaApi.Speak(message);
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(ObjectSelector), exception);
            }
        }

        private sealed class SelectorEntry
        {
            internal SelectorEntry(UnityEngine.Object value, string displayName)
            {
                Value = value;
                DisplayName = displayName;
            }

            internal UnityEngine.Object Value { get; private set; }

            internal string DisplayName { get; private set; }
        }

        private sealed class SelectorEntryComparer : IComparer<SelectorEntry>
        {
            internal static readonly SelectorEntryComparer Instance = new SelectorEntryComparer();

            public int Compare(SelectorEntry left, SelectorEntry right)
            {
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
