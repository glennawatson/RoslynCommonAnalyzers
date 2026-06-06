// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyTupleName = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.TupleElementNameAnalyzer,
    StyleSharp.Analyzers.TupleElementNameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1142 (refer to tuple elements by name) and its fix.</summary>
public class TupleElementNameAnalyzerUnitTest
{
    /// <summary>Verifies access to a named element via ItemN is reported (SST1142) and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedElementItemAccessRenamedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M()
                                  {
                                      (int Count, int Total) data = (1, 2);
                                      return data.{|SST1142:Item1|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M()
                                       {
                                           (int Count, int Total) data = (1, 2);
                                           return data.Count;
                                       }
                                   }
                                   """;
        var test = new VerifyTupleName.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = FixedSource };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies ItemN access on an unnamed tuple is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnnamedTupleItemAccessIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M()
                                  {
                                      (int, int) data = (1, 2);
                                      return data.Item1;
                                  }
                              }
                              """;
        var test = new VerifyTupleName.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = Source };
        await test.RunAsync(CancellationToken.None);
    }
}
