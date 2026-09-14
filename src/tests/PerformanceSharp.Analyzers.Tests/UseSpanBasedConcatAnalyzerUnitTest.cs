// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1222UseSpanBasedConcatAnalyzer,
    PerformanceSharp.Analyzers.Psh1222UseSpanBasedConcatCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1222UseSpanBasedConcatAnalyzer"/> (PSH1222 span-based concatenation).</summary>
public class UseSpanBasedConcatAnalyzerUnitTest
{
    /// <summary>Checks the syntax gate before symbols or framework support are inspected.</summary>
    /// <param name="expression">The candidate invocation.</param>
    /// <param name="expected">Whether the invocation has a positional sliced concatenation shape.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string.Concat(a.Substring(1), b)", true)]
    [Arguments("string.Concat(a, b.Substring(1, 2))", true)]
    [Arguments("string.Concat(a.Substring(1))", false)]
    [Arguments("string.Concat(a.Substring(1), b, c, d, e)", false)]
    [Arguments("Concat(a.Substring(1), b)", false)]
    [Arguments("p->Concat(a.Substring(1), b)", false)]
    [Arguments("string.Join(a.Substring(1), b)", false)]
    [Arguments("string.Concat(a, b)", false)]
    [Arguments("string.Concat(a.Substring(), b)", false)]
    [Arguments("string.Concat(a.Substring(1, 2, 3), b)", false)]
    [Arguments("string.Concat(Substring(1), b)", false)]
    [Arguments("string.Concat(p->Substring(1), b)", false)]
    [Arguments("string.Concat(a.Trim(1), b)", false)]
    [Arguments("string.Concat(str0: a.Substring(1), str1: b)", false)]
    [Arguments("string.Concat(a.Substring(1), ref b)", false)]
    public async Task ConcatShapeRequiresAnUnmodifiedSubstringArgumentAsync(string expression, bool expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(expression);

        await Assert.That(Psh1222UseSpanBasedConcatAnalyzer.IsConcatShape(invocation)).IsEqualTo(expected);
    }

    /// <summary>Checks slice lookup scans all arguments and clears the result when no slice exists.</summary>
    /// <param name="expression">The candidate invocation.</param>
    /// <param name="expected">The first substring expression, if any.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string.Concat()", null)]
    [Arguments("string.Concat(a, b)", null)]
    [Arguments("string.Concat(a, b.Substring(1), c.Substring(2))", "b.Substring(1)")]
    public async Task FirstSubstringLookupPreservesArgumentOrderAsync(string expression, string? expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(expression);

        await Assert.That(Psh1222UseSpanBasedConcatAnalyzer.FindFirstSubstring(invocation)?.ToString()).IsEqualTo(expected);
    }

