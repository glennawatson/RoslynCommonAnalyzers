// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Supplies a compact in-source stand-in for the structured-logging abstractions the logging rules resolve by
/// metadata name, so the logging tests are hermetic and need no package restore. The shapes the analyzers
/// probe — the logger interfaces, the extension methods with their exception-first overloads, and the factory
/// methods — are faithful; the bodies are inert.
/// </summary>
internal static class LoggingTestSource
{
    /// <summary>The logging abstraction declarations, in their real namespace.</summary>
    private const string Shim = """
        using Microsoft.Extensions.Logging;

        namespace Microsoft.Extensions.Logging
        {
            public enum LogLevel { Trace, Debug, Information, Warning, Error, Critical, None }

            public readonly struct EventId
            {
                public EventId(int id, string name = null) { }
            }

            public interface ILogger
            {
                bool IsEnabled(LogLevel logLevel);
            }

            public interface ILogger<out TCategoryName> : ILogger { }

            public interface ILoggerFactory
            {
                ILogger CreateLogger(string categoryName);
            }

            public static class LoggerFactoryExtensions
            {
                public static ILogger<T> CreateLogger<T>(this ILoggerFactory factory) => default!;
                public static ILogger CreateLogger(this ILoggerFactory factory, System.Type type) => default!;
            }

            public static class LoggerExtensions
            {
                public static void LogTrace(this ILogger logger, string message, params object[] args) { }
                public static void LogTrace(this ILogger logger, System.Exception exception, string message, params object[] args) { }
                public static void LogDebug(this ILogger logger, string message, params object[] args) { }
                public static void LogDebug(this ILogger logger, System.Exception exception, string message, params object[] args) { }
                public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                public static void LogInformation(this ILogger logger, System.Exception exception, string message, params object[] args) { }
                public static void LogWarning(this ILogger logger, string message, params object[] args) { }
                public static void LogWarning(this ILogger logger, System.Exception exception, string message, params object[] args) { }
                public static void LogError(this ILogger logger, string message, params object[] args) { }
                public static void LogError(this ILogger logger, System.Exception exception, string message, params object[] args) { }
                public static void LogCritical(this ILogger logger, string message, params object[] args) { }
                public static void LogCritical(this ILogger logger, System.Exception exception, string message, params object[] args) { }
                public static void Log(this ILogger logger, LogLevel logLevel, string message, params object[] args) { }
                public static void Log(this ILogger logger, LogLevel logLevel, System.Exception exception, string message, params object[] args) { }
                public static System.IDisposable BeginScope(this ILogger logger, string messageFormat, params object[] args) => default!;
            }
        }

        """;

    /// <summary>Prepends the logging shim to a snippet of user code.</summary>
    /// <param name="code">The user code, in the global namespace.</param>
    /// <returns>The combined source.</returns>
    public static string Wrap(string code) => Shim + "\n" + code;
}
