// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyPartial = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1205PartialElementAccessAnalyzer,
    StyleSharp.Analyzers.Sst1400AccessModifierCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the partial-element access rule (SST1205).</summary>
public class OrderingPartialUnitTest
{
    /// <summary>Verifies a partial type without an access modifier is reported (SST1205) and gets one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialTypeGetsAccessModifierAsync()
    {
        const string Source = """
                              partial class {|SST1205:C|}
                              {
                              }
                              """;
        const string FixedSource = """
                                   internal partial class C
                                   {
                                   }
                                   """;
        await VerifyPartial.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a partial type that declares its access is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialTypeWithAccessIsCleanAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            internal partial class C
            {
            }
            """);
}
