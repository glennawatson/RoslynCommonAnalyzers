// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyTuple = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1141UseTupleSyntaxAnalyzer,
    StyleSharp.Analyzers.Sst1141UseTupleSyntaxCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1141 (use tuple syntax instead of ValueTuple&lt;...&gt;) and its fix.</summary>
public class UseTupleSyntaxAnalyzerUnitTest
{
    /// <summary>Verifies an explicit ValueTuple type is reported (SST1141) and rewritten to tuple syntax.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueTupleTypeRewrittenAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public {|SST1141:ValueTuple<int, string>|} M() => default;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public (int, string) M() => default;
                                   }
                                   """;
        var test = new VerifyTuple.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = FixedSource };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies tuple syntax and a single-argument ValueTuple are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TupleSyntaxAndSingleArgAreCleanAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public (int, string) M() => default;

                                  public ValueTuple<int> Single() => default;
                              }
                              """;
        var test = new VerifyTuple.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = Source };
        await test.RunAsync(CancellationToken.None);
    }
}
