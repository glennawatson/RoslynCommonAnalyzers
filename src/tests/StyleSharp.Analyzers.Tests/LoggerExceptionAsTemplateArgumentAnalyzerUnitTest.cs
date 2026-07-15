// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLogger = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.LoggerCallAnalyzer,
    StyleSharp.Analyzers.Sst2439ExceptionAsTemplateArgumentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2439 (an exception passed as a message value) and its fix.</summary>
public class LoggerExceptionAsTemplateArgumentAnalyzerUnitTest
{
    /// <summary>Verifies an exception passed as a value is hoisted into the exception argument.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExceptionValueIsHoistedAsync()
    {
        var source = LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, System.Exception ex)
                    => logger.LogError("failed {Ex}", {|SST2439:ex|});
            }
            """);
        var fixedSource = LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, System.Exception ex)
                    => logger.LogError(ex, "failed");
            }
            """);
        await VerifyLogger.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies a derived exception type is also hoisted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedExceptionValueIsHoistedAsync()
    {
        var source = LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, System.InvalidOperationException ex)
                    => logger.LogError("bad state {Error}", {|SST2439:ex|});
            }
            """);
        var fixedSource = LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, System.InvalidOperationException ex)
                    => logger.LogError(ex, "bad state");
            }
            """);
        await VerifyLogger.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies an exception already in the exception argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExceptionInItsArgumentIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, System.Exception ex)
                    => logger.LogError(ex, "failed");
            }
            """));

    /// <summary>Verifies a projection of an exception, which is not itself an exception, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExceptionMessageValueIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, System.Exception ex)
                    => logger.LogWarning("failed {Detail}", ex.Message);
            }
            """));
}
