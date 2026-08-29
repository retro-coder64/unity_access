using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>An NVDA-friendly, keyboard-operated view of the project's Assets folder.</summary>
    public sealed class AccessibleAssetsViewer : EditorWindow
    {
        private const string SourceFile = nameof(AccessibleAssetsViewer);
        private const string MenuPath = "Unity Access/Assets Viewer";
        private const string SearchControl = "UnityAccessAssetSearch";
        private const string RenameControl = "UnityAccessAssetRename";
        private const float RowHeight = 22.0f;
        private const int MaximumSearchResultsPerColumn = 100;

        private static readonly string[] StandardMenuOptions = { "Rename", "Delete", "Move" };
        private static readonly string[] ImageMenuOptions = { "Rename", "Delete", "Move", "Convert to texture", "Convert to sprite" };

        private readonly List<AssetEntry> folders = new List<AssetEntry>();
        private readonly List<AssetEntry> files = new List<AssetEntry>();
        private readonly AccessibleTextEdit renameEdit = new AccessibleTextEdit();
        private Vector2 folderScroll;
        private Vector2 fileScroll;
        private string currentFolder = "Assets";
        private string searchText = string.Empty;
        private int selectedFolderIndex = -1;
        private int selectedFileIndex = -1;
        private Pane activePane = Pane.Folders;
        private AssetEntry editTarget;
        private AssetEntry menuTarget;
        private int menuIndex;
        private bool isSearchSelected = true;
        private bool focusSearch;
        private bool focusRename;

        private enum Pane
        {
            Folders,
            Files
        }

        private sealed class AssetEntry
        {
            public string Path { get; private set; }
            public string Name { get; private set; }
            public string TypeName { get; private set; }
            public bool IsFolder { get; private set; }

            public AssetEntry(string path, bool isFolder)
            {
                Path = path;
                Name = System.IO.Path.GetFileName(path);
                IsFolder = isFolder;
                Type assetType = isFolder ? null : AssetDatabase.GetMainAssetTypeAtPath(path);
                TextureImporter textureImporter = isFolder ? null : AssetImporter.GetAtPath(path) as TextureImporter;
                bool isSprite = textureImporter != null && textureImporter.textureType == TextureImporterType.Sprite;
                TypeName = isFolder ? "Folder" : (isSprite ? "Sprite" : (assetType == null ? "Asset" : assetType.Name));
            }
        }

        /// <summary>Pairs an asset with its name-match quality for deterministic search ordering.</summary>
        private sealed class SearchMatch
        {
            public AssetEntry Entry { get; private set; }
            public int Score { get; private set; }

            public SearchMatch(AssetEntry entry, int score)
            {
                Entry = entry;
                Score = score;
            }
        }

        /// <summary>Opens the accessible asset viewer from the Unity Access menu.</summary>
        [MenuItem(MenuPath, false, 20)]
        public static void Open()
        {
            AccessibleAssetsViewer window = GetWindow<AccessibleAssetsViewer>(true, "Accessible Assets Viewer", true);
            window.minSize = new Vector2(760.0f, 480.0f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            RefreshEntries();
            isSearchSelected = true;
            focusSearch = false;
            AccessibleSpeech.Speak("Search, editable text, press Enter to edit", SourceFile);
        }

        private void OnFocus()
        {
            wantsMouseMove = true;
        }

        private void OnGUI()
        {
            try
            {
                DrawSearch();
                DrawPathHeader();
                DrawColumns();
                DrawRenameBox();
                DrawOptionsMenu();
                HandleKeyboard(Event.current);
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(SourceFile, exception);
                EditorGUILayout.HelpBox("The assets viewer encountered an error. See Editor/debug.txt.", MessageType.Error);
            }
        }

        private void DrawSearch()
        {
            Rect searchRow = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            AccessibleEditorStyles.DrawSelection(searchRow, isSearchSelected);
            string updated = AccessibleControls.TextBox(searchRow, SearchControl, "Search", searchText, focusSearch);
            focusSearch = false;
            if (!string.Equals(updated, searchText, StringComparison.Ordinal))
            {
                searchText = updated;
                RefreshEntries();
            }
        }

        private void DrawPathHeader()
        {
            EditorGUILayout.Space(4.0f);
            string heading = string.IsNullOrWhiteSpace(searchText)
                ? currentFolder
                : "Search results for " + searchText.Trim();
            EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Up/Down: select   Left/Right: change column   Enter: open/options   F2: rename   Backspace: delete   Ctrl+F: search");
            EditorGUILayout.Space(4.0f);
        }

        private void DrawColumns()
        {
            EditorGUILayout.BeginHorizontal();
            DrawEntryColumn("Folders", folders, Pane.Folders, ref selectedFolderIndex, ref folderScroll);
            DrawEntryColumn("Files and assets", files, Pane.Files, ref selectedFileIndex, ref fileScroll);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEntryColumn(string heading, List<AssetEntry> entries, Pane pane, ref int selectedIndex, ref Vector2 scroll)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f - 8.0f));
            EditorGUILayout.LabelField(heading, activePane == pane ? EditorStyles.boldLabel : EditorStyles.label);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (entries.Count == 0)
            {
                EditorGUILayout.LabelField("No matching " + heading.ToLowerInvariant() + ".");
            }

            for (int index = 0; index < entries.Count; index++)
            {
                AssetEntry entry = entries[index];
                string location = string.IsNullOrWhiteSpace(searchText) ? string.Empty : " — " + GetParentPath(entry.Path);
                GUIContent content = new GUIContent(entry.Name + (entry.IsFolder ? string.Empty : " — " + entry.TypeName) + location, AssetDatabase.GetCachedIcon(entry.Path));
                Rect row = EditorGUILayout.GetControlRect(false, RowHeight);
                bool isSelected = activePane == pane && index == selectedIndex;
                AccessibleEditorStyles.DrawSelection(row, isSelected);
                if (GUI.Button(row, content, EditorStyles.label))
                {
                    activePane = pane;
                    selectedIndex = index;
                    AnnounceSelection();
                    Repaint();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRenameBox()
        {
            if (!renameEdit.IsEditing)
            {
                return;
            }

            EditorGUILayout.Space(4.0f);
            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            renameEdit.Value = AccessibleControls.TextBox(row, RenameControl, "New name", renameEdit.Value, focusRename);
            focusRename = false;
        }

        private void DrawOptionsMenu()
        {
            if (menuTarget == null)
            {
                return;
            }

            string[] options = GetMenuOptions(menuTarget);
            EditorGUILayout.Space(6.0f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Options for " + menuTarget.Name, EditorStyles.boldLabel);
            for (int index = 0; index < options.Length; index++)
            {
                if (AccessibleControls.Button(options[index], index == menuIndex))
                {
                    menuIndex = index;
                    RunMenuOption();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void HandleKeyboard(Event currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (renameEdit.IsEditing)
            {
                HandleRenameKeyboard(currentEvent);
                return;
            }

            if (menuTarget != null)
            {
                HandleMenuKeyboard(currentEvent);
                return;
            }

            if (currentEvent.control && currentEvent.keyCode == KeyCode.F)
            {
                isSearchSelected = true;
                focusSearch = true;
                currentEvent.Use();
                Repaint();
                return;
            }

            if (GUI.GetNameOfFocusedControl() == SearchControl)
            {
                if (currentEvent.keyCode == KeyCode.Escape)
                {
                    GUI.FocusControl(string.Empty);
                    isSearchSelected = true;
                    currentEvent.Use();
                    AnnounceSearch();
                }
                else if (AccessibleKeyboard.IsConfirm(currentEvent) || currentEvent.keyCode == KeyCode.DownArrow)
                {
                    GUI.FocusControl(string.Empty);
                    SelectFirstSearchResult();
                    currentEvent.Use();
                }

                return;
            }

            if (isSearchSelected)
            {
                if (AccessibleKeyboard.IsConfirm(currentEvent))
                {
                    focusSearch = true;
                    AccessibleSpeech.Speak("Search, editing", SourceFile);
                    currentEvent.Use();
                    Repaint();
                }
                else if (currentEvent.keyCode == KeyCode.DownArrow)
                {
                    SelectFirstSearchResult();
                    currentEvent.Use();
                }
                else if (AccessibleKeyboard.IsCancel(currentEvent))
                {
                    NavigateUpOrClose();
                    currentEvent.Use();
                }

                return;
            }

            if (currentEvent.keyCode == KeyCode.LeftArrow || currentEvent.keyCode == KeyCode.RightArrow)
            {
                isSearchSelected = false;
                activePane = currentEvent.keyCode == KeyCode.LeftArrow ? Pane.Folders : Pane.Files;
                EnsureSelection();
                AnnounceSelection();
                currentEvent.Use();
                Repaint();
                return;
            }

            int direction;
            if (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                MoveSelection(direction);
                currentEvent.Use();
                return;
            }

            if (currentEvent.keyCode == KeyCode.F2)
            {
                BeginRename();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Backspace)
            {
                DeleteSelected();
                currentEvent.Use();
            }
            else if (AccessibleKeyboard.IsConfirm(currentEvent))
            {
                ActivateSelected();
                currentEvent.Use();
            }
            else if (currentEvent.shift && currentEvent.keyCode == KeyCode.F10)
            {
                OpenOptionsForSelected();
                currentEvent.Use();
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent))
            {
                NavigateUpOrClose();
                currentEvent.Use();
            }
        }

        private void HandleRenameKeyboard(Event currentEvent)
        {
            if (AccessibleKeyboard.IsConfirm(currentEvent))
            {
                CommitRename();
                currentEvent.Use();
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent))
            {
                renameEdit.End();
                editTarget = null;
                GUI.FocusControl(string.Empty);
                AccessibleSpeech.Speak("Rename cancelled", SourceFile);
                currentEvent.Use();
                Repaint();
            }
        }

        private void HandleMenuKeyboard(Event currentEvent)
        {
            string[] options = GetMenuOptions(menuTarget);
            int direction;
            if (AccessibleKeyboard.TryGetVerticalDirection(currentEvent, out direction))
            {
                menuIndex = Mathf.Clamp(menuIndex + direction, 0, options.Length - 1);
                AccessibleSpeech.Speak(options[menuIndex] + ", " + (menuIndex + 1) + " of " + options.Length, SourceFile);
                currentEvent.Use();
                Repaint();
            }
            else if (AccessibleKeyboard.IsConfirm(currentEvent))
            {
                currentEvent.Use();
                RunMenuOption();
            }
            else if (AccessibleKeyboard.IsCancel(currentEvent))
            {
                menuTarget = null;
                AccessibleSpeech.Speak("Options closed", SourceFile);
                currentEvent.Use();
                Repaint();
            }
        }

        private void MoveSelection(int direction)
        {
            int currentIndex = activePane == Pane.Folders ? selectedFolderIndex : selectedFileIndex;
            if (direction < 0 && currentIndex <= 0)
            {
                isSearchSelected = true;
                AccessibleSpeech.Speak("Search, editable text, press Enter to edit", SourceFile);
                Repaint();
                return;
            }

            if (activePane == Pane.Folders)
            {
                selectedFolderIndex = AccessibleList.Move(selectedFolderIndex, direction, folders.Count);
                AccessibleList.KeepVisible(ref folderScroll, selectedFolderIndex, RowHeight);
            }
            else
            {
                selectedFileIndex = AccessibleList.Move(selectedFileIndex, direction, files.Count);
                AccessibleList.KeepVisible(ref fileScroll, selectedFileIndex, RowHeight);
            }

            AnnounceSelection();
            Repaint();
        }

        private void EnsureSelection()
        {
            selectedFolderIndex = AccessibleList.Clamp(selectedFolderIndex, folders.Count);
            selectedFileIndex = AccessibleList.Clamp(selectedFileIndex, files.Count);
            if (activePane == Pane.Folders && selectedFolderIndex < 0 && folders.Count > 0)
            {
                selectedFolderIndex = 0;
            }
            else if (activePane == Pane.Files && selectedFileIndex < 0 && files.Count > 0)
            {
                selectedFileIndex = 0;
            }
        }

        private void SelectFirstSearchResult()
        {
            isSearchSelected = false;
            activePane = folders.Count > 0 ? Pane.Folders : Pane.Files;
            EnsureSelection();
            AnnounceSelection();
            Repaint();
        }

        private AssetEntry GetSelected()
        {
            if (activePane == Pane.Folders)
            {
                return selectedFolderIndex >= 0 && selectedFolderIndex < folders.Count ? folders[selectedFolderIndex] : null;
            }

            return selectedFileIndex >= 0 && selectedFileIndex < files.Count ? files[selectedFileIndex] : null;
        }

        private void AnnounceSelection()
        {
            AssetEntry selected = GetSelected();
            List<AssetEntry> entries = activePane == Pane.Folders ? folders : files;
            int index = activePane == Pane.Folders ? selectedFolderIndex : selectedFileIndex;
            if (selected == null)
            {
                AccessibleSpeech.Speak(activePane + " column, empty", SourceFile);
                return;
            }

            string location = string.IsNullOrWhiteSpace(searchText) ? string.Empty : ", in " + GetParentPath(selected.Path);
            AccessibleSpeech.Speak(selected.Name + ", " + selected.TypeName + location + ", " + AccessibleList.Position(index, entries.Count), SourceFile);
        }

        private static void AnnounceSearch()
        {
            AccessibleSpeech.Speak("Search, editable text, press Enter to edit", SourceFile);
        }

        private void ActivateSelected()
        {
            AssetEntry selected = GetSelected();
            if (selected == null)
            {
                return;
            }

            if (selected.IsFolder)
            {
                currentFolder = selected.Path;
                searchText = string.Empty;
                RefreshEntries();
                AccessibleSpeech.Speak(selected.Name + " folder opened", SourceFile);
            }
            else
            {
                OpenOptions(selected);
            }
        }

        private void OpenOptionsForSelected()
        {
            AssetEntry selected = GetSelected();
            if (selected != null)
            {
                OpenOptions(selected);
            }
        }

        private void OpenOptions(AssetEntry target)
        {
            menuTarget = target;
            menuIndex = 0;
            string[] options = GetMenuOptions(target);
            AccessibleSpeech.Speak("Options for " + target.Name + ". " + options[0] + ", 1 of " + options.Length, SourceFile);
            Repaint();
        }

        /// <summary>Includes texture conversion actions only for assets handled by Unity's texture importer.</summary>
        private static string[] GetMenuOptions(AssetEntry target)
        {
            if (target != null && !target.IsFolder && AssetImporter.GetAtPath(target.Path) is TextureImporter)
            {
                return ImageMenuOptions;
            }

            return StandardMenuOptions;
        }

        private void RunMenuOption()
        {
            AssetEntry target = menuTarget;
            menuTarget = null;
            if (target == null)
            {
                return;
            }

            if (menuIndex == 0)
            {
                BeginRename(target);
            }
            else if (menuIndex == 1)
            {
                Delete(target);
            }
            else if (menuIndex == 2)
            {
                Move(target);
            }
            else if (menuIndex == 3)
            {
                ConvertImage(target, TextureImporterType.Default, "texture");
            }
            else if (menuIndex == 4)
            {
                ConvertImage(target, TextureImporterType.Sprite, "sprite");
            }
        }

        /// <summary>Changes an image's Unity import mode and reimports it without bypassing its metadata.</summary>
        private void ConvertImage(AssetEntry target, TextureImporterType importerType, string typeName)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(target.Path) as TextureImporter;
            if (textureImporter == null)
            {
                InvalidOperationException exception = new InvalidOperationException("No TextureImporter was found for " + target.Path);
                PluginErrorLog.Write(SourceFile, exception);
                AccessibleSpeech.Speak("Conversion failed. The selected asset is not a supported image.", SourceFile);
                return;
            }

            try
            {
                textureImporter.textureType = importerType;
                if (importerType == TextureImporterType.Sprite)
                {
                    // A Multiple sprite import with no slices creates no usable Sprite sub-asset.
                    textureImporter.spriteImportMode = SpriteImportMode.Single;
                    textureImporter.alphaIsTransparency = true;
                    textureImporter.mipmapEnabled = false;
                }

                textureImporter.SaveAndReimport();

                TextureImporter refreshedImporter = AssetImporter.GetAtPath(target.Path) as TextureImporter;
                bool hasExpectedImporterType = refreshedImporter != null && refreshedImporter.textureType == importerType;
                bool hasUsableSprite = importerType != TextureImporterType.Sprite
                    || AssetDatabase.LoadAssetAtPath<Sprite>(target.Path) != null;
                if (!hasExpectedImporterType || !hasUsableSprite)
                {
                    throw new InvalidOperationException("Unity did not create the requested " + typeName + " for " + target.Path);
                }

                AssetDatabase.SaveAssets();
                RefreshEntries();
                AccessibleSpeech.Speak(target.Name + " converted to " + typeName, SourceFile);
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(SourceFile, exception);
                AccessibleSpeech.Speak("Conversion failed. See Editor debug log.", SourceFile);
            }
        }

        private void BeginRename()
        {
            BeginRename(GetSelected());
        }

        private void BeginRename(AssetEntry target)
        {
            if (target == null)
            {
                return;
            }

            editTarget = target;
            renameEdit.Begin(target.Name);
            focusRename = true;
            AccessibleSpeech.Speak("Rename " + target.Name + ". Enter a new name, then press Enter.", SourceFile);
            Repaint();
        }

        private void CommitRename()
        {
            if (editTarget == null)
            {
                renameEdit.End();
                return;
            }

            string newName = renameEdit.Value.Trim();
            if (string.IsNullOrEmpty(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                AccessibleSpeech.Speak("Invalid name. Enter another name.", SourceFile);
                focusRename = true;
                return;
            }

            string error = AssetDatabase.RenameAsset(editTarget.Path, newName);
            if (!string.IsNullOrEmpty(error))
            {
                PluginErrorLog.Write(SourceFile, new InvalidOperationException(error));
                AccessibleSpeech.Speak("Rename failed. " + error, SourceFile);
                focusRename = true;
                return;
            }

            renameEdit.End();
            editTarget = null;
            AssetDatabase.SaveAssets();
            RefreshEntries();
            AccessibleSpeech.Speak("Renamed to " + newName, SourceFile);
            GUI.FocusControl(string.Empty);
        }

        private void DeleteSelected()
        {
            Delete(GetSelected());
        }

        private void Delete(AssetEntry target)
        {
            if (target == null)
            {
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog("Delete item", "Are you sure you want to delete this item?\n\n" + target.Name, "Delete", "Cancel");
            if (!confirmed)
            {
                AccessibleSpeech.Speak("Delete cancelled", SourceFile);
                return;
            }

            if (!AssetDatabase.DeleteAsset(target.Path))
            {
                InvalidOperationException exception = new InvalidOperationException("AssetDatabase could not delete " + target.Path);
                PluginErrorLog.Write(SourceFile, exception);
                AccessibleSpeech.Speak("Delete failed. See Editor debug log.", SourceFile);
                return;
            }

            RefreshEntries();
            AccessibleSpeech.Speak(target.Name + " deleted", SourceFile);
        }

        private void Move(AssetEntry target)
        {
            string absoluteDestination = EditorUtility.OpenFolderPanel("Move " + target.Name, Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(absoluteDestination))
            {
                AccessibleSpeech.Speak("Move cancelled", SourceFile);
                return;
            }

            string destinationFolder = FileUtil.GetProjectRelativePath(absoluteDestination).Replace('\\', '/');
            if (string.IsNullOrEmpty(destinationFolder) || !AssetDatabase.IsValidFolder(destinationFolder))
            {
                EditorUtility.DisplayDialog("Move item", "Choose a folder inside this Unity project's Assets folder.", "OK");
                AccessibleSpeech.Speak("Invalid destination. Choose a folder inside Assets.", SourceFile);
                return;
            }

            string destinationPath = AssetDatabase.GenerateUniqueAssetPath(destinationFolder + "/" + target.Name);
            string error = AssetDatabase.MoveAsset(target.Path, destinationPath);
            if (!string.IsNullOrEmpty(error))
            {
                PluginErrorLog.Write(SourceFile, new InvalidOperationException(error));
                AccessibleSpeech.Speak("Move failed. " + error, SourceFile);
                return;
            }

            AssetDatabase.SaveAssets();
            RefreshEntries();
            AccessibleSpeech.Speak(target.Name + " moved to " + destinationFolder, SourceFile);
        }

        private void NavigateUpOrClose()
        {
            if (string.Equals(currentFolder, "Assets", StringComparison.Ordinal))
            {
                AccessibleSpeech.Speak("Closing assets viewer", SourceFile);
                Close();
                return;
            }

            string parent = Path.GetDirectoryName(currentFolder);
            currentFolder = string.IsNullOrEmpty(parent) ? "Assets" : parent.Replace('\\', '/');
            searchText = string.Empty;
            RefreshEntries();
            AccessibleSpeech.Speak(currentFolder + " opened", SourceFile);
        }

        private void RefreshEntries()
        {
            folders.Clear();
            files.Clear();
            if (!AssetDatabase.IsValidFolder(currentFolder))
            {
                currentFolder = "Assets";
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                PopulateSearchResults(searchText.Trim());
                FinishRefresh();
                return;
            }

            string[] subfolders = AssetDatabase.GetSubFolders(currentFolder);
            for (int index = 0; index < subfolders.Length; index++)
            {
                AssetEntry entry = new AssetEntry(subfolders[index], true);
                folders.Add(entry);
            }

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { currentFolder });
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (AssetDatabase.IsValidFolder(path) || !IsImmediateChild(path, currentFolder))
                {
                    continue;
                }

                AssetEntry entry = new AssetEntry(path, false);
                files.Add(entry);
            }

            folders.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            files.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            FinishRefresh();
        }

        private void PopulateSearchResults(string term)
        {
            List<SearchMatch> folderMatches = new List<SearchMatch>();
            List<SearchMatch> fileMatches = new List<SearchMatch>();
            HashSet<string> visitedPaths = new HashSet<string>(StringComparer.Ordinal);
            AddFolderSearchMatches("Assets", term, folderMatches, visitedPaths);

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" });
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path) || !visitedPaths.Add(path))
                {
                    continue;
                }

                AddSearchMatch(new AssetEntry(path, false), term, fileMatches);
            }

            SortAndCopyMatches(folderMatches, folders);
            SortAndCopyMatches(fileMatches, files);
        }

        private static void AddFolderSearchMatches(string parentFolder, string term, List<SearchMatch> matches, HashSet<string> visitedPaths)
        {
            string[] childFolders = AssetDatabase.GetSubFolders(parentFolder);
            for (int index = 0; index < childFolders.Length; index++)
            {
                string childFolder = childFolders[index];
                if (!visitedPaths.Add(childFolder))
                {
                    continue;
                }

                AddSearchMatch(new AssetEntry(childFolder, true), term, matches);
                AddFolderSearchMatches(childFolder, term, matches, visitedPaths);
            }
        }

        private static void AddSearchMatch(AssetEntry entry, string term, List<SearchMatch> matches)
        {
            int score = CalculateMatchScore(entry.Name, term);
            if (score >= 0)
            {
                matches.Add(new SearchMatch(entry, score));
            }
        }

        private static void SortAndCopyMatches(List<SearchMatch> matches, List<AssetEntry> destination)
        {
            matches.Sort((left, right) =>
            {
                int scoreComparison = left.Score.CompareTo(right.Score);
                return scoreComparison != 0
                    ? scoreComparison
                    : string.Compare(left.Entry.Name, right.Entry.Name, StringComparison.OrdinalIgnoreCase);
            });

            int resultCount = Mathf.Min(matches.Count, MaximumSearchResultsPerColumn);
            for (int index = 0; index < resultCount; index++)
            {
                destination.Add(matches[index].Entry);
            }
        }

        private static int CalculateMatchScore(string candidate, string term)
        {
            if (string.Equals(candidate, term, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (candidate.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            {
                return 10 + candidate.Length - term.Length;
            }

            int substringIndex = candidate.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (substringIndex >= 0)
            {
                return 30 + substringIndex + candidate.Length - term.Length;
            }

            int distance = CalculateEditDistance(candidate.ToLowerInvariant(), term.ToLowerInvariant());
            int allowedDistance = Mathf.Max(1, term.Length / 3);
            return distance <= allowedDistance ? 100 + distance : -1;
        }

        private static int CalculateEditDistance(string candidate, string term)
        {
            int[,] distances = new int[candidate.Length + 1, term.Length + 1];
            for (int candidateIndex = 0; candidateIndex <= candidate.Length; candidateIndex++)
            {
                distances[candidateIndex, 0] = candidateIndex;
            }

            for (int termIndex = 0; termIndex <= term.Length; termIndex++)
            {
                distances[0, termIndex] = termIndex;
            }

            for (int candidateIndex = 1; candidateIndex <= candidate.Length; candidateIndex++)
            {
                for (int termIndex = 1; termIndex <= term.Length; termIndex++)
                {
                    int substitutionCost = candidate[candidateIndex - 1] == term[termIndex - 1] ? 0 : 1;
                    distances[candidateIndex, termIndex] = Mathf.Min(
                        Mathf.Min(distances[candidateIndex - 1, termIndex] + 1, distances[candidateIndex, termIndex - 1] + 1),
                        distances[candidateIndex - 1, termIndex - 1] + substitutionCost);
                }
            }

            return distances[candidate.Length, term.Length];
        }

        private void FinishRefresh()
        {
            selectedFolderIndex = folders.Count > 0 ? 0 : -1;
            selectedFileIndex = files.Count > 0 ? 0 : -1;
            EnsureSelection();
            folderScroll = Vector2.zero;
            fileScroll = Vector2.zero;
            Repaint();
        }

        private static string GetParentPath(string path)
        {
            string parent = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(parent) ? "Assets" : parent.Replace('\\', '/');
        }

        private static bool IsImmediateChild(string path, string folder)
        {
            string parent = Path.GetDirectoryName(path);
            return string.Equals(parent == null ? string.Empty : parent.Replace('\\', '/'), folder, StringComparison.Ordinal);
        }
    }
}
