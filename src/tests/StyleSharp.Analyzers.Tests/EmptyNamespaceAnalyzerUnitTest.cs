// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEmptyNamespace = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.EmptyCodeAnalyzer,
    StyleSharp.Analyzers.EmptyNamespaceCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1435 (empty namespace) and its fix.</summary>
public class EmptyNamespaceAnalyzerUnitTest
{
    /// <summary>Verifies an empty namespace declaration is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyNamespaceRemovedAsync()
    {
        const string Source = """
                              public class A
                              {
                                  private int _a;
                              }

                              namespace {|SST1435:Empty|}
                              {
                              }

                              public class B
                              {
                                  private int _b;
                              }
                              """;
        const string FixedSource = """
                                   public class A
                                   {
                                       private int _a;
                                   }

                                   public class B
                                   {
                                       private int _b;
                                   }
                                   """;
        await VerifyEmptyNamespace.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a namespace with members is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonEmptyNamespaceIsCleanAsync()
        => await VerifyEmptyNamespace.VerifyAnalyzerAsync(
            """
            namespace Populated
            {
                public class C
                {
                    private int _value;
                }
            }
            """);
}
