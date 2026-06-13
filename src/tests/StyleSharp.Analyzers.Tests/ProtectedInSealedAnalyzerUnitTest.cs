// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyProtected = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantModifierAnalyzer,
    StyleSharp.Analyzers.RemoveModifierCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1427 (protected members of sealed types) and its fix.</summary>
public class ProtectedInSealedAnalyzerUnitTest
{
    /// <summary>Verifies a protected member of a sealed class is reported and made private.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedMemberOfSealedClassIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  {|SST1427:protected|} void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifyProtected.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies protected members of non-sealed types and overrides are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonSealedAndOverrideAreCleanAsync()
        => await VerifyProtected.VerifyAnalyzerAsync(
            """
            public class Base
            {
                protected virtual void M()
                {
                }
            }

            public sealed class C : Base
            {
                protected override void M()
                {
                }
            }
            """);
}
