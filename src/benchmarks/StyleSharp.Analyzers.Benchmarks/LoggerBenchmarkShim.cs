// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Supplies an in-source stand-in for the structured-logging abstractions the logging benchmarks resolve.</summary>
internal static class LoggerBenchmarkShim
{
    /// <summary>The logging abstraction declarations, in their real namespace.</summary>
    public const string Shim = """
        namespace Microsoft.Extensions.Logging
        {
            public enum LogLevel { Trace, Debug, Information, Warning, Error, Critical, None }

            public interface ILogger { bool IsEnabled(LogLevel logLevel); }

            public interface ILogger<out TCategoryName> : ILogger { }

            public interface ILoggerFactory { ILogger CreateLogger(string categoryName); }

            public static class LoggerFactoryExtensions
            {
                public static ILogger<T> CreateLogger<T>(this ILoggerFactory factory) => default!;
            }

            public static class LoggerExtensions
            {
                public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                public static void LogInformation(this ILogger logger, System.Exception exception, string message, params object[] args) { }
                public static void LogError(this ILogger logger, string message, params object[] args) { }
                public static void LogError(this ILogger logger, System.Exception exception, string message, params object[] args) { }
            }
        }
        """;
}
