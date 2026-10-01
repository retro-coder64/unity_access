using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>Creates and edits animator controllers with explicit keyboard focus and NVDA speech.</summary>
    public sealed class AnimatorAccessibilityWindow : EditorWindow
    {
        private const string SourceFile = "AnimatorAccessibilityWindow.cs";
        private const float RowHeight = 21.0f;
        private const string SearchControlName = "AnimatorClipSearch";
        private const string LayerNameControlName = "AnimatorLayerName";

        private readonly List<ChildAnimatorState> animationStates = new List<ChildAnimatorState>();
        private readonly List<ClipEntry> clips = new List<ClipEntry>();
        private AnimatorController controller;
        private View view = View.Start;
        private int selectedIndex;
        private int selectedLayerIndex;
        private Vector2 scrollPosition;
        private string searchText = string.Empty;
        private bool focusSearch;
        private readonly AccessibleTextEdit layerName = new AccessibleTextEdit();
        private bool focusLayerName;

        [MenuItem("Unity Access/Animator", false, 11)]
        public static void Open()
        {
            try
            {
                AnimatorAccessibilityWindow window = GetWindow<AnimatorAccessibilityWindow>("Accessible Animator");
                window.minSize = new Vector2(440.0f, 300.0f);
                window.controller = null;
                window.view = View.Start;
                window.selectedIndex = 0;
                window.selectedLayerIndex = 0;
                window.Show();
                window.Focus();
                Speak("Animator opened. " + window.DescribeSelection());
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(SourceFile, exception);
            }
        }

        private void OnEnable()
        {
            // Project and Undo callbacks keep an open controller current when another editor changes it.
            EditorApplication.projectChanged += Refresh;
            Undo.undoRedoPerformed += Refresh;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= Refresh;
            Undo.undoRedoPerformed -= Refresh;
        }

        private void OnInspectorUpdate()
        {
            if (view == View.Controller) Repaint();
        }

        private void Refresh()
        {
            if (this != null) Repaint();
        }

        private void OnGUI()
        {
            try
            {
                // Read the state machine on every draw so changes from the Animator window appear immediately.
                if (view == View.Controller) RefreshAnimationStates();
                HandleKeyboard(Event.current);
                if (view == View.Start) DrawStart();
                else if (view == View.Controller) DrawController();
                else if (view == View.ClipPicker) DrawClipPicker();
                else DrawAddLayer();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(SourceFile, exception);
                Speak("Animator error. See Editor debug.txt.");
            }
        }

        private void DrawStart()
        {
            EditorGUILayout.LabelField("Animator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Up or Down moves; Enter selects; Escape closes.");
            if (AccessibleControls.Button("Create animator controller", selectedIndex == 0)) CreateController();
            if (AccessibleControls.Button("Open animator controller", selectedIndex == 1)) OpenController();
        }

        private void DrawController()
        {
            EditorGUILayout.LabelField(controller == null ? "Animator controller unavailable" : controller.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Up or Down moves; Enter selects; Backspace removes the highlighted layer or animation; Escape closes.");
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            AnimatorControllerLayer[] layers = controller == null ? null : controller.layers;
            int layerCount = layers == null ? 0 : layers.Length;
            // Layer rows come first, followed by three actions and the selected layer's clips.
            EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
            for (int index = 0; index < layerCount; index++)
            {
                string label = "Layer: " + layers[index].name + (index == selectedLayerIndex ? ", active" : string.Empty);
                if (AccessibleControls.Button(label, selectedIndex == index)) SelectLayer(index);
            }
            if (AccessibleControls.Button("Add animation", selectedIndex == layerCount)) BeginAddAnimation();
            if (AccessibleControls.Button("Add layer", selectedIndex == layerCount + 1)) BeginAddLayer();
            if (AccessibleControls.Button("Save changes", selectedIndex == layerCount + 2)) SaveController();
            EditorGUILayout.LabelField("Animations in " + (layerCount > 0 ? layers[selectedLayerIndex].name : "selected layer"), EditorStyles.boldLabel);
            if (animationStates.Count == 0) EditorGUILayout.LabelField("No animations in this layer.");
            for (int index = 0; index < animationStates.Count; index++)
            {
                AnimatorState state = animationStates[index].state;
                AnimationClip clip = state == null ? null : state.motion as AnimationClip;
                if (clip != null && AccessibleControls.Button(
                    "Animation: " + clip.name + ", state: " + state.name, selectedIndex == layerCount + 3 + index))
                {
                    selectedIndex = layerCount + 3 + index;
                    Speak(DescribeSelection());
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawAddLayer()
        {
            EditorGUILayout.LabelField("Add layer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Type a layer name. Enter creates the layer; Escape returns.");
            Rect nameRow = EditorGUILayout.GetControlRect();
            AccessibleEditorStyles.DrawSelection(nameRow, selectedIndex == 0);
            layerName.Value = AccessibleControls.TextBox(nameRow, LayerNameControlName, "Layer name", layerName.Value, focusLayerName);
            focusLayerName = false;
            if (AccessibleControls.Button("Create layer", selectedIndex == 1)) AddLayer();
            if (AccessibleControls.Button("Cancel", selectedIndex == 2)) ReturnToController("Layer creation cancelled.");
        }

        private void DrawClipPicker()
        {
            EditorGUILayout.LabelField("Add animation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Search animation clips in .anim files and imported models. Up or Down moves; Enter selects; Escape returns.");
            string updatedSearch = AccessibleControls.ToolbarSearch(
                SearchControlName, "Search animation clips", searchText, focusSearch);
            focusSearch = false;
            if (!string.Equals(searchText, updatedSearch, StringComparison.Ordinal))
            {
                searchText = updatedSearch;
                BuildClips();
                selectedIndex = 0;
                Speak(clips.Count + " matching animation clips. " + DescribeSelection());
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (clips.Count == 0) EditorGUILayout.LabelField("No animation clips found.");
            for (int index = 0; index < clips.Count; index++)
            {
                ClipEntry entry = clips[index];
                if (AccessibleControls.Button(entry.Name + ", " + entry.Path, selectedIndex == index + 1))
                {
                    selectedIndex = index + 1;
                    AddAnimation(entry);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void HandleKeyboard(Event currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown) return;
            int direction;
            if (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                int count = view == View.Start ? 2 : view == View.Controller ? ControllerItemCount :
                    view == View.ClipPicker ? clips.Count + 1 : 3;
                selectedIndex = AccessibleList.Move(selectedIndex, direction, count);
                AccessibleList.KeepVisible(ref scrollPosition, selectedIndex, RowHeight);
                if (view == View.ClipPicker)
                    GUI.FocusControl(selectedIndex == 0 ? SearchControlName : string.Empty);
                else if (view == View.AddLayer)
                    GUI.FocusControl(selectedIndex == 0 ? LayerNameControlName : string.Empty);
                Speak(DescribeSelection());
            }
            else if (AccessibleKeyboard.IsConfirm(currentEvent))
            {
                ActivateSelection();
            }
            else if (currentEvent.keyCode == KeyCode.Backspace && view == View.Controller &&
                selectedIndex < LayerCount)
            {
                RemoveSelectedLayer();
            }
            else if (currentEvent.keyCode == KeyCode.Backspace && view == View.Controller &&
                selectedIndex >= LayerCount + 3)
            {
                RemoveSelectedAnimation();
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent))
            {
                if (view == View.ClipPicker) ReturnToController("Animation selection cancelled.");
                else if (view == View.AddLayer) ReturnToController("Layer creation cancelled.");
                else Close();
            }
            else return;
            currentEvent.Use();
        }

        private void ActivateSelection()
        {
            if (view == View.Start)
            {
                if (selectedIndex == 0) CreateController();
                else OpenController();
            }
            else if (view == View.Controller)
            {
                if (selectedIndex < LayerCount) SelectLayer(selectedIndex);
                else if (selectedIndex == LayerCount) BeginAddAnimation();
                else if (selectedIndex == LayerCount + 1) BeginAddLayer();
                else if (selectedIndex == LayerCount + 2) SaveController();
                else Speak(DescribeSelection());
            }
            else if (view == View.AddLayer)
            {
                if (selectedIndex == 2) ReturnToController("Layer creation cancelled.");
                else AddLayer();
            }
            else if (selectedIndex == 0)
            {
                if (clips.Count > 0)
                {
                    selectedIndex = 1;
                    Speak(DescribeSelection());
                }
                else Speak("No matching animation clips.");
            }
            else AddAnimation(clips[selectedIndex - 1]);
        }

        private void CreateController()
        {
            try
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Create Animator Controller", "New Animator Controller", "controller",
                    "Choose a location inside Assets for the animator controller.");
                if (string.IsNullOrEmpty(path))
                {
                    Speak("Controller creation cancelled. " + DescribeSelection());
                    return;
                }

                path = path.Replace('\\', '/');
                if (!IsAssetPath(path, ".controller")) throw new InvalidOperationException("Choose a .controller file inside Assets.");
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException("A controller already exists at that path.");
                AnimatorController created = AnimatorController.CreateAnimatorControllerAtPath(path);
                if (created == null) throw new InvalidOperationException("Unity could not create the animator controller.");
                ShowController(created, "Created blank controller");
            }
            catch (Exception exception) { ReportError(exception, "Controller creation failed."); }
        }

        private void OpenController()
        {
            try
            {
                string absolutePath = EditorUtility.OpenFilePanel(
                    "Open Animator Controller", Application.dataPath, "controller");
                if (string.IsNullOrEmpty(absolutePath))
                {
                    Speak("Open cancelled. " + DescribeSelection());
                    return;
                }

                string assetsDirectory = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullPath = Path.GetFullPath(absolutePath);
                string prefix = assetsDirectory + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Choose a controller inside this project's Assets folder.");
                string assetPath = "Assets/" + fullPath.Substring(prefix.Length).Replace('\\', '/');
                if (!IsAssetPath(assetPath, ".controller"))
                    throw new InvalidOperationException("Choose a .controller file inside Assets.");
                AnimatorController loaded = AssetDatabase.LoadAssetAtPath(assetPath, typeof(AnimatorController)) as AnimatorController;
                if (loaded == null) throw new InvalidOperationException("Unity could not load that animator controller.");
                ShowController(loaded, "Opened controller");
            }
            catch (Exception exception) { ReportError(exception, "Opening the controller failed."); }
        }

        private void ShowController(AnimatorController selectedController, string message)
        {
            controller = selectedController;
            view = View.Controller;
            selectedIndex = 0;
            selectedLayerIndex = 0;
            scrollPosition = Vector2.zero;
            RefreshAnimationStates();
            Focus();
            Repaint();
            Speak(message + " " + controller.name + ". " + animationStates.Count + " animations. " + DescribeSelection());
        }

        private void RefreshAnimationStates()
        {
            animationStates.Clear();
            AnimatorControllerLayer[] layers = controller == null ? null : controller.layers;
            int layerCount = layers == null ? 0 : layers.Length;
            // An external edit may have removed the active layer since the last draw.
            selectedLayerIndex = AccessibleList.Clamp(selectedLayerIndex, layerCount);
            if (selectedLayerIndex >= 0 && layers[selectedLayerIndex].stateMachine != null)
            {
                ChildAnimatorState[] states = layers[selectedLayerIndex].stateMachine.states;
                foreach (ChildAnimatorState child in states)
                {
                    if (child.state != null && child.state.motion is AnimationClip) animationStates.Add(child);
                }
            }
            selectedIndex = AccessibleList.Clamp(selectedIndex, ControllerItemCount);
        }

        private int LayerCount
        {
            get { return controller == null || controller.layers == null ? 0 : controller.layers.Length; }
        }

        private int ControllerItemCount
        {
            get { return LayerCount + 3 + animationStates.Count; }
        }

        private void SelectLayer(int index)
        {
            if (index < 0 || index >= LayerCount) return;
            selectedLayerIndex = index;
            selectedIndex = index;
            RefreshAnimationStates();
            Repaint();
            Speak("Selected layer " + controller.layers[index].name + ". " + animationStates.Count +
                " animations. " + DescribeSelection());
        }

        private void RemoveSelectedLayer()
        {
            try
            {
                int layerIndex = selectedIndex;
                if (controller == null || layerIndex < 0 || layerIndex >= LayerCount)
                    throw new InvalidOperationException("No layer is selected.");
                if (LayerCount == 1)
                {
                    Speak("The last layer cannot be removed. " + DescribeSelection());
                    return;
                }

                string name = controller.layers[layerIndex].name;
                int previousLayerCount = LayerCount;
                controller.RemoveLayer(layerIndex);
                if (LayerCount != previousLayerCount - 1)
                    throw new InvalidOperationException("Unity did not remove the selected layer.");

                // Keep keyboard focus on the row that took the removed layer's place.
                selectedLayerIndex = Mathf.Min(layerIndex, LayerCount - 1);
                selectedIndex = selectedLayerIndex;
                EditorUtility.SetDirty(controller);
                RefreshAnimationStates();
                Repaint();
                Speak("Removed layer " + name + ". " + DescribeSelection());
            }
            catch (Exception exception) { ReportError(exception, "Removing the layer failed."); }
        }

        private void BeginAddLayer()
        {
            if (controller == null) { Speak("No animator controller is open."); return; }
            layerName.Begin(string.Empty);
            view = View.AddLayer;
            selectedIndex = 0;
            focusLayerName = true;
            Repaint();
            Speak("Add layer. " + DescribeSelection());
        }

        private void AddLayer()
        {
            try
            {
                if (controller == null) throw new InvalidOperationException("No animator controller is open.");
                string name = layerName.Value == null ? string.Empty : layerName.Value.Trim();
                if (name.Length == 0)
                {
                    selectedIndex = 0;
                    focusLayerName = true;
                    Repaint();
                    Speak("Layer name cannot be empty. " + DescribeSelection());
                    return;
                }
                int previousLayerCount = LayerCount;
                controller.AddLayer(name);
                if (LayerCount <= previousLayerCount)
                    throw new InvalidOperationException("Unity did not create the new layer.");
                EditorUtility.SetDirty(controller);
                layerName.End();
                view = View.Controller;
                selectedLayerIndex = LayerCount - 1;
                selectedIndex = selectedLayerIndex;
                scrollPosition = Vector2.zero;
                RefreshAnimationStates();
                Focus();
                Repaint();
                Speak("Added layer " + name + ". " + DescribeSelection());
            }
            catch (Exception exception) { ReportError(exception, "Adding the layer failed."); }
        }

        private void BeginAddAnimation()
        {
            if (controller == null) { Speak("No animator controller is open."); return; }
            view = View.ClipPicker;
            searchText = string.Empty;
            selectedIndex = 0;
            scrollPosition = Vector2.zero;
            focusSearch = true;
            BuildClips();
            Repaint();
            Speak("Select an animation clip. " + clips.Count + " clips found. " + DescribeSelection());
        }

        private void BuildClips()
        {
            clips.Clear();
            // Search all project assets so clips inside imported models are found even when
            // Unity indexes the model's main asset as a GameObject instead of AnimationClip.
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsAssetPath(path, ".anim") && !(AssetImporter.GetAtPath(path) is ModelImporter)) continue;

                // LoadAllAssetsAtPath returns both standalone clips and model sub-assets.
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (UnityEngine.Object asset in assets)
                {
                    AnimationClip clip = asset as AnimationClip;
                    if (clip == null) continue;
                    if (!string.IsNullOrWhiteSpace(searchText) &&
                        clip.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0 &&
                        path.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    clips.Add(new ClipEntry(clip, path));
                }
            }
            clips.Sort((left, right) =>
            {
                int pathOrder = string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
                return pathOrder != 0 ? pathOrder : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void AddAnimation(ClipEntry entry)
        {
            try
            {
                if (controller == null || entry == null || entry.Clip == null ||
                    !string.Equals(AssetDatabase.GetAssetPath(entry.Clip), entry.Path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Select a valid AnimationClip asset for an open controller.");
                AnimationClip clip = entry.Clip;
                AnimatorState state = controller.AddMotion(clip, selectedLayerIndex);
                if (state == null) throw new InvalidOperationException("Unity could not add the animation state.");
                EditorUtility.SetDirty(controller);
                ReturnToController("Added animation " + clip.name + ".");
            }
            catch (Exception exception) { ReportError(exception, "Adding the animation failed."); }
        }

        private void RemoveSelectedAnimation()
        {
            try
            {
                int stateIndex = selectedIndex - LayerCount - 3;
                if (controller == null || stateIndex < 0 || stateIndex >= animationStates.Count)
                    throw new InvalidOperationException("No animation is selected.");
                AnimatorState state = animationStates[stateIndex].state;
                string name = state.motion == null ? state.name : state.motion.name;
                controller.layers[selectedLayerIndex].stateMachine.RemoveState(state);
                EditorUtility.SetDirty(controller);
                RefreshAnimationStates();
                Repaint();
                Speak("Removed animation " + name + ". " + DescribeSelection());
            }
            catch (Exception exception) { ReportError(exception, "Removing the animation failed."); }
        }

        private void SaveController()
        {
            try
            {
                if (controller == null) throw new InvalidOperationException("No animator controller is open.");
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                Speak("Saved animator controller " + controller.name + ".");
            }
            catch (Exception exception) { ReportError(exception, "Saving the controller failed."); }
        }

        private void ReturnToController(string message)
        {
            layerName.End();
            view = View.Controller;
            selectedIndex = selectedLayerIndex;
            scrollPosition = Vector2.zero;
            RefreshAnimationStates();
            Focus();
            Repaint();
            Speak(message + " " + animationStates.Count + " animations in the selected layer. " + DescribeSelection());
        }

        private string DescribeSelection()
        {
            if (view == View.Start)
                return (selectedIndex == 0 ? "Create animator controller" : "Open animator controller") +
                    ", button, " + AccessibleList.Position(selectedIndex, 2) + ".";
            if (view == View.ClipPicker)
            {
                if (selectedIndex == 0) return "Search animation clips, editable text, " +
                    (string.IsNullOrEmpty(searchText) ? "empty" : searchText) + ".";
                ClipEntry clip = clips[selectedIndex - 1];
                return clip.Name + ", " + clip.Path + ", " +
                    AccessibleList.Position(selectedIndex, clips.Count + 1) + ".";
            }
            if (view == View.AddLayer)
            {
                if (selectedIndex == 0) return "Layer name, editable text, " +
                    (string.IsNullOrEmpty(layerName.Value) ? "empty" : layerName.Value) + ".";
                return (selectedIndex == 1 ? "Create layer" : "Cancel") + ", button, " +
                    AccessibleList.Position(selectedIndex, 3) + ".";
            }
            if (selectedIndex < LayerCount) return "Layer " + controller.layers[selectedIndex].name +
                (selectedIndex == selectedLayerIndex ? ", active" : string.Empty) + ", button, " +
                AccessibleList.Position(selectedIndex, ControllerItemCount) +
                (LayerCount > 1 ? ". Press Enter to select or Backspace to remove." :
                    ". Press Enter to select. The last layer cannot be removed.");
            if (selectedIndex == LayerCount) return "Add animation, button, " +
                AccessibleList.Position(selectedIndex, ControllerItemCount) + ".";
            if (selectedIndex == LayerCount + 1) return "Add layer, button, " +
                AccessibleList.Position(selectedIndex, ControllerItemCount) + ".";
            if (selectedIndex == LayerCount + 2) return "Save changes, button, " +
                AccessibleList.Position(selectedIndex, ControllerItemCount) + ".";
            AnimatorState state = animationStates[selectedIndex - LayerCount - 3].state;
            AnimationClip animation = state.motion as AnimationClip;
            return "Animation " + (animation == null ? state.name : animation.name) + ", " +
                AccessibleList.Position(selectedIndex, ControllerItemCount) +
                ". Press Backspace to remove.";
        }

        private static bool IsAssetPath(string path, string extension)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        private static void Speak(string message)
        {
            AccessibleSpeech.Speak(message, SourceFile);
        }

        private static void ReportError(Exception exception, string message)
        {
            PluginErrorLog.Write(SourceFile, exception);
            Speak(message + " See Editor debug.txt.");
        }

        private enum View { Start, Controller, ClipPicker, AddLayer }

        private sealed class ClipEntry
        {
            internal ClipEntry(AnimationClip clip, string path) { Clip = clip; Path = path; }
            internal AnimationClip Clip { get; private set; }
            internal string Name { get { return Clip == null ? "Missing clip" : Clip.name; } }
            internal string Path { get; private set; }
        }
    }
}
