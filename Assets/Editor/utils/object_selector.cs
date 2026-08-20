using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>
    /// Restricts which kind of Unity object reference is shown by the selector.
    /// </summary>
    public enum ObjectReferenceScope
    {
        SceneObjects,
        Assets,
        SceneObjectsAndAssets
    }

    /// <summary>
    /// Provides a keyboard-driven, NVDA-accessible picker for Unity object references.
    /// </summary>
    public sealed class ObjectSelector : EditorWindow
    {
        private const float RowHeight = 20.0f;

        private readonly List<SelectorEntry> allEntries = new List<SelectorEntry>();
        private readonly List<SelectorEntry> entries = new List<SelectorEntry>();
        private Type requiredType;
        private ObjectReferenceScope referenceScope;
        private Action<UnityEngine.Object> selectionCallback;
        private EditorWindow returnWindow;
        private int selectedIndex;
        private Vector2 scrollPosition;
        private string searchText = string.Empty;
        private string appliedSearchText = string.Empty;
        private bool focusSearchField = true;

        /// <summary>
        /// Opens a selector whose allowed reference sources are derived from the field owner.
        /// </summary>
        /// <param name="objectType">The required Unity object type.</param>
        /// <param name="owner">The scene object or persistent asset that owns the reference field.</param>
        /// <param name="currentValue">The field's current value, used only as the initial selection.</param>
        /// <param name="onSelected">Called with the chosen object, or null when None is chosen.</param>
        public static void Open(
            Type objectType,
            UnityEngine.Object owner,
            UnityEngine.Object currentValue,
            Action<UnityEngine.Object> onSelected)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            ObjectReferenceScope scope = EditorUtility.IsPersistent(owner)
                ? ObjectReferenceScope.Assets
                : ObjectReferenceScope.SceneObjectsAndAssets;
            OpenInternal(objectType, onSelected, currentValue, scope);
        }

        /// <summary>
        /// Internal implementation used after the public Open method has derived the valid
        /// reference scope from the object that owns the field.
        /// </summary>
        private static void OpenInternal(
            Type objectType,
            Action<UnityEngine.Object> onSelected,
            UnityEngine.Object currentValue,
            ObjectReferenceScope scope)
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

            if (!Enum.IsDefined(typeof(ObjectReferenceScope), scope))
            {
                throw new ArgumentOutOfRangeException(nameof(scope));
            }

            try
            {
                EditorWindow previousWindow = focusedWindow;
                ObjectSelector window = CreateInstance<ObjectSelector>();
                window.titleContent = new GUIContent("Select " + objectType.Name);
                window.minSize = new Vector2(320.0f, 220.0f);
                window.requiredType = objectType;
                window.referenceScope = scope;
                window.selectionCallback = onSelected;
                window.returnWindow = previousWindow;
                window.BuildEntries(currentValue);
                window.ShowAuxWindow();
                window.Focus();
                window.SpeakSearchField("Object selector opened. ");
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
                DrawSearchField();
                ApplySearchFilterIfNeeded();
                EditorGUILayout.LabelField("Up or Down moves, Enter selects, Escape cancels.");
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                if (entries.Count == 0)
                {
                    EditorGUILayout.LabelField("No results.");
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
                PluginErrorLog.Write(nameof(ObjectSelector), exception);
                CloseAndRestoreFocus();
            }
        }

        private void DrawSearchField()
        {
            searchText = AccessibleControls.ToolbarSearch(
                "ObjectSelectorSearch", "Search objects", searchText, focusSearchField);
            focusSearchField = false;
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
            if (entries.Count == 0)
            {
                SpeakSafely("No results.");
                return;
            }

            selectedIndex = AccessibleList.Move(selectedIndex, direction, entries.Count);
            AccessibleList.KeepVisible(ref scrollPosition, selectedIndex, RowHeight);
            SpeakCurrentEntry(string.Empty);
            Repaint();
        }

        private void CommitSelection()
        {
            if (entries.Count == 0)
            {
                SpeakSafely("No results.");
                return;
            }

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
            if (referenceScope != ObjectReferenceScope.Assets)
            {
                AddLoadedObjects(knownInstanceIds);
            }

            if (referenceScope != ObjectReferenceScope.SceneObjects)
            {
                AddAssetObjects(knownInstanceIds);
            }
            entries.Sort(1, entries.Count - 1, SelectorEntryComparer.Instance);
            allEntries.Clear();
            allEntries.AddRange(entries);

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

        private void ApplySearchFilterIfNeeded(bool force = false)
        {
            if (!force && string.Equals(searchText, appliedSearchText, StringComparison.Ordinal))
            {
                return;
            }

            appliedSearchText = searchText;
            entries.Clear();
            foreach (SelectorEntry entry in allEntries)
            {
                if (string.IsNullOrWhiteSpace(searchText) ||
                    entry.DisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    entries.Add(entry);
                }
            }

            selectedIndex = 0;
            scrollPosition = Vector2.zero;
            if (entries.Count == 0)
            {
                SpeakSearchField("No results. ");
            }
            else
            {
                string resultCount = entries.Count + (entries.Count == 1 ? " result. " : " results. ");
                SpeakSearchField(resultCount);
            }

            Repaint();
        }

        private void SpeakSearchField(string prefix = "")
        {
            string value = string.IsNullOrEmpty(searchText) ? "empty" : searchText;
            SpeakSafely(prefix + "Search objects, editable text box, " + value + ".");
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
            if (assetGuids.Length == 0)
            {
                // Some imported asset types, including audio in certain import states,
                // are absent from Unity's type index even though they load correctly.
                assetGuids = AssetDatabase.FindAssets(string.Empty);
            }

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
                    UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    AddAssetIfCompatible(mainAsset, assetPath, knownInstanceIds);

                    UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
                    foreach (UnityEngine.Object subAsset in subAssets)
                    {
                        AddAssetIfCompatible(subAsset, assetPath, knownInstanceIds);
                    }
                }
            }
        }

        private void AddAssetIfCompatible(
            UnityEngine.Object candidate,
            string assetPath,
            HashSet<int> knownInstanceIds)
        {
            if (candidate != null && requiredType.IsAssignableFrom(candidate.GetType()))
            {
                AddEntry(candidate, assetPath, knownInstanceIds);
            }
        }

        private string GetAssetSearchFilter()
        {
            // UnityEngine.Object is the generic fallback for unresolved or broadly typed fields.
            // Searching without a type filter ensures assets such as AudioClip are not omitted.
            if (requiredType == typeof(UnityEngine.Object))
            {
                return string.Empty;
            }

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
            string referenceKind = EditorUtility.IsPersistent(candidate) ? "Asset" : "Scene object";
            string location = source;
            if (source == "Scene" && gameObject != null)
            {
                location = gameObject.scene.name + "/" + GetHierarchyPath(gameObject.transform);
            }

            return candidate.GetType().Name + " - " + referenceKind + " - " + location;
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
            bool isAsset = EditorUtility.IsPersistent(candidate);
            if ((referenceScope == ObjectReferenceScope.Assets && !isAsset) ||
                (referenceScope == ObjectReferenceScope.SceneObjects && isAsset))
            {
                return;
            }

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
                AccessibleList.Position(selectedIndex, entries.Count) + ".");
        }

        private static void SpeakSafely(string message)
        {
            AccessibleSpeech.Speak(message, nameof(ObjectSelector));
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
