using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>
    /// Sends accessible text to a running 64-bit NVDA screen reader instance.
    /// </summary>
    public static class NvdaApi
    {
        private const int Success = 0;

        /// <summary>
        /// Gets whether the bundled controller client can reach a running NVDA instance.
        /// </summary>
        public static bool IsRunning
        {
            get { return NativeMethods.TestIfRunning() == Success; }
        }

        /// <summary>
        /// Passes the supplied text to NVDA for immediate speech output.
        /// </summary>
        /// <param name="text">The non-empty text that NVDA should speak.</param>
        public static void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("NVDA speech text cannot be empty.", nameof(text));
            }

            int result = NativeMethods.CancelSpeech();
            if (result != Success)
            {
                throw new Win32Exception(result, "NVDA could not cancel its current speech.");
            }

            result = NativeMethods.SpeakText(text);
            if (result != Success)
            {
                throw new Win32Exception(result, "NVDA rejected the speech request.");
            }
        }

        /// <summary>
        /// Contains the native NVDA Controller Client declaration in one place.
        /// </summary>
        private static class NativeMethods
        {
            [DllImport(
                "nvdaControllerClient.dll",
                EntryPoint = "nvdaController_speakText",
                CharSet = CharSet.Unicode,
                ExactSpelling = true)]
            internal static extern int SpeakText(string text);

            [DllImport(
                "nvdaControllerClient.dll",
                EntryPoint = "nvdaController_cancelSpeech",
                ExactSpelling = true)]
            internal static extern int CancelSpeech();

            [DllImport(
                "nvdaControllerClient.dll",
                EntryPoint = "nvdaController_testIfRunning",
                ExactSpelling = true)]
            internal static extern int TestIfRunning();
        }
    }

    /// <summary>
    /// Writes plugin failures to the project Editor/debug.txt file.
    /// </summary>
    public static class PluginErrorLog
    {
        private const string DebugFileName = "debug.txt";

        /// <summary>
        /// Records an error without allowing a logging failure to crash the editor.
        /// </summary>
        public static void Write(string sourceFile, Exception exception)
        {
            try
            {
                string editorDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "Editor"));
                string debugPath = Path.Combine(editorDirectory, DebugFileName);
                string record = sourceFile + Environment.NewLine + exception + Environment.NewLine;
                File.AppendAllText(debugPath, record, Encoding.UTF8);
            }
            catch (Exception loggingException)
            {
                Debug.LogError("Unity Access could not write to debug.txt: " + loggingException);
            }
        }
    }
}