    /// <summary>Checks member access is appended without changing the operand's precedence.</summary>
    /// <param name="operand">The unsliced concatenation argument.</param>
    /// <param name="expected">The rewritten argument.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value", "value.AsSpan()")]
    [Arguments("this.Value", "this.Value.AsSpan()")]
    [Arguments("Get()", "Get().AsSpan()")]
    [Arguments("items[0]", "items[0].AsSpan()")]
    [Arguments("\"x\"", "\"x\".AsSpan()")]
    [Arguments("(value)", "(value).AsSpan()")]
    [Arguments("this", "this.AsSpan()")]
    [Arguments("flag ? a : b", "(flag ? a : b).AsSpan()")]
    [Arguments("a ?? b", "(a ?? b).AsSpan()")]
    public async Task SpanRewritePreservesOperandPrecedenceAsync(string operand, string expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression($"string.Concat(a.Substring(1), {operand})");
        var rewritten = Psh1222UseSpanBasedConcatAnalyzer.BuildSpanConcat(invocation);

        await Assert.That(rewritten.ArgumentList.Arguments[1].ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies the four-argument overload rewrites a later slice and a conditional value.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task FourArgumentConcatWithConditionalIsFixedAsync() =>
        VerifyAsync(
            "using System; class C { string M(string a, string b, bool flag) => {|PSH1222:string.Concat(a, b.Substring(1), flag ? a : b, a)|}; }",
            "using System; class C { string M(string a, string b, bool flag) => string.Concat(a.AsSpan(), b.AsSpan(1), (flag ? a : b).AsSpan(), a.AsSpan()); }");

    /// <summary>Verifies unresolved or foreign methods cannot produce a span recommendation.</summary>
    /// <param name="expression">The invocation with an unsafe binding.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Missing.Concat(a.Substring(1), b)")]
    [Arguments("helper.Concat(a.Substring(1), b)")]
    [Arguments("C.Concat<string>(a.Substring(1), b)")]
    [Arguments("C.Concat(a.Substring(1), b)")]
    [Arguments("string.Concat(a.Substring(1), missing)")]
    [Arguments("string.Concat(a.Substring(1), (object)b, (object)a, (object)b)")]
    [Arguments("string.Concat(C.Substring(1), b)")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnrelatedConcatBindingsAreSilentAsync(string expression) =>
        new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = $$"""
                using System;
                class Helper { public string Concat(string a, string b) => a; }
                class C
                {
                    static string Concat(string a, string b) => a;
                    static string Concat<T>(string a, string b) => a;
                    static string Substring(int start) => "";
                    string M(string a, string b, Helper helper) => {{expression}};
                }
                """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies the recommendation requires extension methods that bind to spans in the current scope.</summary>
    /// <param name="imports">The extension-method scope to expose.</param>
    /// <param name="declaration">A custom extension that may shadow the framework method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "")]
    [Arguments("using System;", """
        static class Extensions
        {
            public static string AsSpan(this string value) => value;
            public static string AsSpan(this string value, int start) => value;
        }
        """)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnavailableOrShadowedSpanRewriteIsSilentAsync(string imports, string declaration) =>
        VerifyAsync($"{imports} {declaration} class C {{ string M(string a, string b) => string.Concat(a.Substring(1), b); }}");

    /// <summary>Verifies a concatenated substring is flagged and every argument moves to a span.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenatedSubstringIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public string M(string a, string b) => {|PSH1222:string.Concat(a.Substring(1), b)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public string M(string a, string b) => string.Concat(a.AsSpan(1), b.AsSpan());
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the two-argument Substring form carries both of its arguments across.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StartAndLengthSubstringIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public string M(string a, string b) => {|PSH1222:string.Concat(a.Substring(1, 3), b)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public string M(string a, string b) => string.Concat(a.AsSpan(1, 3), b.AsSpan());
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a three-argument concatenation with two slices is flagged and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreeArgumentConcatIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public string M(string a, string b) => {|PSH1222:string.Concat(a.Substring(1), "-", b.Substring(2))|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public string M(string a, string b) => string.Concat(a.AsSpan(1), "-".AsSpan(), b.AsSpan(2));
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a concatenation with no slice at all is not reported — there is nothing to save.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConcatWithoutSubstringIsCleanAsync() =>
        VerifyAsync(
            """
            using System;

            public class C
            {
                public string M(string a, string b) => string.Concat(a, b);
            }
            """);

    /// <summary>Verifies the object overload is not reported — it has no span counterpart.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ObjectOverloadIsCleanAsync() =>
        VerifyAsync(
            """
            using System;

            public class C
            {
                public string M(string a, object b) => string.Concat(a.Substring(1), b);
            }
            """);

    /// <summary>Verifies a Substring on something other than a string is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonStringSubstringIsCleanAsync() =>
        VerifyAsync(
            """
            using System;

            public class Buffer
            {
                public string Substring(int start) => "x";
            }

            public class C
            {
                public string M(Buffer buffer, string b) => string.Concat(buffer.Substring(1), b);
            }
            """);

    /// <summary>Verifies the params overload is not reported — it has no span counterpart either.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ParamsOverloadIsCleanAsync() =>
        VerifyAsync(
            """
            using System;

            public class C
            {
                public string M(string a, string b, string c, string d, string e)
                    => string.Concat(a.Substring(1), b, c, d, e);
            }
            """);

    /// <summary>Verifies a concatenation inside an expression tree is not rewritten — a tree cannot hold a span.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConcatInsideExpressionTreeIsCleanAsync() =>
        VerifyAsync(
            """
            using System;
            using System.Linq.Expressions;

            public class C
            {
                public Expression<Func<string, string>> M() => a => string.Concat(a.Substring(1), a);
            }
            """);

    /// <summary>Verifies the rule registers nothing against netstandard2.0, where the span overloads of <c>string.Concat</c> do not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The reported code compiles perfectly well on netstandard2.0 — it is the <i>suggestion</i> that
    /// does not exist there. <c>string.Concat(ReadOnlySpan&lt;char&gt;, ReadOnlySpan&lt;char&gt;)</c>
    /// arrived with .NET Core 2.1, so the rule probes <see cref="string"/>'s member list at compilation
    /// start and registers no syntax action at all when the overload is missing.
    /// </remarks>
    [Test]
    public async Task NetStandard20IsSilentAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = """
                       public class C
                       {
                           public string M(string a, string b) => string.Concat(a.Substring(1), b);
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
