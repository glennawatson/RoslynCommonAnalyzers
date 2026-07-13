// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1221UseStartsWithOverIndexOfAnalyzer,
    PerformanceSharp.Analyzers.Psh1221UseStartsWithOverIndexOfCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1221UseStartsWithOverIndexOfAnalyzer"/> (PSH1221 prefix tests).</summary>
public class UseStartsWithOverIndexOfAnalyzerUnitTest
{
    /// <summary>Verifies the ordinal char form is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CharIndexOfIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string text) => {|PSH1221:text.IndexOf('/') == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string text) => text.StartsWith('/');
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the not-equal form is rewritten as a negated prefix test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NotEqualZeroIsFlaggedAndNegatedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string text) => {|PSH1221:text.IndexOf('/') != 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string text) => !text.StartsWith('/');
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the comparison is matched with the zero on either side.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroOnTheLeftIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string text) => {|PSH1221:0 == text.IndexOf('/')|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string text) => text.StartsWith('/');
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an explicit StringComparison is carried across to StartsWith unchanged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// This is the shape that proves the rule preserves the comparison rather than picking one: the
    /// very same <c>StringComparison</c> argument moves to the prefix test, so an ordinal search stays
    /// ordinal and a culture-sensitive one stays culture-sensitive.
    /// </remarks>
    [Test]
    public async Task OrdinalStringComparisonIsCarriedAcrossAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string text) => {|PSH1221:text.IndexOf("ab", StringComparison.Ordinal) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(string text) => text.StartsWith("ab", StringComparison.Ordinal);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a culture-sensitive StringComparison stays culture-sensitive.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CultureStringComparisonStaysCultureSensitiveAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string text) => {|PSH1221:text.IndexOf("ab", StringComparison.CurrentCultureIgnoreCase) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(string text) => text.StartsWith("ab", StringComparison.CurrentCultureIgnoreCase);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the bare string form is rewritten to the bare prefix test, which is culture-sensitive on both sides.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// <c>string.IndexOf(string)</c> and <c>string.StartsWith(string)</c> are both current-culture
    /// searches, so the basis does not move: the rewrite is not smuggling in an ordinal comparison. It
    /// is also not fixing the missing <c>StringComparison</c> — that is PSH1207's job, and it stays
    /// missing here, exactly as the author left it.
    /// </remarks>
    [Test]
    public async Task BareStringFormKeepsTheCurrentCultureAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string text) => {|PSH1221:text.IndexOf("ab") == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string text) => text.StartsWith("ab");
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a search from a start index is not a prefix question and is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexOfWithStartIndexIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public bool M(string text, int start) => text.IndexOf("ab", start) == 0;
            }
            """);

    /// <summary>Verifies a comparison against something other than zero is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparisonAgainstNonZeroIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public bool M(string text) => text.IndexOf('/') == 1;

                public bool N(string text) => text.IndexOf('/') >= 0;
            }
            """);

    /// <summary>Verifies a search on something that is not a string is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStringReceiverIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public bool M(List<int> values) => values.IndexOf(3) == 0;
            }
            """);

    /// <summary>Verifies a comparison inside an expression tree is not rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparisonInsideExpressionTreeIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Linq.Expressions;

            public class C
            {
                public Expression<Func<string, bool>> M() => text => text.IndexOf('/') == 0;
            }
            """);

    /// <summary>
    /// Verifies the char form is silent against netstandard2.0, where <c>StartsWith(char)</c> does not
    /// exist — this is the rule's headline gate.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// <c>string.IndexOf(char)</c> compiles happily on netstandard2.0 and .NET Framework, and
    /// <c>string.StartsWith(char)</c> does not exist on either. Suggesting it there would hand the
    /// author a diagnostic they cannot act on, so the overload is resolved from the compilation and the
    /// shape is not reported when it is missing.
    /// </remarks>
    [Test]
    public async Task NetStandard20IsSilentOnTheCharFormAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = """
                       public class C
                       {
                           public bool M(string text) => text.IndexOf('/') == 0;
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Verifies the StringComparison form still reports against netstandard2.0, where that overload
    /// does exist.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The gate is per shape, not per rule: the same compilation that cannot offer
    /// <c>StartsWith(char)</c> offers <c>StartsWith(string, StringComparison)</c> perfectly well, so
    /// that shape is still reported and its fix still compiles.
    /// </remarks>
    [Test]
    public async Task NetStandard20StillReportsTheStringComparisonFormAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string text) => {|PSH1221:text.IndexOf("ab", StringComparison.Ordinal) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(string text) => text.StartsWith("ab", StringComparison.Ordinal);
                                   }
                                   """;
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
            FixedCode = FixedSource,
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
