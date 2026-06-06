// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyExplicitCollection = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExplicitCollectionExpressionAnalyzer,
    StyleSharp.Analyzers.CollectionExpressionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2101 (use an explicit collection expression).</summary>
public class ExplicitCollectionExpressionAnalyzerUnitTest
{
    /// <summary>Verifies an explicit array initializer is replaced with a collection expression.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitArrayIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int[] Values = {|SST2101:new[] { 1, 2, 3 }|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int[] Values = [ 1, 2, 3 ];
                                   }
                                   """;
        var test = new VerifyExplicitCollection.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies dictionary-style and var initializers are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AmbiguousInitializersAreCleanAsync()
        => await VerifyExplicitCollection.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M()
                {
                    var values = new[] { 1, 2, 3 };
                    Dictionary<int, string> map = new Dictionary<int, string> { { 1, "one" } };
                }
            }
            """);
}
