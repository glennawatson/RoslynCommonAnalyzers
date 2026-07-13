// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1222UseSpanBasedConcatAnalyzer,
    PerformanceSharp.Analyzers.Psh1222UseSpanBasedConcatCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1222UseSpanBasedConcatAnalyzer"/> (PSH1222 span-based concatenation).</summary>
public class UseSpanBasedConcatAnalyzerUnitTest
{
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
    [Test]
    public async Task ConcatWithoutSubstringIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public string M(string a, string b) => string.Concat(a, b);
            }
            """);

    /// <summary>Verifies the object overload is not reported — it has no span counterpart.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectOverloadIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public string M(string a, object b) => string.Concat(a.Substring(1), b);
            }
            """);

    /// <summary>Verifies a Substring on something other than a string is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStringSubstringIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ParamsOverloadIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ConcatInsideExpressionTreeIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Linq.Expressions;

            public class C
            {
                public Expression<Func<string, string>> M() => a => string.Concat(a.Substring(1), a);
            }
            """);

    /// <summary>
    /// Verifies the rule registers nothing against netstandard2.0, where the span overloads of
    /// <c>string.Concat</c> do not exist.
    /// </summary>
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
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
