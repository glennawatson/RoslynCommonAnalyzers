// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyPartial = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2335PartialStaticMismatchAnalyzer,
    StyleSharp.Analyzers.Sst2335PartialStaticMismatchCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2335PartialStaticMismatchCodeFixProvider"/> (SST2335 add 'static').</summary>
public class Sst2335PartialStaticMismatchCodeFixUnitTest
{
    /// <summary>A two-part partial class where one part omits <c>static</c>.</summary>
    private const string TwoPartSource = """
        static partial class Widget
        {
        }

        partial class {|SST2335:Widget|}
        {
        }
        """;

    /// <summary>Both parts static after the fix.</summary>
    private const string TwoPartFixed = """
        static partial class Widget
        {
        }

        static partial class Widget
        {
        }
        """;

    /// <summary>A three-part partial class where two parts omit <c>static</c>.</summary>
    private const string ThreePartSource = """
        static partial class Gadget
        {
        }

        partial class {|SST2335:Gadget|}
        {
        }

        partial class {|SST2335:Gadget|}
        {
        }
        """;

    /// <summary>Every part static after Fix All.</summary>
    private const string ThreePartFixed = """
        static partial class Gadget
        {
        }

        static partial class Gadget
        {
        }

        static partial class Gadget
        {
        }
        """;

    /// <summary>Verifies the fix adds <c>static</c> to the part that omits it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AddsStaticToOmittingPartAsync()
        => await VerifyPartial.VerifyCodeFixAsync(TwoPartSource, TwoPartFixed);

    /// <summary>Verifies Fix All makes every omitting part static.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixAllMakesEveryPartStaticAsync()
        => await VerifyPartial.VerifyCodeFixAsync(ThreePartSource, ThreePartFixed);
}
