// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyPartial = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2335PartialStaticMismatchAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2335 (declare the same 'static' modifier on every part of a partial type).</summary>
public class Sst2335PartialStaticMismatchAnalyzerUnitTest
{
    /// <summary>Verifies the part that omits <c>static</c> is reported when another part declares it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PartOmittingStaticIsReportedAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            static partial class Widget
            {
            }

            partial class {|SST2335:Widget|}
            {
            }
            """);

    /// <summary>Verifies every part that omits <c>static</c> is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EveryOmittingPartIsReportedAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            static partial class Gadget
            {
            }

            partial class {|SST2335:Gadget|}
            {
            }

            partial class {|SST2335:Gadget|}
            {
            }
            """);

    /// <summary>Verifies parts that all declare <c>static</c> are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AllStaticPartsAreCleanAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            static partial class Widget
            {
            }

            static partial class Widget
            {
            }
            """);

    /// <summary>Verifies parts that all omit <c>static</c> are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AllInstancePartsAreCleanAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            partial class Widget
            {
            }

            partial class Widget
            {
            }
            """);

    /// <summary>Verifies a single, non-partial static class is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SingleStaticClassIsCleanAsync()
        => await VerifyPartial.VerifyAnalyzerAsync(
            """
            static class Widget
            {
            }
            """);
}
