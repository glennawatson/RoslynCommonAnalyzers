// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using VerifyUsing = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.UsingOrderingAnalyzer,
    StyleSharp.Analyzers.UsingSortCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the using-directive ordering rules (SST1200/1208–1217).</summary>
public class OrderingUsingUnitTest
{
    /// <summary>Verifies a System directive after a non-System directive is reported (SST1208) and sorted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SystemUsingsSortedFirstAsync()
    {
        const string Source = """
                              using Foo;
                              {|SST1208:using System;|}

                              namespace Foo
                              {
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using Foo;

                                   namespace Foo
                                   {
                                   }
                                   """;
        await VerifyUsing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies regular directives out of alphabetical order are reported (SST1210) and sorted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RegularUsingsSortedAlphabeticallyAsync()
    {
        const string Source = """
                              using System.Text;
                              {|SST1210:using System.Collections;|}

                              internal class C
                              {
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections;
                                   using System.Text;

                                   internal class C
                                   {
                                   }
                                   """;
        await VerifyUsing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies directives of differing depth are ordered by their first diverging segment (no SST1210).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferingDepthOrderedByDivergingSegmentAsync()
        => await VerifyUsing.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Text;

            internal class C
            {
            }
            """);

    /// <summary>Verifies a using alias before a regular directive is reported (SST1209) and sorted last.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AliasUsingsSortedLastAsync()
    {
        const string Source = """
                              {|SST1209:using X = System.Console;|}
                              using System.Text;

                              internal class C
                              {
                              }
                              """;
        const string FixedSource = """
                                   using System.Text;
                                   using X = System.Console;

                                   internal class C
                                   {
                                   }
                                   """;
        await VerifyUsing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a using static before a regular directive is reported (SST1216) and sorted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticUsingsSortedAfterRegularAsync()
    {
        const string Source = """
                              {|SST1216:using static System.Math;|}
                              using System.Text;

                              internal class C
                              {
                              }
                              """;
        const string FixedSource = """
                                   using System.Text;
                                   using static System.Math;

                                   internal class C
                                   {
                                   }
                                   """;
        await VerifyUsing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies static directives out of alphabetical order are reported (SST1217) and sorted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticUsingsSortedAlphabeticallyAsync()
    {
        const string Source = """
                              using static System.Math;
                              {|SST1217:using static System.Console;|}

                              internal class C
                              {
                              }
                              """;
        const string FixedSource = """
                                   using static System.Console;
                                   using static System.Math;

                                   internal class C
                                   {
                                   }
                                   """;
        await VerifyUsing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies alias directives out of alphabetical order are reported (SST1211) and sorted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AliasUsingsSortedAlphabeticallyAsync()
    {
        const string Source = """
                              using Y = System.Text.StringBuilder;
                              {|SST1211:using X = System.Console;|}

                              internal class C
                              {
                              }
                              """;
        const string FixedSource = """
                                   using X = System.Console;
                                   using Y = System.Text.StringBuilder;

                                   internal class C
                                   {
                                   }
                                   """;
        await VerifyUsing.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies directives separated by a conditional directive are sorted independently (no SST1210 across the boundary).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalDirectiveResetsOrderingAsync()
        => await VerifyUsing.VerifyAnalyzerAsync(
            """
            using System.Threading;
            #if true
            using System.Collections;
            #endif

            internal class C
            {
            }
            """);

    /// <summary>Verifies a directive after an #endif is not compared against the conditional branch (no SST1210).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalElseBranchResetsOrderingAsync()
        => await VerifyUsing.VerifyAnalyzerAsync(
            """
            #if false
            using System.Threading;
            #else
            using System.Globalization;
            #endif
            using System.Collections;

            internal class C
            {
            }
            """);

    /// <summary>Verifies directives inside the same conditional block are still alphabetised (SST1210), and the sort fix is suppressed to avoid scrambling directives.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WithinConditionalBlockStillAlphabeticalAsync()
    {
        const string Source = """
            #if true
            using System.Threading;
            {|SST1210:using System.Collections;|}
            #endif

            internal class C
            {
            }
            """;

        var test = new VerifyUsing.Test
        {
            TestCode = Source,
            FixedCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a using directive inside a namespace is reported (SST1200).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingInsideNamespaceReportedAsync()
        => await VerifyUsing.VerifyAnalyzerAsync(
            """
            namespace Foo
            {
                {|SST1200:using System;|}
            }
            """);

    /// <summary>Verifies a correctly ordered using list is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrderedUsingsAreCleanAsync()
        => await VerifyUsing.VerifyAnalyzerAsync(
            """
            using System.Collections;
            using System.Text;

            internal class C
            {
            }
            """);

    /// <summary>Verifies qualified using names are compared segment-by-segment without changing sort order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareSortKeyOrdersQualifiedPrefixesAsync()
    {
        var left = ParseUsingDirective("using System;");
        var right = ParseUsingDirective("using System.Collections;");

        await Assert.That(UsingClassification.CompareSortKey(left, right)).IsLessThan(0);
    }

    /// <summary>Verifies alias using names are compared by alias identifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareSortKeyOrdersAliasesByNameAsync()
    {
        var left = ParseUsingDirective("using Alpha = System.Console;");
        var right = ParseUsingDirective("using Beta = System.Console;");

        await Assert.That(UsingClassification.CompareSortKey(left, right)).IsLessThan(0);
    }

    /// <summary>Parses a single using directive from the supplied compilation unit text.</summary>
    /// <param name="source">The source containing the using directive.</param>
    /// <returns>The parsed using directive.</returns>
    private static UsingDirectiveSyntax ParseUsingDirective(string source)
        => SyntaxFactory.ParseCompilationUnit(source).Usings[0];
}
