// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyReadonlyStructMember = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1460ReadonlyStructMemberAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1460ReadonlyStructMemberAnalyzer"/>.</summary>
public class ReadonlyStructMemberAnalyzerUnitTest
{
    /// <summary>Verifies a non-mutating struct method is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonMutatingMethodIsReportedAsync()
        => await VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                public int {|SST1460:Value|}() => _value;
            }
            """);

    /// <summary>Verifies a method with a call is skipped because mutation cannot be cheaply proven away.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MethodWithCallIsCleanAsync()
        => await VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                public int Value() => GetValue();
                private readonly int GetValue() => 1;
            }
            """);

    /// <summary>Verifies a property returning a writable reference is skipped, since readonly would not compile.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RefReturningPropertyIsCleanAsync()
        => await VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                [System.Diagnostics.CodeAnalysis.UnscopedRef]
                public ref int Value => ref _value;
            }
            """);

    /// <summary>Verifies a method returning a writable reference is skipped, since readonly would not compile.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RefReturningMethodIsCleanAsync()
        => await VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                [System.Diagnostics.CodeAnalysis.UnscopedRef]
                public ref int Value() => ref _value;
            }
            """);

    /// <summary>Verifies a property returning a readonly reference is still reported, which stays compilable.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RefReadonlyReturningPropertyIsReportedAsync()
        => await VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                [System.Diagnostics.CodeAnalysis.UnscopedRef]
                public ref readonly int {|SST1460:Value|} => ref _value;
            }
            """);

    /// <summary>Verifies the rule stays silent below C# 8, where readonly instance members do not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp8Async()
    {
        const string Source = """
                              public struct Counter
                              {
                                  private int _value;

                                  public int Value() => _value;
                              }
                              """;
        var test = new VerifyReadonlyStructMember.Test { TestCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp7_3));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
