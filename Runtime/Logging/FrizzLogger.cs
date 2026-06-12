using UnityEngine;

namespace FrizzNet.Logging
{
    /// <summary>
    /// Log levels filtering FrizzNet console output severity.
    /// </summary>
    public enum FrizzLogLevel
    {
        None = 0,
        Errors = 1,
        Warnings = 2,
        Info = 3,
        Verbose = 4
    }

    /// <summary>
    /// Custom toggleable logger for FrizzNet framework.
    /// Provides categorized logs with clean prefixes.
    /// </summary>
    public static class FrizzLogger
    {
        /// <summary>
        /// Current global logging level limit.
        /// </summary>
        public static FrizzLogLevel CurrentLogLevel { get; set; } = FrizzLogLevel.Info;

        /// <summary>
        /// Enable or disable FrizzNet logging (legacy backward compatibility wrapper).
        /// </summary>
        public static bool Enabled
        {
            get => CurrentLogLevel != FrizzLogLevel.None;
            set => CurrentLogLevel = value ? FrizzLogLevel.Info : FrizzLogLevel.None;
        }

        /// <summary>
        /// Log informational messages.
        /// </summary>
        public static void LogInfo(string message)
        {
            if (CurrentLogLevel < FrizzLogLevel.Info) return;
            Debug.Log($"<color=#39FF14>[FrizzNet]</color> <color=#00FFFF>[Info]</color> {message}");
        }

        /// <summary>
        /// Log network events (connecting, disconnecting, package transfers).
        /// </summary>
        public static void LogNetwork(string message)
        {
            if (CurrentLogLevel < FrizzLogLevel.Verbose) return;
            Debug.Log($"<color=#39FF14>[FrizzNet]</color> <color=#FF00FF>[Network]</color> {message}");
        }

        /// <summary>
        /// Log warning messages.
        /// </summary>
        public static void LogWarning(string message)
        {
            if (CurrentLogLevel < FrizzLogLevel.Warnings) return;
            Debug.LogWarning($"[FrizzNet] [Warning] {message}");
        }

        /// <summary>
        /// Log error messages.
        /// </summary>
        public static void LogError(string message)
        {
            if (CurrentLogLevel < FrizzLogLevel.Errors) return;
            Debug.LogError($"[FrizzNet] [Error] {message}");
        }
    }
}
