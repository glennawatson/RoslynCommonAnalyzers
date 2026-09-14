// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using RoslynCommon.Analyzers.Tests;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1660ParameterDocumentationOrderAnalyzer,
    StyleSharp.Analyzers.Sst1660ParameterDocumentationOrderCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1660 (parameter documentation should be ordered to match the parameters).</summary>
public class ParameterDocumentationOrderAnalyzerUnitTest
{
    /// <summary>Verifies <c>&lt;param&gt;</c> elements in declaration order produce no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InOrderIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Does things.</summary>
                /// <param name="a">The a.</param>
                /// <param name="b">The b.</param>
                public void M(int a, int b)
                {
                }
            }
            """);

    /// <summary>Verifies a partially-documented member is out of scope (the set does not match).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IncompleteSetIsIgnoredAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Does things.</summary>
                /// <param name="a">The a.</param>
                public void M(int a, int b)
                {
                }
            }
            """);

    /// <summary>Verifies only an exact, distinct parameter set is eligible for ordering diagnostics.</summary>
    /// <param name="documentation">The absent or inapplicable documentation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("")]
    [Arguments("/// <inheritdoc/>")]
    [Arguments("/// <param>Missing name.</param><param name=\"a\"/>")]
    [Arguments("/// <param/><param name=\"a\"/>")]
    [Arguments("/// <param name=\"b\"/><param name=\"b\"/>")]
    [Arguments("/// <param name=\"z\"/><param name=\"a\"/>")]
    [Arguments("/// <param name=\"b\"/><param name=\"a\"/><param name=\"z\"/>")]
    public Task InapplicableDocumentationIsCleanAsync(string documentation) =>
        Verify.VerifyAnalyzerAsync($$"""
            class C
            {
                {{documentation}}
                void M(int a, int b) { }
            }
            """);

    /// <summary>Verifies members without at least two parameters cannot have an order mismatch.</summary>
    /// <param name="parameters">The zero- or one-parameter declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("")]
    [Arguments("int a")]
    public Task FewerThanTwoParametersIsCleanAsync(string parameters) =>
        Verify.VerifyAnalyzerAsync($"class C {{ void M({parameters}) {{ }} }}");

    /// <summary>Verifies all supported declaration kinds report their out-of-order element.</summary>
    /// <param name="declaration">The documented member or primary constructor.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("public C(int a, int b) { }")]
    [Arguments("public delegate void D(int a, int b);")]
    [Arguments("public int this[int a, int b] => 0;")]
    [Arguments("public class Nested(int a, int b) { }")]
    [Arguments("public struct Nested(int a, int b) { }")]
    [Arguments("public record Nested(int a, int b);")]
    [Arguments("public record struct Nested(int a, int b);")]
    public Task SupportedMembersReportTheirNamesAsync(string declaration) =>
        new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net80,
            TestCode = $$"""
                class C
                {
                    /// {|SST1660:<param name="b"/>|}
                    /// <param name="a"/>
                    {{declaration}}
                }
                """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a matching prefix does not hide a later mismatch.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LaterMismatchIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            class C
            {
                /// <param name="a"/>
                /// {|SST1660:<param name="c"/>|}
                /// <param name="b"/>
                void M(int a, int b, int c) { }
            }
            """);

    /// <summary>Verifies out-of-order parameter documentation is reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OutOfOrderIsReorderedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Does things.</summary>
                                  /// {|SST1660:<param name="b">The b.</param>|}
                                  /// <param name="a">The a.</param>
                                  public void M(int a, int b)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does things.</summary>
                                       /// <param name="a">The a.</param>
                                       /// <param name="b">The b.</param>
                                       public void M(int a, int b)
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a three-parameter rotation is reordered to match the declaration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RotationIsReorderedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Does things.</summary>
                                  /// {|SST1660:<param name="c">The c.</param>|}
                                  /// <param name="a">The a.</param>
                                  /// <param name="b">The b.</param>
                                  public void M(int a, int b, int c)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does things.</summary>
                                       /// <param name="a">The a.</param>
                                       /// <param name="b">The b.</param>
                                       /// <param name="c">The c.</param>
                                       public void M(int a, int b, int c)
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }
}
