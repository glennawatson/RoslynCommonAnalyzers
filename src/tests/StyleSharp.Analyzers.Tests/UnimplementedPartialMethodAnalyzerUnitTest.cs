// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyPartial = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2468UnimplementedPartialMethodAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2468 (a classic partial method declared but never implemented).</summary>
public class UnimplementedPartialMethodAnalyzerUnitTest
{
    /// <summary>Verifies an unimplemented classic partial hook with a call site is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnimplementedClassicPartialIsReportedAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                partial void {|SST2468:OnChanged|}();

                public void Set() => OnChanged();
            }
            """);

    /// <summary>Verifies an unimplemented static classic partial method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnimplementedStaticClassicPartialIsReportedAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                static partial void {|SST2468:OnChanged|}();
            }
            """);

    /// <summary>Verifies an unimplemented partial method with a by-value parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnimplementedPartialWithValueParameterIsReportedAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                partial void {|SST2468:OnChanged|}(int value);
            }
            """);

    /// <summary>Verifies an unimplemented partial method with a <c>ref</c> parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnimplementedPartialWithRefParameterIsReportedAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                partial void {|SST2468:OnChanged|}(ref int value);
            }
            """);

    /// <summary>Verifies the defining declaration is reported even when a second partial part exists without an implementation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnimplementedAcrossSeparatePartialDeclarationsIsReportedAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                partial void {|SST2468:OnChanged|}();
            }

            public partial class C
            {
                public void Set() => OnChanged();
            }
            """);

    /// <summary>Verifies an implemented classic partial method is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplementedClassicPartialIsCleanAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                partial void OnChanged();

                public void Set() => OnChanged();
            }

            public partial class C
            {
                partial void OnChanged()
                {
                }
            }
            """);

    /// <summary>Verifies an extended partial method with any accessibility modifier is left to the compiler.</summary>
    /// <param name="accessibility">The accessibility keyword that makes the partial method extended.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("public")]
    [Arguments("private")]
    [Arguments("protected")]
    [Arguments("internal")]
    public async Task ExtendedPartialWithAccessibilityIsCleanAsync(string accessibility)
        => await VerifyPartial.VerifyAnalyzerAsync(
            $$"""
            public partial class C
            {
                {{accessibility}} partial void OnChanged();

                {{accessibility}} partial void OnChanged()
                {
                }
            }
            """);

    /// <summary>Verifies an extended partial method with a non-void return is left to the compiler.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExtendedPartialWithReturnValueIsCleanAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                private partial int Compute();

                private partial int Compute() => 0;
            }
            """);

    /// <summary>Verifies an extended partial method with an <c>out</c> parameter is left to the compiler.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExtendedPartialWithOutParameterIsCleanAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                private partial void TryGet(out int value);

                private partial void TryGet(out int value)
                {
                    value = 0;
                }
            }
            """);

    /// <summary>Verifies an ordinary method with a body is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrdinaryMethodIsCleanAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Set()
                {
                }
            }
            """);

    /// <summary>Verifies a bodyless non-partial method (an interface member) is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceMethodIsCleanAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            public interface IThing
            {
                void Do();
            }
            """);
}
