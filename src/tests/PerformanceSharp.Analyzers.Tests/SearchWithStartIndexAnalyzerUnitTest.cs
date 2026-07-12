// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1218SearchWithStartIndexAnalyzer,
    PerformanceSharp.Analyzers.Psh1218SearchWithStartIndexCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1218SearchWithStartIndexAnalyzer"/> (PSH1218 substring searches).</summary>
public class SearchWithStartIndexAnalyzerUnitTest
{
    /// <summary>Verifies a searched substring is flagged and sliced with AsSpan.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubstringIndexOfIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(string text, int start) => {|PSH1218:text.Substring(start)|}.IndexOf('a');
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(string text, int start) => text.AsSpan(start).IndexOf('a');
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix is offered when the numeric result is used as an index, because the basis does not move.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// This is the case the string <c>IndexOf(value, startIndex)</c> overload would have broken: its
    /// result is relative to the whole string, while the substring's — and the span's — is relative to
    /// the slice. Slicing with <c>AsSpan</c> keeps the value identical, miss (<c>-1</c>) included, so
    /// the offset never has to be reasoned about.
    /// </remarks>
    [Test]
    public async Task SubstringIndexOfUsedAsIndexIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public char M(string text, int start)
                                  {
                                      var offset = {|PSH1218:text.Substring(start)|}.IndexOf('a');
                                      return offset < 0 ? '\0' : text[start + offset];
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public char M(string text, int start)
                                       {
                                           var offset = text.AsSpan(start).IndexOf('a');
                                           return offset < 0 ? '\0' : text[start + offset];
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a found/not-found test over a searched substring is flagged and sliced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubstringIndexOfInBooleanTestIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string text, int start) => {|PSH1218:text.Substring(start)|}.IndexOf('a') >= 0;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(string text, int start) => text.AsSpan(start).IndexOf('a') >= 0;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a backward search over a substring is flagged and sliced, keeping the slice-relative result.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubstringLastIndexOfIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(string text, int start) => {|PSH1218:text.Substring(start)|}.LastIndexOf('a');
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(string text, int start) => text.AsSpan(start).LastIndexOf('a');
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a char containment test over a substring is flagged and sliced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubstringContainsCharIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string text, int start) => {|PSH1218:text.Substring(start)|}.Contains('a');
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(string text, int start) => text.AsSpan(start).Contains('a');
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an ordinal StartsWith over a substring is flagged and sliced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubstringStartsWithOrdinalIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string text, int start) => {|PSH1218:text.Substring(start)|}.StartsWith("ab", StringComparison.Ordinal);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(string text, int start) => text.AsSpan(start).StartsWith("ab", StringComparison.Ordinal);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an EndsWith carrying its own StringComparison is flagged and sliced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubstringEndsWithComparisonIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string text, int start) => {|PSH1218:text.Substring(start)|}.EndsWith("ab", StringComparison.OrdinalIgnoreCase);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(string text, int start) => text.AsSpan(start).EndsWith("ab", StringComparison.OrdinalIgnoreCase);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a culture-sensitive search is not reported — a span search is ordinal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CultureSensitiveIndexOfIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public int M(string text, int start) => text.Substring(start).IndexOf("ab");
            }
            """);

    /// <summary>Verifies a culture-sensitive StartsWith is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CultureSensitiveStartsWithIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public bool M(string text, int start) => text.Substring(start).StartsWith("ab");
            }
            """);

    /// <summary>Verifies a bounded substring is not reported — only the open-ended tail slice is rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BoundedSubstringIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public int M(string text, int start) => text.Substring(start, 2).IndexOf('a');
            }
            """);

    /// <summary>Verifies a substring used for more than the search is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReusedSubstringIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public string M(string text, int start)
                {
                    var tail = text.Substring(start);
                    return tail.IndexOf('a') >= 0 ? tail : string.Empty;
                }
            }
            """);

    /// <summary>Verifies a search inside an expression tree is not reported — a tree cannot hold a span.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SearchInsideExpressionTreeIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Linq.Expressions;

            public class C
            {
                public Expression<Func<string, bool>> M() => text => text.Substring(1).Contains('a');
            }
            """);

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
