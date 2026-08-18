using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityAccess.Accessibility;

namespace UnityAccess.Console
{
    /// <summary>Provides a keyboard-driven, NVDA-accessible view of Unity log messages.</summary>
    public sealed class AccessibleConsoleWindow : EditorWindow
    {
        private const string WindowTitle = "Accessible Console";
        private const string EmptyMessage = "The console contains no messages.";
        private const int ClearControlIndex = -1;
        private static EditorWindow previousWindow;
        private Vector2 scrollPosition;
        private int selectedIndex = ClearControlIndex;

        private List<ConsoleLogEntry> Entries
        {
            get { return ConsoleLogStore.Entries; }
        }

        /// <summary>Opens and focuses the console. Ctrl+Alt+C avoids Unity's built-in shortcut.</summary>
        [MenuItem("Unity Access/Console %&c", false, 3)]
        public static void Open()
        {
            try
            {
                EditorWindow currentFocusedWindow = EditorWindow.focusedWindow;
                if (!(currentFocusedWindow is AccessibleConsoleWindow))
                {
                    previousWindow = currentFocusedWindow;
                }

                AccessibleConsoleWindow window = GetWindow<AccessibleConsoleWindow>();
                window.titleContent = new GUIContent(WindowTitle);
                window.minSize = new Vector2(420.0f, 180.0f);
                window.selectedIndex = ClearControlIndex;
                window.PlaceAtBottomOfEditor();
                window.Show();
                window.Focus();

                if (!NvdaApi.IsRunning)
                {
                    throw new InvalidOperationException("NVDA is not running or rejected the controller connection.");
                }

                window.AnnounceOpened();
                window.Repaint();
            }
            catch (Exception exception)
            {
                ConsoleDiagnostics.Record(nameof(AccessibleConsoleWindow), exception);
                EditorUtility.DisplayDialog(
                    "Unity Access NVDA error",
                    "The console could not communicate with NVDA. See Assets/Editor/consol/debug.txt for details.",
                    "OK");
            }
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            ConsoleLogStore.Changed += HandleLogStoreChanged;
        }

        private void OnDisable()
        {
            ConsoleLogStore.Changed -= HandleLogStoreChanged;
        }

        private void OnGUI()
        {
            try
            {
                HandleKeyboardInput(Event.current);
                DrawConsole();
            }
            catch (Exception exception)
            {
                ConsoleDiagnostics.Record(nameof(AccessibleConsoleWindow), exception);
            }
        }

