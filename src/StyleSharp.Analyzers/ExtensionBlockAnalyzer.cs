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

        // 'sawExtension' records that a block has been seen; 'groupEnded' that a non-extension member
        // has since interrupted the run — a later extension block is then no longer contiguous.
        // 'previousReceiver' is the receiver type of the most recent block, for the ordering rule.
        var reportedContainerNaming = false;
        var sawExtension = false;
        var groupEnded = false;
        string? previousReceiver = null;
        HashSet<string>? seenReceivers = null;
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

            if (!reportedContainerNaming)
            {
                reportedContainerNaming = true;
                if (!declaration.Identifier.ValueText.EndsWith("Extensions", System.StringComparison.Ordinal))
                {
                    context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.ExtensionContainerNaming, declaration.SyntaxTree, declaration.Identifier.Span, declaration.Identifier.ValueText));
                }
            }

            sawExtension = true;
            ProcessBlock(context, block, groupEnded, ref previousReceiver, ref seenReceivers);
        }
    }

    /// <summary>Applies the per-block rules (SST1700/1701/1702/1706/1707) to one extension block.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    /// <param name="groupEnded">Whether a non-extension member already interrupted the block run.</param>
    /// <param name="previousReceiver">The receiver type of the previous block (updated on return).</param>
    /// <param name="seenReceivers">The receiver types already seen in earlier blocks.</param>
    private static void ProcessBlock(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax block,
        bool groupEnded,
        ref string? previousReceiver,
        ref HashSet<string>? seenReceivers)
    {
        var extensionKeyword = block.GetFirstToken();
        var receiverType = ExtensionBlockHelper.ReceiverType(block);

        if (groupEnded)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.GroupExtensionBlocks, block.SyntaxTree, extensionKeyword.Span));
        }

        if (block.Members.Count == 0)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.EmptyExtensionBlock, block.SyntaxTree, extensionKeyword.Span));
        }

        if (ExtensionBlockHelper.IsBroadReceiver(receiverType, out var broadReceiverText))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.BroadExtensionReceiver, block.SyntaxTree, extensionKeyword.Span, broadReceiverText));
        }

        var receiver = ExtensionBlockHelper.ReceiverTypeText(receiverType);
        if (receiver is null)
        {
            return;
        }

        if (previousReceiver is not null && string.CompareOrdinal(receiver, previousReceiver) < 0)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.OrderExtensionBlocks, block.SyntaxTree, extensionKeyword.Span));
        }

        seenReceivers ??= new(StringComparer.Ordinal);
        if (!seenReceivers.Add(receiver))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.CombineExtensionBlocks, block.SyntaxTree, extensionKeyword.Span, receiver));
        }

        previousReceiver = receiver;
    }
}
