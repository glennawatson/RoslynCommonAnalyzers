// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyLogger = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.LoggerCallAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2441 (a message template placeholder with no valid property name).</summary>
public class LoggerMalformedPlaceholderAnalyzerUnitTest
{
    /// <summary>Verifies an unresolved logging method is left to the compiler.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnresolvedLogCallIsCleanAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap("""
            class C { void M(ILogger logger) { logger.{|CS1061:LogMissing|}("{}"); } }
            """));

    /// <summary>Verifies conditional and static calls still locate malformed template spans.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConditionalAndStaticCallsAreAnalyzedAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap("""
            class C
            {
                void M(ILogger logger)
                {
                    logger?.LogInformation("{|SST2441:{}|}", 1);
                    LoggerExtensions.LogInformation(logger, "{|SST2441:{}|}", 1);
                }
            }
            """));

    /// <summary>Verifies malformed-shaped calls must resolve to a supported template signature.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnsupportedLoggingSignaturesAreCleanAsync() =>
        VerifyLogger.VerifyAnalyzerAsync("""
            using Microsoft.Extensions.Logging;
            namespace Microsoft.Extensions.Logging
            {
                public interface ILogger { }
                public static class LoggerExtensions
                {
                    public static int Value;
                    public static void LogPlain(this ILogger logger, string message) { }
                    public static void LogOnly(params object[] args) { }
                    public static void LogNumber(this ILogger logger, int number, params object[] args) { }
                    public static void LogTemplate(this ILogger logger, string message, params object[] args) { }
                    public static void LogOptional(this ILogger logger, string prefix, string message = "", params object[] args) { }
                }
            }
            class C
            {
                void M(ILogger logger, string template)
                {
                    logger.LogPlain("{}");
                    LoggerExtensions.LogOnly("{}");
                    logger.LogNumber(1, "{}");
                    logger.LogTemplate(template, "{}");
                    logger.LogTemplate(null, "{}");
                    logger.LogOptional("prefix");
                    System.Action<string> action = _ => { };
                    ((System.Action<string>)action)("{}");
                }
            }
            """);

    /// <summary>Verifies logger-shaped calls stay silent when logging metadata is unavailable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MissingLoggingMetadataIsCleanAsync() =>
        VerifyLogger.VerifyAnalyzerAsync("""
            class C
            {
                void Log(string template, params object[] values) { }
                void M() { Log("{}", 1); }
            }
            """);

    /// <summary>Verifies an empty placeholder is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptyPlaceholderIsReportedAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger) => logger.LogInformation("a {|SST2441:{}|} b");
            }
            """));

    /// <summary>Verifies a whitespace-only placeholder is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WhitespacePlaceholderIsReportedAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger) => logger.LogInformation("a {|SST2441:{ }|} b");
            }
            """));

    /// <summary>Verifies a placeholder whose name has a space is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SpacedNameIsReportedAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int count) => logger.LogInformation("{|SST2441:{Item Count}|}", count);
            }
            """));

    /// <summary>Verifies a well-formed named placeholder is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamedPlaceholderIsCleanAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int count) => logger.LogInformation("count {Count}", count);
            }
            """));

    /// <summary>Verifies a formatted or destructured named placeholder is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FormattedAndDestructuredPlaceholdersAreCleanAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int count, object first, object second)
                    => logger.LogInformation("{Count:N0} {Count2,5} {@first} {$second}", count, count, first, second);
            }
            """));

    /// <summary>Verifies a numeric placeholder is left to the rule that owns positional placeholders.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NumericPlaceholderIsNotReportedAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int count) => logger.LogInformation("count {0}", count);
            }
            """));

    /// <summary>Verifies escaped braces are not read as a placeholder.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EscapedBracesAreCleanAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger) => logger.LogInformation("{{literal}} text");
            }
            """));

    /// <summary>Verifies a non-constant template is left to the concern that owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterpolatedTemplateIsNotReportedAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int count) => logger.LogInformation($"a {count} b");
            }
            """));

    /// <summary>Verifies a malformed placeholder in a scope template is reported too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BeginScopeTemplateIsReportedAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger) => logger.BeginScope("a {|SST2441:{}|} b");
            }
            """));
}
