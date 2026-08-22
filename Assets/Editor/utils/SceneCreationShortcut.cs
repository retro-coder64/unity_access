using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>Assigns Ctrl+N exclusively to the accessible scene creation window.</summary>
    [InitializeOnLoad]
    internal static class SceneCreationShortcut
    {
        private const string SourceFile = "SceneCreationShortcut.cs";
        private const string AccessibleShortcutId = "Unity Access/Create Scene";
        private const string BuiltInShortcutId = "Main Menu/File/New Scene";
        private const string WritableProfileId = "Unity Access";

        static SceneCreationShortcut()
        {
            // Delay setup until Unity has finished discovering all shortcut attributes.
            EditorApplication.delayCall += Configure;
        }

        private static void Configure()
        {
            try
            {
                IShortcutManager shortcutManager = ShortcutManager.instance;
                EnsureWritableProfile(shortcutManager);
                HashSet<string> shortcutIds = new HashSet<string>(
                    shortcutManager.GetAvailableShortcutIds(), StringComparer.Ordinal);

                if (!shortcutIds.Contains(AccessibleShortcutId))
                {
                    throw new InvalidOperationException(
                        "The accessible scene creation shortcut was not registered by Unity.");
                }

                // Remove Unity's original Ctrl+N owner before assigning the same chord.
                if (shortcutIds.Contains(BuiltInShortcutId))
                {
                    shortcutManager.RebindShortcut(BuiltInShortcutId, ShortcutBinding.empty);
                }

                KeyCombination controlN = new KeyCombination(KeyCode.N, ShortcutModifiers.Action);
                shortcutManager.RebindShortcut(
                    AccessibleShortcutId,
                    new ShortcutBinding(controlN));
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(SourceFile, exception);
                AccessibleSpeech.Speak(
                    "Ctrl N could not be assigned to the accessible scene creator.",
                    SourceFile);
            }
        }

        private static void EnsureWritableProfile(IShortcutManager shortcutManager)
        {
            string activeProfileId = shortcutManager.activeProfileId;
            if (!shortcutManager.IsProfileReadOnly(activeProfileId))
            {
                return;
            }

            HashSet<string> profileIds = new HashSet<string>(
                shortcutManager.GetAvailableProfileIds(), StringComparer.Ordinal);
            if (!profileIds.Contains(WritableProfileId))
            {
                shortcutManager.CreateProfile(WritableProfileId);
            }

            shortcutManager.activeProfileId = WritableProfileId;
        }
    }
}
