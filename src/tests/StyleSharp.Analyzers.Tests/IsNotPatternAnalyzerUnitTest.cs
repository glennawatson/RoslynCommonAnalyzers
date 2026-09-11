// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;

using VerifyIsNotPattern = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2008IsNotPatternAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2008IsNotPatternAnalyzer"/>.</summary>
public class IsNotPatternAnalyzerUnitTest
{
    /// <summary>Verifies a negated null pattern is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NegatedPatternIsReportedAsync() =>
        VerifyIsNotPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object value) => {|SST2008:!(value is null)|};
            }
            """);

    /// <summary>Verifies a negated declaration pattern is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A name bound under a <c>not</c> is assigned on exactly the branch it was assigned on before, so the
    /// early-return form this produces compiles and reads the same.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NegatedDeclarationPatternIsReportedAsync() =>
        VerifyIsNotPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object value)
                {
                    if ({|SST2008:!(value is string text)|})
                    {
                        return 0;
                    }

                    return text.Length;
                }
            }
            """);

    /// <summary>Verifies a negated recursive pattern that binds a name is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NegatedRecursivePatternWithDesignationIsReportedAsync() =>
        VerifyIsNotPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object value)
                {
                    if ({|SST2008:!(value is string { Length: > 0 } text)|})
                    {
                        return 0;
                    }

                    return text.Length;
                }
            }
            """);

    /// <summary>Verifies a <c>var</c> pattern is skipped because nothing can fail to match it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A <c>var</c> pattern accepts every value, so the negated form is unsatisfiable and the compiler
    /// rejects it outright.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NegatedVarPatternIsCleanAsync() =>
        VerifyIsNotPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object value) => !(value is var text) || text is null;
            }
            """);

    /// <summary>Verifies a <c>var</c> reached through a subpattern does not withhold the report.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The enclosing pattern can still fail, so negating the whole thing remains satisfiable.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VarInsideASubpatternIsStillReportedAsync() =>
        VerifyIsNotPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(object value)
                {
                    if ({|SST2008:!(value is string { Length: var length })|})
                    {
                        return 0;
                    }

                    return length;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent below C# 9, where the <c>is not</c> pattern the fix emits does not exist.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SilentBelowCSharp9Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(object value) => !(value is null);
                              }
                              """;
        var test = new VerifyIsNotPattern.Test { TestCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp8));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
