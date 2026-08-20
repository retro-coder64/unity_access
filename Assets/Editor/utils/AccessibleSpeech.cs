using System;

namespace UnityAccess
{
    /// <summary>Provides the single safe speech entry point used by accessible editor features.</summary>
    public static class AccessibleSpeech
    {
        public static void Speak(string message, string sourceFile)
        {
            try
            {
                NvdaApi.Speak(message);
            }
            catch (Exception exception)
            {
                PluginErrorLog.Write(sourceFile, exception);
            }
        }
    }
}