        private void DrawConsole()
        {
            EditorGUILayout.LabelField("Accessible console", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Up and Down navigate. Enter clears on Clear Console. Ctrl+C copies a message. Escape closes.");

            // Clear is a normal cursor row, matching the working inspector action-row pattern.
            Rect clearRow = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            if (selectedIndex == ClearControlIndex)
            {
                EditorGUI.DrawRect(clearRow, new Color(0.24f, 0.49f, 0.90f, 0.45f));
            }

            EditorGUI.LabelField(clearRow, "Clear Console, button. Press Enter.", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int index = 0; index < Entries.Count; index++)
            {
                DrawMessageRow(index, Entries[index]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawMessageRow(int index, ConsoleLogEntry entry)
        {
            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2.0f);
            if (index == selectedIndex)
            {
                EditorGUI.DrawRect(row, new Color(0.24f, 0.49f, 0.90f, 0.45f));
            }

            GUIStyle messageStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            messageStyle.normal.textColor = GetLogColor(entry.Type);
            EditorGUI.LabelField(row, entry.DisplayText, messageStyle);
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
                ActivateSelection();
                currentEvent.Use();
            }
            else if (currentEvent.control && currentEvent.keyCode == KeyCode.C)
            {
                CopySelectedMessage();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                currentEvent.Use();
                Close();
                EditorApplication.delayCall += RestorePreviousFocus;
            }
        }

        private void MoveSelection(int direction)
        {
            if (Entries.Count == 0)
            {
                selectedIndex = ClearControlIndex;
                SpeakSafely("Clear Console, button. " + EmptyMessage);
                Repaint();
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex + direction, ClearControlIndex, Entries.Count - 1);
            SpeakSafely(GetSelectedDescription());
            Repaint();
        }

        private void ActivateSelection()
        {
            if (selectedIndex != ClearControlIndex)
            {
                SpeakSafely(GetSelectedDescription());
                return;
            }

            ConsoleLogStore.Clear();
            selectedIndex = ClearControlIndex;
            SpeakSafely("Console cleared. " + EmptyMessage);
            Repaint();
        }

        private void CopySelectedMessage()
        {
            if (selectedIndex < 0 || selectedIndex >= Entries.Count)
            {
                SpeakSafely("Choose a message before copying.");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = Entries[selectedIndex].CopyText;
            SpeakSafely("Message copied. " + GetSelectedDescription());
        }

        private void AnnounceOpened()
        {
            string messageCount = Entries.Count == 1 ? "1 message" : Entries.Count + " messages";
            SpeakSafely("Accessible Console opened. " + messageCount + ". Clear Console, button. Use Up and Down to navigate.");
        }

        private string GetSelectedDescription()
        {
            if (selectedIndex == ClearControlIndex)
            {
                return "Clear Console, button. Press Enter to clear all messages.";
            }

            ConsoleLogEntry entry = Entries[selectedIndex];
            return entry.AccessibleText + ", " + (selectedIndex + 1) + " of " + Entries.Count + ".";
        }

        private void HandleLogStoreChanged()
        {
            selectedIndex = Mathf.Clamp(selectedIndex, ClearControlIndex, Entries.Count - 1);
            Repaint();
        }

        private void PlaceAtBottomOfEditor()
        {
            // Keep the console as a full-width bar at the bottom of Unity's main window.
            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            const float consoleHeight = 260.0f;
            position = new Rect(mainWindow.x, mainWindow.yMax - consoleHeight, mainWindow.width, consoleHeight);
        }

        private static void RestorePreviousFocus()
        {
            if (previousWindow != null)
            {
                previousWindow.Focus();
            }
        }

        private static void SpeakSafely(string message)
        {
            try
            {
                NvdaApi.Speak(message);
            }
            catch (Exception exception)
            {
                ConsoleDiagnostics.Record(nameof(AccessibleConsoleWindow), exception);
            }
        }

        internal static Color GetLogColor(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return new Color(1.0f, 0.65f, 0.0f);
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return Color.red;
                default:
                    return Color.white;
            }
        }
    }

    /// <summary>Captures Unity messages for the full editor session while the window is closed.</summary>
    [InitializeOnLoad]
    internal static class ConsoleLogStore
    {
        internal static readonly List<ConsoleLogEntry> Entries = new List<ConsoleLogEntry>();
        internal static event Action Changed;

        static ConsoleLogStore()
        {
            Application.logMessageReceived += ReceiveLog;
        }

        internal static void Clear()
        {
            Entries.Clear();
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }

        private static void ReceiveLog(string condition, string stackTrace, LogType type)
        {
            Entries.Add(new ConsoleLogEntry(condition, stackTrace, type));
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }

            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
            {
                EditorApplication.delayCall += AccessibleConsoleWindow.Open;
            }
        }
    }

    /// <summary>Stores one immutable Unity console message.</summary>
    internal sealed class ConsoleLogEntry
    {
        internal ConsoleLogEntry(string message, string stackTrace, LogType type)
        {
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Type = type;
        }

        internal string Message { get; private set; }
        internal string StackTrace { get; private set; }
        internal LogType Type { get; private set; }
        internal string DisplayText { get { return "[" + GetTypeName(Type) + "] " + Message; } }
        internal string AccessibleText { get { return GetTypeName(Type) + ". " + Message; } }
        internal string CopyText { get { return string.IsNullOrWhiteSpace(StackTrace) ? DisplayText : DisplayText + Environment.NewLine + StackTrace; } }

        internal static string GetTypeName(LogType type)
        {
            return type == LogType.Assert ? "Assertion failure" : type.ToString();
        }
    }
}
