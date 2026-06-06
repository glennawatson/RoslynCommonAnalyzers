// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports issues with C# 14 extension blocks declared in a class: an empty extension block
/// (SST1700), a second extension block that repeats an earlier block's receiver type (SST1701),
/// extension blocks separated by other members (SST1702), a container class not named with an
/// 'Extensions' suffix (SST1704), and classic extension methods mixed in with extension blocks
/// (SST1705). A class with no extension block bails after a single membership scan, so the common
/// case is cheap; on the Roslyn 4.8 floor the syntax cannot occur, so nothing is reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExtensionBlockAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ExtensionRules.EmptyExtensionBlock,
        ExtensionRules.CombineExtensionBlocks,
        ExtensionRules.GroupExtensionBlocks,
        ExtensionRules.ExtensionContainerNaming,
        ExtensionRules.DoNotMixExtensionStyles,
        ExtensionRules.BroadExtensionReceiver,
        ExtensionRules.OrderExtensionBlocks);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    /// <summary>Reports extension-block issues among a class's direct members.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        var members = declaration.Members;
        if (!ContainsExtensionBlock(members))
        {
            return;
        }

        if (!declaration.Identifier.ValueText.EndsWith("Extensions", System.StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(ExtensionRules.ExtensionContainerNaming, declaration.Identifier.GetLocation(), declaration.Identifier.ValueText));
        }

        // 'sawExtension' records that a block has been seen; 'groupEnded' that a non-extension member
        // has since interrupted the run — a later extension block is then no longer contiguous.
        // 'previousReceiver' is the receiver type of the most recent block, for the ordering rule.
        var sawExtension = false;
        var groupEnded = false;
        string? previousReceiver = null;
        for (var index = 0; index < members.Count; index++)
        {
            var member = members[index];
            if (member is not TypeDeclarationSyntax block || !ExtensionBlockHelper.IsExtensionBlock(block))
            {
                groupEnded |= sawExtension;
                if (ExtensionBlockHelper.IsClassicExtensionMethod(member))
                {
                    context.ReportDiagnostic(Diagnostic.Create(ExtensionRules.DoNotMixExtensionStyles, ((MethodDeclarationSyntax)member).Identifier.GetLocation()));
                }

                continue;
            }

            sawExtension = true;
            ProcessBlock(context, members, index, block, groupEnded, ref previousReceiver);
        }
    }

    /// <summary>Applies the per-block rules (SST1700/1701/1702/1706/1707) to one extension block.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="members">The class members.</param>
    /// <param name="index">The index of the block.</param>
    /// <param name="block">The extension block.</param>
    /// <param name="groupEnded">Whether a non-extension member already interrupted the block run.</param>
    /// <param name="previousReceiver">The receiver type of the previous block (updated on return).</param>
    private static void ProcessBlock(
        SyntaxNodeAnalysisContext context,
        SyntaxList<MemberDeclarationSyntax> members,
        int index,
        TypeDeclarationSyntax block,
        bool groupEnded,
        ref string? previousReceiver)
    {
        var location = block.GetFirstToken().GetLocation();
        var receiver = ExtensionBlockHelper.ReceiverTypeText(block);

        if (groupEnded)
        {
            context.ReportDiagnostic(Diagnostic.Create(ExtensionRules.GroupExtensionBlocks, location));
        }

        if (block.Members.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(ExtensionRules.EmptyExtensionBlock, location));
        }

        if (IsBroadReceiver(receiver))
        {
            context.ReportDiagnostic(Diagnostic.Create(ExtensionRules.BroadExtensionReceiver, location, receiver));
        }

        if (receiver is not null && previousReceiver is not null && string.CompareOrdinal(receiver, previousReceiver) < 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(ExtensionRules.OrderExtensionBlocks, location));
        }

        previousReceiver = receiver ?? previousReceiver;
        ReportDuplicateReceiver(context, members, index, receiver);
    }

    /// <summary>Returns whether a receiver type is one that attaches extension members to every type.</summary>
    /// <param name="receiver">The receiver type text.</param>
    /// <returns><see langword="true"/> for <c>object</c>, <c>System.Object</c>, or <c>dynamic</c>.</returns>
    private static bool IsBroadReceiver(string? receiver) => receiver is "object" or "System.Object" or "dynamic";

    /// <summary>Returns whether any member is an extension block.</summary>
    /// <param name="members">The class members.</param>
    /// <returns><see langword="true"/> when at least one member is an extension block.</returns>
    private static bool ContainsExtensionBlock(SyntaxList<MemberDeclarationSyntax> members)
    {
        foreach (var member in members)
        {
            if (ExtensionBlockHelper.IsExtensionBlock(member))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports the block when an earlier extension block in the same class shares its receiver type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="members">The class members.</param>
    /// <param name="index">The index of the current block.</param>
    /// <param name="receiver">The current block's receiver type text (already computed).</param>
    private static void ReportDuplicateReceiver(SyntaxNodeAnalysisContext context, SyntaxList<MemberDeclarationSyntax> members, int index, string? receiver)
    {
        if (receiver is null)
        {
            return;
        }

        for (var earlier = 0; earlier < index; earlier++)
        {
            if (members[earlier] is TypeDeclarationSyntax other
                && ExtensionBlockHelper.IsExtensionBlock(other)
                && ExtensionBlockHelper.ReceiverTypeText(other) == receiver)
            {
                context.ReportDiagnostic(Diagnostic.Create(ExtensionRules.CombineExtensionBlocks, members[index].GetFirstToken().GetLocation(), receiver));
                return;
            }
        }
    }
}
