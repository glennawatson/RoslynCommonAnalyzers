// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCallerInfoOrder = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2433CallerInfoParameterOrderAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2433 (a misplaced or defaulted caller-info parameter).</summary>
public class Sst2433CallerInfoParameterOrderAnalyzerUnitTest
{
    /// <summary>A caller-info parameter with no default value, which the compiler also rejects.</summary>
    private const string MissingDefaultSource = """
        using System.Runtime.CompilerServices;

        public sealed class Logger
        {
            public void Log([CallerMemberName] string {|SST2433:caller|})
            {
            }
        }
        """;

    /// <summary>Verifies a caller-info parameter followed by an ordinary parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallerInfoFollowedByOrdinaryIsReportedAsync()
        => await VerifyCallerInfoOrder.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public sealed class Logger
            {
                public void Write([CallerMemberName] string {|SST2433:caller|} = "", string message = "")
                {
                }
            }
            """);

    /// <summary>Verifies a caller-info parameter with no default value is reported, alongside the compiler's own error.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallerInfoWithoutDefaultIsReportedAsync()
        => await VerifyCallerInfoOrder.VerifyAnalyzerAsync(
            MissingDefaultSource,
            DiagnosticResult.CompilerError("CS4022").WithSpan(5, 22, 5, 38));

    /// <summary>Verifies a caller-argument-expression parameter out of place is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallerArgumentExpressionOutOfPlaceIsReportedAsync()
        => await VerifyCallerInfoOrder.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public sealed class Guard
            {
                public void Check(bool condition, [CallerArgumentExpression("condition")] string {|SST2433:expression|} = "", string message = "")
                {
                }
            }
            """);

    /// <summary>Verifies a misplaced caller-info parameter is reported on a constructor too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorCallerInfoFollowedByOrdinaryIsReportedAsync()
        => await VerifyCallerInfoOrder.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public sealed class Entry
            {
                public Entry([CallerMemberName] string {|SST2433:caller|} = "", string message = "")
                {
                }
            }
            """);

    /// <summary>Verifies a caller-info parameter that is last and has a default is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallerInfoLastWithDefaultIsCleanAsync()
        => await VerifyCallerInfoOrder.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public sealed class Logger
            {
                public void Write(string message, [CallerMemberName] string caller = "")
                {
                }
            }
            """);

    /// <summary>Verifies two caller-info parameters in a row at the end, each defaulted, are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConsecutiveCallerInfoAtEndIsCleanAsync()
        => await VerifyCallerInfoOrder.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public sealed class Logger
            {
                public void Write(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
                {
                }
            }
            """);

    /// <summary>Verifies an ordinary parameter carrying a non-caller-info attribute is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonCallerInfoAttributeIsCleanAsync()
        => await VerifyCallerInfoOrder.VerifyAnalyzerAsync(
            """
            using System.ComponentModel;

            public sealed class Logger
            {
                public void Write([Description("the message")] string message, string tag)
                {
                }
            }
            """);
}
