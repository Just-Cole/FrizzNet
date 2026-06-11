using UnityEngine;

namespace FrizzNet.Logging
{
    /// <summary>
    /// Custom toggleable logger for FrizzNet framework.
    /// Provides categorized logs with clean prefixes.
    /// </summary>
    public static class FrizzLogger
    {
        /// <summary>
        /// Enable or disable FrizzNet logging.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Log informational messages.
        /// </summary>
        public static void LogInfo(string message)
        {
            if (!Enabled) return;
            Debug.Log($"<color=#39FF14>[FrizzNet]</color> <color=#00FFFF>[Info]</color> {message}");
        }

        /// <summary>
        /// Log network events (connecting, disconnecting, package transfers).
        /// </summary>
        public static void LogNetwork(string message)
        {
            if (!Enabled) return;
            Debug.Log($"<color=#39FF14>[FrizzNet]</color> <color=#FF00FF>[Network]</color> {message}");
        }

        /// <summary>
        /// Log warning messages.
        /// </summary>
        public static void LogWarning(string message)
        {
            if (!Enabled) return;
            Debug.LogWarning($"[FrizzNet] [Warning] {message}");
        }

        /// <summary>
        /// Log error messages.
        /// </summary>
        public static void LogError(string message)
        {
            if (!Enabled) return;
            Debug.LogError($"[FrizzNet] [Error] {message}");
        }
    }
}
