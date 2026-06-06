// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyBuiltInAlias = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.BuiltInTypeAliasAnalyzer,
    StyleSharp.Analyzers.BuiltInTypeAliasCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the built-in-type-alias rule (SST1121, opt-in).</summary>
public class BuiltInTypeAliasAnalyzerUnitTest
{
    /// <summary>Verifies a qualified framework type name is reported (SST1121) and replaced with its alias.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedFrameworkNameReplacedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private {|SST1121:System.Int32|} value;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int value;
                                   }
                                   """;
        await VerifyBuiltInAlias.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a bare framework type name is reported (SST1121) and replaced with its alias.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareFrameworkNameReplacedAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  private {|SST1121:Int32|} value;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       private int value;
                                   }
                                   """;
        await VerifyBuiltInAlias.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the keyword alias is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeywordAliasIsCleanAsync()
        => await VerifyBuiltInAlias.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int value;
            }
            """);
}
