// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLogger = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.LoggerCallAnalyzer,
    StyleSharp.Analyzers.Sst2440TransposedTemplateArgumentsCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2440 (log values ordered against the placeholders they name) and its fix.</summary>
public class LoggerTransposedTemplateArgumentsAnalyzerUnitTest
{
    /// <summary>Verifies a two-way swap is reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TransposedValuesAreSwappedBackAsync()
    {
        var source = LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int first, int second)
                    => logger.LogInformation("{First} {Second}", {|SST2440:second|}, first);
            }
            """);
        var fixedSource = LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int first, int second)
                    => logger.LogInformation("{First} {Second}", first, second);
            }
            """);
        await VerifyLogger.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies a swap detected through simple member accesses is reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TransposedMemberAccessesAreSwappedBackAsync()
    {
        var source = LoggingTestSource.Wrap(
            """
            public sealed class Order { public int Id; public string Name; }

            public sealed class C
            {
                public void M(ILogger logger, Order order)
                    => logger.LogInformation("{Id} {Name}", {|SST2440:order.Name|}, order.Id);
            }
            """);
        var fixedSource = LoggingTestSource.Wrap(
            """
            public sealed class Order { public int Id; public string Name; }

            public sealed class C
            {
                public void M(ILogger logger, Order order)
                    => logger.LogInformation("{Id} {Name}", order.Id, order.Name);
            }
            """);
        await VerifyLogger.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies correctly ordered values are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CorrectOrderIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int first, int second)
                    => logger.LogInformation("{First} {Second}", first, second);
            }
            """));

    /// <summary>Verifies a three-way rotation, which has no unambiguous repair, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RotationIsNotReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int first, int second, int third)
                    => logger.LogInformation("{First} {Second} {Third}", second, third, first);
            }
            """));

    /// <summary>Verifies a computed value is not read as a name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComputedValueIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public int Get(int value) => value;

                public void M(ILogger logger, int first, int second)
                    => logger.LogInformation("{First} {Second}", Get(second), first);
            }
            """));

    /// <summary>Verifies a value in its own placeholder's slot is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueInItsOwnSlotIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int first, int other)
                    => logger.LogInformation("{First} {Second}", first, other);
            }
            """));
}
