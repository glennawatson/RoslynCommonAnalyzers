// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Names the attributes that mark a test in the supported test frameworks.</summary>
internal static class TestAttributeNames
{
    /// <summary>The first NUnit marker slot, containing its non-parameterized test attribute.</summary>
    internal const int NUnitMarkerStart = 2;

    /// <summary>The slot containing NUnit's inline test-case attribute.</summary>
    internal const int NUnitTestCaseMarkerIndex = 3;

    /// <summary>The slot containing NUnit's external test-case source attribute.</summary>
    internal const int NUnitTestCaseSourceMarkerIndex = 4;

    /// <summary>The slot containing NUnit's theory builder.</summary>
    internal const int NUnitTheoryMarkerIndex = 5;

    /// <summary>The end of the NUnit marker range, exclusive.</summary>
    internal const int NUnitMarkerEnd = 6;

    /// <summary>The slot of the TUnit marker in a marker table, the one marker that does not require public methods.</summary>
    internal const int TUnitMarkerIndex = 8;

    /// <summary>The suffix an attribute name may be written with.</summary>
    private const string AttributeSuffix = "Attribute";

    /// <summary>The metadata names of the test-method marker attributes, with the TUnit marker in the last slot.</summary>
    private static readonly string[] MarkerMetadataNames =
    [
        "Xunit.FactAttribute",
        "Xunit.TheoryAttribute",
        "NUnit.Framework.TestAttribute",
        "NUnit.Framework.TestCaseAttribute",
        "NUnit.Framework.TestCaseSourceAttribute",
        "NUnit.Framework.TheoryAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute",
        "TUnit.Core.TestAttribute",
    ];

    /// <summary>Creates a metadata-name table holding the given names followed by the test-method marker names.</summary>
    /// <param name="leadingNames">The names placed first, so the markers start at their count.</param>
    /// <returns>A new table owned by the caller.</returns>
    internal static string[] CreateMarkerMetadataNames(params string[] leadingNames) => [.. leadingNames, .. MarkerMetadataNames];

    /// <summary>Returns whether a written attribute simple name marks a test in a supported framework or a common extension of one.</summary>
    /// <param name="name">The attribute's simple name, with or without the <c>Attribute</c> suffix.</param>
    /// <returns><see langword="true"/> for a known test-attribute name.</returns>
    /// <remarks>
    /// Beyond the framework markers this accepts the shipped attributes that derive from them: MSTest's
    /// <c>STATestMethod</c>, xUnit v3's <c>CulturedFact</c>/<c>CulturedTheory</c>, the <c>SkippableFact</c> and
    /// <c>StaFact</c>/<c>UIFact</c> families, and LightBDD's <c>Scenario</c>. Binding still decides whether one is a test.
    /// </remarks>
    internal static bool IsTestAttributeName(string name)
    {
        var bare = name.EndsWith(AttributeSuffix, StringComparison.Ordinal)
            ? name.AsSpan(0, name.Length - AttributeSuffix.Length)
            : name.AsSpan();

        return IsFrameworkMarkerName(bare) || IsDerivedMarkerName(bare);
    }

    /// <summary>Returns whether a bare attribute name is one of the framework test markers.</summary>
    /// <param name="bare">The attribute name without the <c>Attribute</c> suffix.</param>
    /// <returns><see langword="true"/> for a framework marker name.</returns>
    private static bool IsFrameworkMarkerName(ReadOnlySpan<char> bare) =>
        bare is "Fact" or "Theory" or "Test" or "TestCase" or "TestCaseSource" or "TestMethod" or "DataTestMethod";

    /// <summary>Returns whether a bare attribute name is a shipped attribute deriving from a framework test marker.</summary>
    /// <param name="bare">The attribute name without the <c>Attribute</c> suffix.</param>
    /// <returns><see langword="true"/> for a derived marker name.</returns>
    private static bool IsDerivedMarkerName(ReadOnlySpan<char> bare) =>
        bare is "STATestMethod" or "CulturedFact" or "CulturedTheory" or "SkippableFact" or "SkippableTheory"
            or "StaFact" or "StaTheory" or "UIFact" or "UITheory" or "Scenario";
}
