// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUsingQualified = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1135UsingDirectiveQualifiedAnalyzer,
    StyleSharp.Analyzers.Sst1135UsingDirectiveQualifiedCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the using-directive-qualified rule (SST1135).</summary>
public class UsingDirectiveQualifiedAnalyzerUnitTest
{
    /// <summary>Verifies a context-relative using is reported (SST1135) and fully qualified.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RelativeUsingQualifiedAsync()
    {
        const string Source = """
                              namespace System.Threading
                              {
                                  using {|SST1135:Tasks|};
                              }
                              """;
        const string FixedSource = """
                                   namespace System.Threading
                                   {
                                       using System.Threading.Tasks;
                                   }
                                   """;
        await VerifyUsingQualified.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All qualifies every relative using directive in one pass (SST1135).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              namespace System.Threading
                              {
                                  using {|SST1135:Tasks|};
                              }

                              namespace System.Collections
                              {
                                  using {|SST1135:Generic|};
                              }
                              """;
        const string FixedSource = """
                                   namespace System.Threading
                                   {
                                       using System.Threading.Tasks;
                                   }

                                   namespace System.Collections
                                   {
                                       using System.Collections.Generic;
                                   }
                                   """;
        await VerifyUsingQualified.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a fully qualified using is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedUsingIsCleanAsync()
        => await VerifyUsingQualified.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            internal class C
            {
                private Task M() => Task.CompletedTask;
            }
            """);
}
