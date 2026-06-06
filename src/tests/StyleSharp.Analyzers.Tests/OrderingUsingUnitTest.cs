// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
}
