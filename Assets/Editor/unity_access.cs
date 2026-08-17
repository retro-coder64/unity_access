using System;
using UnityEditor;

namespace UnityAccess
{
    /// <summary>
    /// Provides the main entry point for the Unity Access editor plugin.
    /// </summary>
    public static class UnityAccessPlugin
    {
        private const string StartMenuPath = "Unity Access/Start";

        /// <summary>
        /// Starts Unity Access from the Unity Editor menu bar.
        /// </summary>
        [MenuItem(StartMenuPath, false, 0)]
        public static void Start()
        {
            try
            {
                HierarchyAccessibilityWindow.Open();
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(nameof(UnityAccessPlugin), exception);
                EditorUtility.DisplayDialog(
                    "Unity Access",
                    "Unity Access could not communicate with NVDA. See Editor/debug.txt for details.",
                    "OK");
            }
        }
    }
}
