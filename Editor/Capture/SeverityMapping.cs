using System;
using ClarityConsole.Core;
using UnityEngine;

namespace ClarityConsole.Capture
{
    /// <summary>Translates the engine's <see cref="LogType"/> into the engine-free <see cref="LogSeverity"/>.</summary>
    internal static class SeverityMapping
    {
        public static LogSeverity FromLogType(LogType logType)
        {
            switch (logType)
            {
                case LogType.Log:
                    return LogSeverity.Log;
                case LogType.Warning:
                    return LogSeverity.Warning;
                case LogType.Error:
                    return LogSeverity.Error;
                case LogType.Exception:
                    return LogSeverity.Exception;
                case LogType.Assert:
                    return LogSeverity.Assert;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logType), logType, "Unknown LogType.");
            }
        }
    }
}
