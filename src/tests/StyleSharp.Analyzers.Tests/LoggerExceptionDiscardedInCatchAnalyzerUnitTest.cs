// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLogger = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.LoggerCallAnalyzer,
    StyleSharp.Analyzers.Sst2438ExceptionDiscardedInCatchCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2438 (an error log in a catch that discards the caught exception) and its fix.</summary>
public class LoggerExceptionDiscardedInCatchAnalyzerUnitTest
{
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
    [Test]
    public async Task InformationLevelIsNotReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
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
    [Test]
    public async Task RethrowingCatchIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
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
    [Test]
    public async Task CatchWithoutVariableIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
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
    [Test]
    public async Task ExceptionAlreadyPassedIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
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
    [Test]
    public async Task ExceptionUsedElsewhereIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
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
