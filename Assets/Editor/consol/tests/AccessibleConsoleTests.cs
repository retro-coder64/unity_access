using NUnit.Framework;
using UnityEngine;
using UnityAccess.Console;

namespace UnityAccess.Tests
{
    /// <summary>Verifies the immutable representation used by the accessible console.</summary>
    public sealed class AccessibleConsoleTests
    {
        [Test]
        public void CopyText_IncludesMessageAndStackTrace()
        {
            ConsoleLogEntry entry = new ConsoleLogEntry("Failure", "Example stack", LogType.Error);

            Assert.That(entry.CopyText, Does.Contain("Failure"));
            Assert.That(entry.CopyText, Does.Contain("Example stack"));
        }

        [TestCase(LogType.Error)]
        [TestCase(LogType.Assert)]
        [TestCase(LogType.Exception)]
        public void ErrorTypes_UseRedText(LogType type)
        {
            Assert.That(AccessibleConsoleWindow.GetLogColor(type), Is.EqualTo(Color.red));
        }

        [Test]
        public void Warning_UsesOrangeText()
        {
            Color warning = AccessibleConsoleWindow.GetLogColor(LogType.Warning);

            Assert.That(warning.r, Is.EqualTo(1f));
            Assert.That(warning.g, Is.EqualTo(0.65f));
            Assert.That(warning.b, Is.EqualTo(0f));
        }

        [Test]
        public void AccessibleText_IncludesTypeAndMessage()
        {
            ConsoleLogEntry entry = new ConsoleLogEntry("Careful", string.Empty, LogType.Warning);

            Assert.That(entry.AccessibleText, Is.EqualTo("Warning. Careful"));
        }
    }
}
