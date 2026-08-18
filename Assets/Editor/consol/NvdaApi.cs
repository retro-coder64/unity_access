using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace UnityAccess.Accessibility
{
    /// <summary>Connects the accessible console to the bundled NVDA Controller Client.</summary>
    public static class NvdaApi
    {
        private const int Success = 0;

        /// <summary>Gets whether the bundled controller can reach a running NVDA instance.</summary>
        public static bool IsRunning
        {
            get { return NativeMethods.TestIfRunning() == Success; }
        }

        /// <summary>Cancels old speech and sends the complete new message to NVDA.</summary>
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

        /// <summary>Declares the same bundled native functions used by the working plugin windows.</summary>
        private static class NativeMethods
        {
            [DllImport("nvdaControllerClient.dll", EntryPoint = "nvdaController_speakText", CharSet = CharSet.Unicode, ExactSpelling = true)]
            internal static extern int SpeakText(string text);

            [DllImport("nvdaControllerClient.dll", EntryPoint = "nvdaController_cancelSpeech", ExactSpelling = true)]
            internal static extern int CancelSpeech();

            [DllImport("nvdaControllerClient.dll", EntryPoint = "nvdaController_testIfRunning", ExactSpelling = true)]
            internal static extern int TestIfRunning();
        }
    }
}
