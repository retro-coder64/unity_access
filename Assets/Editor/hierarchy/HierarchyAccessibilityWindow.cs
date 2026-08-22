using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAccess
{
    /// <summary>
    /// Provides a keyboard-driven, NVDA-accessible view of loaded scene objects.
    /// </summary>
    public sealed class HierarchyAccessibilityWindow : EditorWindow
    {
        private const string WindowTitle = "Accessible Hierarchy";
        private const string EmptyHierarchyMessage = "The hierarchy contains no scene objects.";
        private const int AddObjectControlIndex = -1;
        private readonly List<GameObject> sceneObjects = new List<GameObject>();
        private Vector2 scrollPosition;
        private int selectedIndex = AddObjectControlIndex;
        private bool showingOptions;
        private GameObject optionsObject;
        private int selectedOptionIndex;
        private static readonly string[] Options =
        {
            "Delete", "Duplicate", "Create Prefab", "Set Parent", "Unparent", "Add Child"
        };

        /// <summary>
        /// Opens the accessible hierarchy. The underscore makes H a Unity menu shortcut.
        /// </summary>
        [MenuItem("Unity Access/Hierarchy _h", false, 1)]
        public static void Open()
        {
            try
            {
                HierarchyAccessibilityWindow window = GetWindow<HierarchyAccessibilityWindow>();
                window.titleContent = new GUIContent(WindowTitle);
                window.minSize = new Vector2(320.0f, 180.0f);
                window.Show();
                window.Focus();
                if (!NvdaApi.IsRunning)
                {
                    throw new InvalidOperationException("NVDA is not running or rejected the controller connection.");
                }

                window.RefreshHierarchy(true);
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(HierarchyAccessibilityWindow), exception);
                EditorUtility.DisplayDialog(
                    "Unity Access NVDA error",
                    "The hierarchy could not communicate with NVDA. See Assets/Editor/debug.txt for details.",
                    "OK");
            }
        }

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
            RefreshHierarchy(false);
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
        }

        private void OnGUI()
        {
            try
            {
                HandleKeyboardInput(Event.current);
                if (showingOptions)
                {
                    DrawOptionsMenu();
                    return;
                }
                EditorGUILayout.LabelField("Scene objects", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Use Up and Down to navigate. Enter selects; Backspace removes; A adds an object.");

                // Keep object creation available to mouse users while the A shortcut provides
                // a dependable, NVDA-friendly way to activate the same control.
                if (AccessibleControls.Button(
                    "Add object, button. Press Enter.",
                    selectedIndex == AddObjectControlIndex))
                {
                    selectedIndex = AddObjectControlIndex;
                    OpenAddObjectWindow();
                }

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                for (int index = 0; index < sceneObjects.Count; index++)
                {
                    DrawObjectRow(index);
                }

                EditorGUILayout.EndScrollView();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(HierarchyAccessibilityWindow), exception);
            }
        }

        private void DrawObjectRow(int index)
        {
            GameObject sceneObject = sceneObjects[index];
            // Unity objects compare equal to null immediately after external destruction.
            string displayName = sceneObject == null ? "Object removed" : GetDisplayName(sceneObject);
            AccessibleList.DrawLabelRow(displayName, index == selectedIndex);
        }

        private void HandleKeyboardInput(Event currentEvent)
        {
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (showingOptions)
            {
                HandleOptionsKeyboard(currentEvent);
                return;
            }

            if (currentEvent.keyCode == KeyCode.UpArrow)
            {
                MoveSelection(-1);
                currentEvent.Use();
            }
            else if (currentEvent.shift && currentEvent.keyCode == KeyCode.F10)
            {
                OpenOptionsMenu();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.DownArrow)
            {
                MoveSelection(1);
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                ActivateSelection();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Backspace)
            {
                RemoveSelectedObject();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.F5)
            {
                RefreshHierarchy(true);
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.A)
            {
                OpenAddObjectWindow();
                currentEvent.Use();
            }
        }

        private void OpenOptionsMenu()
        {
            if (selectedIndex < 0 || selectedIndex >= sceneObjects.Count || sceneObjects[selectedIndex] == null)
            {
                SpeakSafely("Select a scene object before opening its options.");
                return;
            }

            optionsObject = sceneObjects[selectedIndex];
            selectedOptionIndex = 0;
            showingOptions = true;
            SpeakSafely(optionsObject.name + " options. " + Options[0] + ", 1 of " + Options.Length + ".");
            Repaint();
        }

        private void DrawOptionsMenu()
        {
            EditorGUILayout.LabelField("Options for " + optionsObject.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Up and Down navigate. Enter activates. Escape returns.");
            for (int index = 0; index < Options.Length; index++)
            {
                if (AccessibleControls.Button(Options[index], index == selectedOptionIndex))
                {
                    selectedOptionIndex = index;
                    ActivateOption();
                    return;
                }
            }
        }

        private void HandleOptionsKeyboard(Event currentEvent)
        {
            if (currentEvent.keyCode == KeyCode.UpArrow || currentEvent.keyCode == KeyCode.DownArrow)
            {
                int direction = currentEvent.keyCode == KeyCode.UpArrow ? -1 : 1;
                selectedOptionIndex = (selectedOptionIndex + direction + Options.Length) % Options.Length;
                SpeakSafely(Options[selectedOptionIndex] + ", " + (selectedOptionIndex + 1) + " of " + Options.Length + ".");
                Repaint();
            }
            else if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                ActivateOption();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                showingOptions = false;
                optionsObject = null;
                SpeakSafely("Options closed.");
                Repaint();
            }
            else
            {
                return;
            }

            currentEvent.Use();
        }

        private void ActivateOption()
        {
            GameObject target = optionsObject;
            if (target == null)
            {
                CloseOptions("The selected object no longer exists.");
                return;
            }

            switch (selectedOptionIndex)
            {
                case 0: DeleteObject(target); break;
                case 1: DuplicateObject(target); break;
                case 2: CreatePrefab(target); break;
                case 3: SetParent(target); break;
                case 4: UnparentObject(target); break;
                case 5: AddChild(target); break;
            }
        }

        private void DeleteObject(GameObject target)
        {
            if (EditorUtility.DisplayDialog("Delete object", "Delete " + target.name + "?", "Delete", "Cancel"))
            {
                Undo.DestroyObjectImmediate(target);
                CloseOptions(target.name + " deleted.");
            }
            else
            {
                SpeakSafely("Delete cancelled. Options menu.");
            }
        }

        private void DuplicateObject(GameObject target)
        {
            GameObject copy = Instantiate(target, target.transform.parent);
            copy.name = target.name + " Copy";
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate " + target.name);
            Selection.activeGameObject = copy;
            RefreshHierarchy(false);
            CloseOptions(copy.name + " duplicated.");
        }

        private void CreatePrefab(GameObject target)
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Prefab", target.name, "prefab", "Choose a prefab location.");
            if (!string.IsNullOrEmpty(path))
            {
                PrefabUtility.SaveAsPrefabAsset(target, path);
                CloseOptions("Prefab created for " + target.name + ".");
            }
        }

        private void SetParent(GameObject target)
        {
            ObjectSelector.OpenSceneGameObject(target, selected =>
            {
                if (selected == null || selected == target || ((GameObject)selected).transform.IsChildOf(target.transform))
                {
                    SpeakSafely("Invalid parent. Choose another scene object.");
                    return;
                }

                Undo.SetTransformParent(target.transform, ((GameObject)selected).transform, "Set Parent");
                CloseOptions(target.name + " parent set to " + selected.name + ".");
            });
        }

        private void UnparentObject(GameObject target)
        {
            if (target.transform.parent != null)
            {
                Undo.SetTransformParent(target.transform, null, "Unparent");
                CloseOptions(target.name + " unparented.");
            }
            else
            {
                CloseOptions(target.name + " is already a root object.");
            }
        }

        private void AddChild(GameObject target)
        {
            showingOptions = false;
            AddObjectWindow.Open(target);
        }

        private void CloseOptions(string message)
        {
            showingOptions = false;
            optionsObject = null;
            RefreshHierarchy(false);
            SpeakSafely(message);
            Repaint();
        }

        /// <summary>
        /// Opens the shared Add Object utility with no parent, as required for hierarchy creation.
        /// </summary>
        private static void OpenAddObjectWindow()
        {
            AddObjectWindow.Open();
        }

        private void MoveSelection(int direction)
        {
            if (sceneObjects.Count == 0)
            {
                selectedIndex = AddObjectControlIndex;
                SpeakSafely("Add object, button. Press Enter. " + EmptyHierarchyMessage);
                return;
            }

            selectedIndex = Mathf.Clamp(
                selectedIndex + direction,
                AddObjectControlIndex,
                sceneObjects.Count - 1);
            if (selectedIndex == AddObjectControlIndex)
            {
                SpeakSafely("Add object, button. Press Enter.");
                Repaint();
                return;
            }

            GameObject selectedObject = sceneObjects[selectedIndex];
            if (selectedObject == null)
            {
                RefreshHierarchy(false);
                MoveSelection(0);
                return;
            }

            // Navigation changes only the hierarchy cursor; Enter commits shared selection.
            EditorGUIUtility.PingObject(selectedObject);
            SpeakSafely(GetSpokenName(selectedObject) + ", " +
                AccessibleList.Position(selectedIndex, sceneObjects.Count) + ".");
            Repaint();
        }

        private void ActivateSelection()
        {
            if (selectedIndex == AddObjectControlIndex)
            {
                OpenAddObjectWindow();
                return;
            }

            OpenSelectedObject();
        }

        private void OpenSelectedObject()
        {
            if (selectedIndex < 0 || selectedIndex >= sceneObjects.Count)
            {
                SpeakSafely("Select a scene object first.");
                return;
            }

            // Publish shared state so the hierarchy never calls the inspector directly.
            GameObject selectedObject = sceneObjects[selectedIndex];
            Selection.activeGameObject = selectedObject;
            SpeakSafely(GetSpokenName(selectedObject) + " selected for the inspector.");
            SharedSelection.Select(selectedObject);
        }

        /// <summary>
        /// Removes the object at the hierarchy cursor through Unity's Undo system.
        /// </summary>
        private void RemoveSelectedObject()
        {
            if (selectedIndex < 0 || selectedIndex >= sceneObjects.Count)
            {
                SpeakSafely("Select a scene object first.");
                return;
            }

            GameObject selectedObject = sceneObjects[selectedIndex];
            if (selectedObject == null)
            {
                RefreshHierarchy(false);
                SpeakSafely("That scene object no longer exists.");
                Repaint();
                return;
            }

            string removedName = selectedObject.name;
            Undo.DestroyObjectImmediate(selectedObject);
            RefreshHierarchy(false);

            string message = sceneObjects.Count == 0
                ? removedName + " removed. " + EmptyHierarchyMessage
                : removedName + " removed. " + GetSpokenName(sceneObjects[selectedIndex]) + ", " + (selectedIndex + 1) + " of " + sceneObjects.Count + ".";
            SpeakSafely(message);
            Repaint();
        }

        private void HandleHierarchyChanged()
        {
            RefreshHierarchy(false);
            Repaint();
        }

        private void RefreshHierarchy(bool announce)
        {
            int previousIndex = selectedIndex;
            int selectedInstanceId = selectedIndex >= 0 && selectedIndex < sceneObjects.Count && sceneObjects[selectedIndex] != null
                ? sceneObjects[selectedIndex].GetInstanceID()
                : 0;

            sceneObjects.Clear();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] rootObjects = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
                {
                    AddObjectAndChildren(rootObjects[rootIndex].transform);
                }
            }

            selectedIndex = FindObjectIndex(selectedInstanceId);
            if (selectedIndex < 0 && sceneObjects.Count > 0 && previousIndex != AddObjectControlIndex)
            {
                selectedIndex = Mathf.Clamp(previousIndex, 0, sceneObjects.Count - 1);
            }

            if (announce)
            {
                string message = sceneObjects.Count == 0
                    ? "Hierarchy opened. Add object, button. Press Enter. " + EmptyHierarchyMessage
                    : selectedIndex == AddObjectControlIndex
                        ? "Hierarchy opened. " + sceneObjects.Count + " scene objects. Add object, button. Press Enter."
                        : "Hierarchy opened. " + sceneObjects.Count + " scene objects. " + GetSpokenName(sceneObjects[selectedIndex]) + ", " + (selectedIndex + 1) + " of " + sceneObjects.Count + ".";
                SpeakSafely(message);
            }
        }

        private void AddObjectAndChildren(Transform objectTransform)
        {
            sceneObjects.Add(objectTransform.gameObject);
            for (int childIndex = 0; childIndex < objectTransform.childCount; childIndex++)
            {
                AddObjectAndChildren(objectTransform.GetChild(childIndex));
            }
        }

        private int FindObjectIndex(int instanceId)
        {
            if (instanceId == 0)
            {
                return -1;
            }

            for (int index = 0; index < sceneObjects.Count; index++)
            {
                if (sceneObjects[index] != null && sceneObjects[index].GetInstanceID() == instanceId)
                {
                    return index;
                }
            }

            return -1;
        }

        private static string GetDisplayName(GameObject sceneObject)
        {
            return new string(' ', GetHierarchyDepth(sceneObject) * 2) + sceneObject.name;
        }

        private static string GetSpokenName(GameObject sceneObject)
        {
            int depth = GetHierarchyDepth(sceneObject);
            return depth == 0 ? sceneObject.name : sceneObject.name + ", child level " + depth;
        }

        private static int GetHierarchyDepth(GameObject sceneObject)
        {
            int depth = 0;
            Transform parent = sceneObject.transform.parent;
            while (parent != null)
            {
                depth++;
                parent = parent.parent;
            }

            return depth;
        }

        private static void SpeakSafely(string message)
        {
            AccessibleSpeech.Speak(message, nameof(HierarchyAccessibilityWindow));
        }
    }

    /// <summary>
    /// Restores the accessible hierarchy when another window publishes a return request.
    /// </summary>
    [InitializeOnLoad]
    internal static class HierarchyReturnObserver
    {
        static HierarchyReturnObserver()
        {
            SharedSelection.ReturnToHierarchyRequested += HierarchyAccessibilityWindow.Open;
        }
    }
}
