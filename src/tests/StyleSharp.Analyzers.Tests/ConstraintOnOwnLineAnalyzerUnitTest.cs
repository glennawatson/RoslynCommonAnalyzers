// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConstraint = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1127ConstraintOnOwnLineAnalyzer,
    StyleSharp.Analyzers.Sst1127ConstraintOnOwnLineCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the constraint-on-own-line rule (SST1127).</summary>
public class ConstraintOnOwnLineAnalyzerUnitTest
{
    /// <summary>Verifies a constraint sharing the declaration line is reported (SST1127).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstraintSharingDeclarationLineReportedAsync()
        => await VerifyConstraint.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M<T>() {|SST1127:where T : class|}
                {
                }
            }
            """);

    /// <summary>Verifies a constraint on its own line is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstraintOnOwnLineIsCleanAsync()
        => await VerifyConstraint.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M<T>()
                    where T : class
                {
                }
            }
            """);

    /// <summary>Verifies a constraint sharing the declaration line is moved below the declaration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstraintSharingDeclarationLineIsMovedAsync()
    {
        const string Source = """
            internal class C
            {
                private static void M<T>() {|SST1127:where T : class|}
                {
                }
            }
            """;
        const string Fixed = """
            internal class C
            {
                private static void M<T>()
                    where T : class
                {
                }
            }
            """;

        await VerifyConstraint.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a second constraint sharing the first constraint line is moved onto its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstraintSharingPreviousConstraintLineIsMovedAsync()
    {
        const string Source = """
            internal class C
            {
                private static void M<T, U>()
                    where T : class {|SST1127:where U : struct|}
                {
                }
            }
            """;
        const string Fixed = """
            internal class C
            {
                private static void M<T, U>()
                    where T : class
                    where U : struct
                {
                }
            }
            """;

        await VerifyConstraint.VerifyCodeFixAsync(Source, Fixed);
    }

    /// <summary>Verifies Fix All moves every constraint clause onto its own line in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllMovesConstraintsOntoOwnLinesAsync()
    {
        const string Source = """
            internal class C<T, U> {|SST1127:where T : class|} {|SST1127:where U : struct|}
            {
                private static void M<TMethod>() {|SST1127:where TMethod : notnull|}
                {
                }
            }
            """;
        const string Fixed = """
            internal class C<T, U>
                where T : class
                where U : struct
            {
                private static void M<TMethod>()
                    where TMethod : notnull
                {
                }
            }
            """;

        await VerifyConstraint.VerifyCodeFixAsync(Source, Fixed);
    }
}
