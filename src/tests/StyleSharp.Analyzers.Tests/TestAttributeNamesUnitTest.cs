// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the recognition of the written names of test-marking attributes.</summary>
public sealed class TestAttributeNamesUnitTest
{
    /// <summary>Verifies the supported names match with and without the suffix, and other names do not.</summary>
    /// <param name="name">The written attribute name.</param>
    /// <param name="expected">Whether the name marks a test.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Fact", true)]
    [Arguments("TheoryAttribute", true)]
    [Arguments("TestCaseSource", true)]
    [Arguments("DataTestMethodAttribute", true)]
    [Arguments("STATestMethod", true)]
    [Arguments("CulturedTheoryAttribute", true)]
    [Arguments("SkippableFact", true)]
    [Arguments("UITheory", true)]
    [Arguments("Scenario", true)]
    [Arguments("TestClass", false)]
    [Arguments("Attribute", false)]
    [Arguments("FactAttributeAttribute", false)]
    [Arguments("Obsolete", false)]
    public async Task RecognizesTestAttributeNamesAsync(string name, bool expected) =>
        await Assert.That(TestAttributeNames.IsTestAttributeName(name)).IsEqualTo(expected);

    /// <summary>Verifies a marker table places the leading names first, ends with the TUnit marker, and is a fresh copy.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreatesMarkerTableAfterLeadingNamesAsync()
    {
        const string LeadingName = "System.Threading.Thread";
        var markers = TestAttributeNames.CreateMarkerMetadataNames();
        var withLeading = TestAttributeNames.CreateMarkerMetadataNames(LeadingName);

        await Assert.That(markers[TestAttributeNames.TUnitMarkerIndex]).IsEqualTo("TUnit.Core.TestAttribute");
        await Assert.That(markers.Length).IsEqualTo(TestAttributeNames.TUnitMarkerIndex + 1);
        await Assert.That(withLeading[0]).IsEqualTo(LeadingName);
        await Assert.That(withLeading[1]).IsEqualTo(markers[0]);
        await Assert.That(ReferenceEquals(markers, TestAttributeNames.CreateMarkerMetadataNames())).IsFalse();
    }
}
