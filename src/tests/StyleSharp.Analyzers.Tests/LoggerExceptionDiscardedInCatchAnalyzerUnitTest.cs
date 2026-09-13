// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using VerifyLogger = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.LoggerCallAnalyzer,
    StyleSharp.Analyzers.Sst2438ExceptionDiscardedInCatchCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2438 (an error log in a catch that discards the caught exception) and its fix.</summary>
public class LoggerExceptionDiscardedInCatchAnalyzerUnitTest
{
    /// <summary>Verifies extension names without a known log level do not imply a discarded exception.</summary>
    /// <param name="method">The custom logging extension name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("LogFail")]
    [Arguments("LogAudit")]
    [Arguments("LogVerbose")]
    [Arguments("LogSecurity")]
    [Arguments("LogApplication")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnknownLogLevelsAreCleanAsync(string method) =>
        VerifyLogger.VerifyAnalyzerAsync($$"""
            using Microsoft.Extensions.Logging;
            namespace Microsoft.Extensions.Logging
            {
                public interface ILogger { }
                public static class LoggerExtensions
                {
                    public static void {{method}}(this ILogger logger, string message, params object[] args) { }
                }
            }
            class C
            {
                void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception ex) { logger.{{method}}("failure"); }
                }
            }
            """);

    /// <summary>Verifies top-level logging has no enclosing caught exception to preserve.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TopLevelLogIsCleanAsync()
    {
        var test = new VerifyLogger.Test
        {
            TestCode = """
                using Microsoft.Extensions.Logging;
                ILogger logger = null;
                logger.LogError("failure");
                namespace Microsoft.Extensions.Logging
                {
                    public interface ILogger { }
                    public static class LoggerExtensions
                    {
                        public static void LogError(this ILogger logger, string message, params object[] args) { }
                        public static void LogError(this ILogger logger, System.Exception error, string message, params object[] args) { }
                    }
                }
                """,
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
            solution.WithProjectCompilationOptions(projectId, solution.GetProject(projectId)!.CompilationOptions!.WithOutputKind(OutputKind.ConsoleApplication)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies general Log calls need a known level and a catch in the same function.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DynamicLevelsAndNestedFunctionsAreCleanAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap("""
            class C
            {
                void M(ILogger logger, LogLevel level)
                {
                    try { }
                    catch (System.Exception ex)
                    {
                        logger.Log(level, "failure");
                        System.Action action = () => logger.LogError("failure");
                        void Local() { logger.LogError("failure"); }
                    }
                }
            }
            """));

    /// <summary>Verifies constant levels and degraded exception arguments without placeholders are reported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task GeneralLogAndUnmappedProjectionAreReportedAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap("""
            class C
            {
                void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception ex)
                    {
                        logger.{|SST2438:Log|}(LogLevel.Error, "failure", ex.Message);
                        logger.{|SST2438:LogError|}("failure", 1, ex.Message, ex.StackTrace);
                    }
                }
            }
            """));

    /// <summary>Verifies the configured floor controls diagnostics at every supported logging level.</summary>
    /// <param name="level">The configured minimum level.</param>
    /// <param name="floor">The first reportable ordinal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("trace", 0)]
    [Arguments("debug", 1)]
    [Arguments("information", 2)]
    [Arguments("warning", 3)]
    [Arguments("error", 4)]
    [Arguments("critical", 5)]
    [Arguments("unknown", 4)]
    public async Task ConfiguredMinimumLevelControlsReportingAsync(string level, int floor)
    {
        string[] methods = ["LogTrace", "LogDebug", "LogInformation", "LogWarning", "LogError", "LogCritical"];
        var statements = methods.Select((method, index) =>
            index >= floor ? $"logger.{{|SST2438:{method}|}}(\"operation failed\");" : $"logger.{method}(\"operation failed\");");
        var source = LoggingTestSource.Wrap($$"""
            class C
            {
                public void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception ex)
                    {
                        {{string.Join("\n", statements)}}
                    }
                }
            }
            """);
        var test = new VerifyLogger.Test { TestCode = source };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $"root = true\n[*.cs]\nstylesharp.SST2438.minimum_level = {level}\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an error log that never mentions the caught exception gets it passed in.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnreferencedExceptionIsPassedAsync()
    {
        var source = LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception ex)
                    {
                        logger.{|SST2438:LogError|}("failed to process");
                    }
                }
            }
            """);
        var fixedSource = LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception ex)
                    {
                        logger.LogError(ex, "failed to process");
                    }
                }
            }
            """);
        await VerifyLogger.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies a degraded projection of the exception is replaced by the exception itself.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DegradedProjectionIsReplacedAsync()
    {
        var source = LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception ex)
                    {
                        logger.{|SST2438:LogError|}("failed: {Message}", ex.Message);
                    }
                }
            }
            """);
        var fixedSource = LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception ex)
                    {
                        logger.LogError(ex, "failed:");
                    }
                }
            }
            """);
        await VerifyLogger.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies a log below the error floor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InformationLevelIsNotReportedAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception ex)
                    {
                        logger.LogInformation("failed to process");
                    }
                }
            }
            """));

    /// <summary>Verifies a catch that rethrows is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RethrowingCatchIsCleanAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception ex)
                    {
                        logger.LogError("failed to process");
                        throw;
                    }
                }
            }
            """));

    /// <summary>Verifies a catch that names no exception variable is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CatchWithoutVariableIsCleanAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception)
                    {
                        logger.LogError("failed to process");
                    }
                }
            }
            """));

    /// <summary>Verifies a log already passing the exception is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExceptionAlreadyPassedIsCleanAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception ex)
                    {
                        logger.LogError(ex, "failed to process");
                    }
                }
            }
            """));

    /// <summary>Verifies an exception used elsewhere in the catch is not treated as discarded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExceptionUsedElsewhereIsCleanAsync() =>
        VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                private System.Exception _last;

                public void M(ILogger logger)
                {
                    try { }
                    catch (System.Exception ex)
                    {
                        _last = ex;
                        logger.LogError("failed to process");
                    }
                }
            }
            """));
}
