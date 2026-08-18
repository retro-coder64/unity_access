using System;
using System.IO;
using UnityEngine;

namespace UnityAccess.Accessibility
{
    /// <summary>Records handled plugin failures without generating another Unity console message.</summary>
    internal static class ConsoleDiagnostics
    {
        private static readonly string DebugPath = Path.Combine(Application.dataPath, "Editor", "consol", "debug.txt");

        internal static void Record(string filename, Exception exception)
        {
            try
            {
                string record = $"{filename}{Environment.NewLine}{exception}{Environment.NewLine}";
                File.AppendAllText(DebugPath, record);
            }
            catch (IOException)
            {
                // Diagnostics must never cause a recursive log or interrupt editor use.
            }
            catch (UnauthorizedAccessException)
            {
                // A read-only project still retains all console functionality.
            }
        }
    }
}
