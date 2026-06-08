// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyEmptyCollection = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2100EmptyCollectionExpressionAnalyzer,
    StyleSharp.Analyzers.CollectionExpressionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2100 (use an empty collection expression).</summary>
public class EmptyCollectionExpressionAnalyzerUnitTest
{
    /// <summary>Verifies standard empty collection creations are replaced with brackets.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EmptyCreationsAreFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int[] A = {|SST2100:Array.Empty<int>()|};
                                  public List<int> B = {|SST2100:new List<int>()|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public int[] A = [];
                                       public List<int> B = [];
                                   }
                                   """;
        var test = new VerifyEmptyCollection.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a targetless var initialization is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task VarInitializationIsCleanAsync()
        => await VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void M()
                {
                    var values = Array.Empty<int>();
                }
            }
            """);
}
