// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared per-slot first-demand resolution of well-known types.</summary>
public sealed class LazyMetadataTypeSlotsUnitTest
{
    /// <summary>The slot of the base marker.</summary>
    private const int MarkerSlot = 0;

    /// <summary>The slot of the name no compilation declares.</summary>
    private const int MissingSlot = 1;

    /// <summary>The slot of the second marker.</summary>
    private const int OtherSlot = 2;

    /// <summary>The slot names: a base marker, a missing type and a second marker.</summary>
    private static readonly string[] SlotNames = ["Framework.MarkerAttribute", "Framework.Missing", "Framework.OtherAttribute"];

    /// <summary>A compilation declaring the markers, a derived marker and look-alikes in other namespaces.</summary>
    private static readonly CSharpCompilation Compilation = CSharpCompilation.Create(
        nameof(LazyMetadataTypeSlotsUnitTest),
        [CSharpSyntaxTree.ParseText(
            """
            namespace Framework
            {
                public class MarkerAttribute : System.Attribute { }
                public class OtherAttribute : System.Attribute { }
                public class DerivedAttribute : MarkerAttribute { }
                public class Outer { public class MarkerAttribute : System.Attribute { } }
            }

            namespace Elsewhere
            {
                public class MarkerAttribute : System.Attribute { }
            }

            public class MarkerAttribute : System.Attribute { }
            """)],
        [RuntimeMetadataReferences.CoreLibrary]);

    /// <summary>Verifies a slot resolves its type, caches an absent type, and returns the same symbol on repeat.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResolvesAndCachesSlotsAsync()
    {
        var slots = new LazyMetadataTypeSlots(Compilation, SlotNames);

        var marker = slots.Get(MarkerSlot);

        await Assert.That(marker?.Name).IsEqualTo("MarkerAttribute");
        await Assert.That(slots.Get(MissingSlot)).IsNull();
        await Assert.That(slots.Get(MissingSlot)).IsNull();
        await Assert.That(ReferenceEquals(slots.Get(MarkerSlot), marker)).IsTrue();
    }

    /// <summary>Verifies only the exact top-level type in the slot range matches, not a look-alike or a type outside the range.</summary>
    /// <param name="metadataName">The metadata name of the candidate type.</param>
    /// <param name="start">The first slot, inclusive.</param>
    /// <param name="end">The last slot, exclusive.</param>
    /// <param name="expected">Whether the candidate is one of the slots' types.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Framework.MarkerAttribute", MarkerSlot, OtherSlot + 1, true)]
    [Arguments("Framework.OtherAttribute", MarkerSlot, OtherSlot + 1, true)]
    [Arguments("Framework.OtherAttribute", MarkerSlot, OtherSlot, false)]
    [Arguments("Framework.DerivedAttribute", MarkerSlot, OtherSlot + 1, false)]
    [Arguments("Elsewhere.MarkerAttribute", MarkerSlot, OtherSlot + 1, false)]
    [Arguments("MarkerAttribute", MarkerSlot, OtherSlot + 1, false)]
    [Arguments("Framework.Outer+MarkerAttribute", MarkerSlot, OtherSlot + 1, false)]
    public async Task MatchesOnlyTheSlotTypesAsync(string metadataName, int start, int end, bool expected)
    {
        var slots = new LazyMetadataTypeSlots(Compilation, SlotNames);

        await Assert.That(slots.IsAny(Compilation.GetTypeByMetadataName(metadataName)!, start, end)).IsEqualTo(expected);
    }

    /// <summary>Verifies a type deriving from a slot type matches through its base chain.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MatchesThroughTheBaseChainAsync()
    {
        var slots = new LazyMetadataTypeSlots(Compilation, SlotNames);
        var derived = Compilation.GetTypeByMetadataName("Framework.DerivedAttribute")!;

        await Assert.That(slots.IsOrDerivesFromAny(derived, MarkerSlot, MarkerSlot + 1)).IsTrue();
        await Assert.That(slots.IsOrDerivesFromAny(derived, OtherSlot, OtherSlot + 1)).IsFalse();
    }
}
