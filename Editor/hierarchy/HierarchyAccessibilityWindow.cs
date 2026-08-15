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
        private readonly List<GameObject> sceneObjects = new List<GameObject>();
        private Vector2 scrollPosition;
        private int selectedIndex = -1;

        /// <summary>
        /// Opens the accessible hierarchy. The underscore makes I a Unity menu shortcut.
        /// </summary>
        [MenuItem("Unity Access/Hierarchy _i", false, 1)]
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
                EditorGUILayout.LabelField("Scene objects", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Use Up and Down to navigate. Press Enter to open the inspector.");

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
            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            if (index == selectedIndex)
            {
                EditorGUI.DrawRect(row, new Color(0.24f, 0.49f, 0.90f, 0.45f));
            }

            EditorGUI.LabelField(row, GetDisplayName(sceneObject), EditorStyles.label);
        }

        private void HandleKeyboardInput(Event currentEvent)
        {
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (currentEvent.keyCode == KeyCode.UpArrow)
            {
                MoveSelection(-1);
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.DownArrow)
            {
                MoveSelection(1);
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                OpenSelectedObject();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.F5)
            {
                RefreshHierarchy(true);
                currentEvent.Use();
            }
        }

        private void MoveSelection(int direction)
        {
            if (sceneObjects.Count == 0)
            {
                SpeakSafely(EmptyHierarchyMessage);
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, sceneObjects.Count - 1);
            GameObject selectedObject = sceneObjects[selectedIndex];
            Selection.activeGameObject = selectedObject;
            EditorGUIUtility.PingObject(selectedObject);
            SpeakSafely(GetSpokenName(selectedObject) + ", " + (selectedIndex + 1) + " of " + sceneObjects.Count + ".");
            Repaint();
        }

        private void OpenSelectedObject()
        {
            if (selectedIndex < 0 || selectedIndex >= sceneObjects.Count)
            {
                SpeakSafely("Select a scene object first.");
                return;
            }

            Selection.activeGameObject = sceneObjects[selectedIndex];
            SpeakSafely("Inspector opened for " + GetSpokenName(sceneObjects[selectedIndex]) + ".");
            EditorUtility.DisplayDialog("Unity Access", "inspector opened", "OK");
        }

        private void HandleHierarchyChanged()
        {
            RefreshHierarchy(false);
            Repaint();
        }

        private void RefreshHierarchy(bool announce)
        {
            int selectedInstanceId = selectedIndex >= 0 && selectedIndex < sceneObjects.Count
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
            if (selectedIndex < 0 && sceneObjects.Count > 0)
            {
                selectedIndex = 0;
            }

            if (announce)
            {
                string message = sceneObjects.Count == 0
                    ? EmptyHierarchyMessage
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
            for (int index = 0; index < sceneObjects.Count; index++)
            {
                if (sceneObjects[index].GetInstanceID() == instanceId)
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
            try
            {
                NvdaApi.Speak(message);
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(HierarchyAccessibilityWindow), exception);
            }
        }
    }
}
