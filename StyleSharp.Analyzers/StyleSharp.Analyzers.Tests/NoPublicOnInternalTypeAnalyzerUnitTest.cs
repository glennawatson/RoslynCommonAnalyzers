// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNoPublic = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.NoPublicOnInternalTypeAnalyzer,
    StyleSharp.Analyzers.NoPublicOnInternalTypeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1416 (do not declare public members in a non-public type) and its fix.</summary>
public class NoPublicOnInternalTypeAnalyzerUnitTest
{
    /// <summary>Verifies a public member of an internal type is reported (SST1416) and demoted to internal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMemberOfInternalTypeDemotedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1416:public|} void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       internal void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifyNoPublic.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies interface implementations and members of a public type are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceImplementationAndPublicTypeAreCleanAsync()
        => await VerifyNoPublic.VerifyAnalyzerAsync(
            """
            internal class C : System.IDisposable
            {
                public void Dispose()
                {
                }
            }

            public class D
            {
                public void M()
                {
                }
            }
            """);
}
