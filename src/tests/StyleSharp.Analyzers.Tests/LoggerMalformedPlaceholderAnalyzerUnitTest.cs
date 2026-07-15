// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLogger = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.LoggerCallAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2441 (a message template placeholder with no valid property name).</summary>
public class LoggerMalformedPlaceholderAnalyzerUnitTest
{
    /// <summary>Verifies an empty placeholder is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyPlaceholderIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger) => logger.LogInformation("a {|SST2441:{}|} b");
            }
            """));

    /// <summary>Verifies a whitespace-only placeholder is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WhitespacePlaceholderIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger) => logger.LogInformation("a {|SST2441:{ }|} b");
            }
            """));

    /// <summary>Verifies a placeholder whose name has a space is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpacedNameIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int count) => logger.LogInformation("{|SST2441:{Item Count}|}", count);
            }
            """));

    /// <summary>Verifies a well-formed named placeholder is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedPlaceholderIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int count) => logger.LogInformation("count {Count}", count);
            }
            """));

    /// <summary>Verifies a formatted or destructured named placeholder is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FormattedAndDestructuredPlaceholdersAreCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int count, object first, object second)
                    => logger.LogInformation("{Count:N0} {Count2,5} {@first} {$second}", count, count, first, second);
            }
            """));

    /// <summary>Verifies a numeric placeholder is left to the rule that owns positional placeholders.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NumericPlaceholderIsNotReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int count) => logger.LogInformation("count {0}", count);
            }
            """));

    /// <summary>Verifies escaped braces are not read as a placeholder.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EscapedBracesAreCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger) => logger.LogInformation("{{literal}} text");
            }
            """));

    /// <summary>Verifies a non-constant template is left to the concern that owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolatedTemplateIsNotReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int count) => logger.LogInformation($"a {count} b");
            }
            """));

    /// <summary>Verifies a malformed placeholder in a scope template is reported too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BeginScopeTemplateIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger) => logger.BeginScope("a {|SST2441:{}|} b");
            }
            """));
}
