// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The MEF-contract descriptors (SST2472, SST2473, SST2474), all reported by <see cref="MefContractAnalyzer"/>.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2472 — a type is exported for a contract it does not implement or inherit.</summary>
    public static readonly DiagnosticDescriptor ExportedContractNotImplemented = Create(
        "SST2472",
        "An exported contract type must be implemented by the exporting type",
        "This type is exported as the contract '{0}', but it neither implements nor inherits it, so the container cannot supply it for that contract",
        ExportedContractNotImplementedDescription);

    /// <summary>SST2473 — a shared export part is constructed directly with <c>new</c>.</summary>
    public static readonly DiagnosticDescriptor SharedExportPartConstructedDirectly = Create(
        "SST2473",
        "A shared export part should be obtained from the container, not constructed with 'new'",
        "'{0}' is a shared export part, so constructing it with 'new' creates an instance outside the container and defeats its single-instance guarantee",
        SharedExportPartConstructedDirectlyDescription);

    /// <summary>SST2474 — a part-creation-policy attribute is applied to a type with no export.</summary>
    public static readonly DiagnosticDescriptor CreationPolicyWithoutExport = Create(
        "SST2474",
        "A part-creation-policy attribute is meaningless without an export",
        "This creation-policy attribute has no effect because the type it is applied to is not exported",
        CreationPolicyWithoutExportDescription);

    /// <summary>The ExportedContractNotImplemented rule description.</summary>
    private const string ExportedContractNotImplementedDescription =
        "An export declares the contract a type is offered under: the container hands the part out wherever that contract is "
        + "imported. When the exporting type does not actually implement or inherit the declared contract, nothing satisfies the "
        + "import at run time — composition fails, or the cast the container performs throws — even though the code compiles. The "
        + "contract is only ever named inside the attribute, so the mismatch is invisible until the part is composed. The exported "
        + "contract type must be assignable from the exporting type.";

    /// <summary>The SharedExportPartConstructedDirectly rule description.</summary>
    private const string SharedExportPartConstructedDirectlyDescription =
        "A part marked shared is meant to exist once: the container creates a single instance and hands the same one to every "
        + "importer. Constructing that type directly with 'new' builds a second instance the container knows nothing about, so the "
        + "single-instance guarantee the part was designed around no longer holds — any state it caches, any resource it owns, and "
        + "any identity comparison against the container's instance quietly diverge. A shared part should be obtained through import "
        + "or from the container, never with 'new'.";

    /// <summary>The CreationPolicyWithoutExport rule description.</summary>
    private const string CreationPolicyWithoutExportDescription =
        "A part-creation-policy attribute only tells the container how to share the instances it creates for an export. On a type "
        + "that is not exported the container never creates the part, so the attribute governs nothing: it reads as though the type "
        + "participates in composition when it does not. The attribute is either left over from an export that was removed, or it "
        + "signals a missing export. Either way it should be removed, or the intended export added.";
}
