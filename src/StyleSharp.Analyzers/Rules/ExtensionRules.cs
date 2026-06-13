// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the C# 14 extension-block rules (SST17xx). These
/// cover the new <c>extension(Receiver) { … }</c> member syntax.
/// </summary>
internal static class ExtensionRules
{
    /// <summary>SST1700 — an extension block declares no members.</summary>
    public static readonly DiagnosticDescriptor EmptyExtensionBlock = Create(
        "SST1700",
        "Extension blocks should not be empty",
        "Remove this empty extension block or add members to it",
        "An extension block declares at least one member; an empty block contributes nothing.");

    /// <summary>SST1701 — two extension blocks in a type share the same receiver type.</summary>
    public static readonly DiagnosticDescriptor CombineExtensionBlocks = Create(
        "SST1701",
        "Combine extension blocks for the same receiver type",
        "Merge this extension block into the earlier 'extension({0})' block",
        "Extension members for one receiver type live in a single extension block rather than several.");

    /// <summary>SST1702 — extension blocks in a type are separated by other members.</summary>
    public static readonly DiagnosticDescriptor GroupExtensionBlocks = Create(
        "SST1702",
        "Extension blocks should be grouped together",
        "Move this extension block next to the other extension blocks",
        "The extension blocks of a type are contiguous rather than interleaved with other members.");

    /// <summary>SST1703 — a classic 'this'-parameter extension method is used where an extension block could be (opt-in).</summary>
    public static readonly DiagnosticDescriptor PreferExtensionBlock = CreateOptIn(
        "SST1703",
        "Prefer extension blocks over extension methods",
        "Replace this classic extension method with an extension block member",
        "Extension members are declared inside an 'extension(Receiver) { … }' block. Off by default — adopting the C# 14 syntax is a deliberate, repo-wide migration.");

    /// <summary>SST1704 — a class declaring extension blocks is not named with an accepted extension-container suffix.</summary>
    public static readonly DiagnosticDescriptor ExtensionContainerNaming = Create(
        "SST1704",
        "Extension container class names should end in 'Extensions' or 'Mixins'",
        "Rename '{0}' to end in 'Extensions' or 'Mixins'",
        "A class that declares extension blocks is named with an accepted suffix such as 'Extensions' or 'Mixins' so its purpose is clear and it is easy to find.");

    /// <summary>SST1705 — a class mixes classic extension methods with extension blocks.</summary>
    public static readonly DiagnosticDescriptor DoNotMixExtensionStyles = Create(
        "SST1705",
        "Do not mix classic extension methods with extension blocks",
        "Move this extension method into an extension block",
        "A class that uses extension blocks declares all of its extensions as extension-block members rather than mixing in classic 'this'-parameter methods.");

    /// <summary>SST1706 — an extension block targets a broad receiver type such as <c>object</c> or <c>dynamic</c>.</summary>
    public static readonly DiagnosticDescriptor BroadExtensionReceiver = Create(
        "SST1706",
        "Avoid declaring extension members on a broad receiver type",
        "Avoid declaring extension members on '{0}'",
        "Extension members on 'object' or 'dynamic' attach to every type and clutter completion lists; target a specific type instead.");

    /// <summary>SST1707 — extension blocks in a type are not ordered by receiver type (opt-in).</summary>
    public static readonly DiagnosticDescriptor OrderExtensionBlocks = CreateOptIn(
        "SST1707",
        "Extension blocks should be ordered by receiver type",
        "Order this extension block by its receiver type",
        "Extension blocks within a type are ordered by receiver type so they are easy to scan. Off by default — block ordering is a matter of preference.");

    /// <summary>Creates a Warning-severity Extensions descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Extensions",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");

    /// <summary>Creates an Extensions descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Extensions",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
