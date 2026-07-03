// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyRedundantInterpolation = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1205RedundantInterpolatedStringAnalyzer,
    PerformanceSharp.Analyzers.Psh1205RedundantInterpolatedStringCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1205 (remove interpolation that does no work) and its code fix.</summary>
public class RedundantInterpolatedStringAnalyzerUnitTest
{
    /// <summary>Verifies a single bare string hole is reported (PSH1205) and replaced by the value itself.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleStringHoleReplacedWithValueAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(string name) => {|PSH1205:$"{name}"|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(string name) => name;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a hole whose expression is not a string is not reported (ToString semantics differ).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectHoleIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public string M(object value) => $"{value}";
            }
            """);

    /// <summary>Verifies a text-only interpolated string is reported and replaced by a plain literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TextOnlyInterpolatedStringReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M() => {|PSH1205:$"hello"|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M() => "hello";
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an empty interpolated string is reported and replaced by an empty literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyInterpolatedStringReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M() => {|PSH1205:$""|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M() => "";
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a hole with a format clause is not reported — the interpolation does formatting work.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FormatClauseIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public string M(string s) => $"{s:F2}";
            }
            """);

    /// <summary>Verifies a hole with an alignment clause is not reported — the interpolation does padding work.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlignmentClauseIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public string M(string s) => $"{s,10}";
            }
            """);

    /// <summary>Verifies mixed literal text and a hole is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedTextAndHoleIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public string M(string s) => $"a{s}";
            }
            """);

    /// <summary>Verifies escaped braces are unescaped in the replacement literal's value.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EscapedBracesReplacedWithUnescapedLiteralAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M() => {|PSH1205:$"{{x}}"|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M() => "{x}";
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an interpolated string converted to <c>FormattableString</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FormattableStringTargetIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public System.FormattableString M(string s)
                {
                    System.FormattableString f = $"{s}";
                    return f;
                }
            }
            """);

    /// <summary>Verifies a conditional hole keeps its parentheses so the replacement stays well-formed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalHoleReplacedWithParenthesesAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(bool flag, string a, string b) => {|PSH1205:$"{(flag ? a : b)}"|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(bool flag, string a, string b) => (flag ? a : b);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a binary hole expression is parenthesized when it stands alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BinaryHoleGetsParenthesesAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(string a, string b) => {|PSH1205:$"{a + b}"|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(string a, string b) => (a + b);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyRedundantInterpolation.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90CleanAsync(string source)
        => await VerifyNet90Async(source, source);
}
