// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

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
    /// <summary>The block count at which duplicate tracking must cover more than the previous receiver.</summary>
    private const int SecondExtensionCount = 2;

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

    /// <summary>Returns whether the current receiver sorts before the previous receiver.</summary>
    /// <param name="receiver">The current receiver text.</param>
    /// <param name="previousReceiver">The previous receiver text, when any.</param>
    /// <returns><see langword="true"/> when the current receiver is out of lexical order.</returns>
    internal static bool IsOutOfOrderReceiver(string receiver, string? previousReceiver)
        => previousReceiver is not null && string.CompareOrdinal(receiver, previousReceiver) < 0;

    /// <summary>Returns whether a second extension block repeats the immediately previous receiver.</summary>
    /// <param name="receiver">The current receiver text.</param>
    /// <param name="previousReceiver">The previous receiver text.</param>
    /// <returns><see langword="true"/> when both receivers are ordinally equal.</returns>
    internal static bool IsDuplicateImmediateReceiver(string receiver, string previousReceiver)
        => string.Equals(receiver, previousReceiver, StringComparison.Ordinal);

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
        var extensionCount = 0;
        string? firstReceiver = null;
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
            ProcessBlock(context, block, groupEnded, ref extensionCount, ref firstReceiver, ref previousReceiver, ref seenReceivers);
        }
    }

    /// <summary>Applies the per-block rules (SST1700/1701/1702/1706/1707) to one extension block.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    /// <param name="groupEnded">Whether a non-extension member already interrupted the block run.</param>
    /// <param name="extensionCount">The number of extension blocks seen so far.</param>
    /// <param name="firstReceiver">The receiver text of the first block in the container.</param>
    /// <param name="previousReceiver">The receiver type of the previous block (updated on return).</param>
    /// <param name="seenReceivers">The receiver types already seen in earlier blocks.</param>
    private static void ProcessBlock(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax block,
        bool groupEnded,
        ref int extensionCount,
        ref string? firstReceiver,
        ref string? previousReceiver,
        ref HashSet<string>? seenReceivers)
    {
        var extensionKeyword = default(SyntaxToken);
        var receiverType = ExtensionBlockHelper.ReceiverType(block);

        if (groupEnded)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.GroupExtensionBlocks, block.SyntaxTree, ExtensionKeywordSpan(block, ref extensionKeyword)));
        }

        if (block.Members.Count == 0)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.EmptyExtensionBlock, block.SyntaxTree, ExtensionKeywordSpan(block, ref extensionKeyword)));
        }

        string? receiver;
        if (ExtensionBlockHelper.TryClassifyReceiver(receiverType, out var classifiedReceiver, out var isBroadReceiver))
        {
            receiver = classifiedReceiver!;
            if (isBroadReceiver)
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.BroadExtensionReceiver, block.SyntaxTree, ExtensionKeywordSpan(block, ref extensionKeyword), receiver));
            }
        }
        else if (ExtensionBlockHelper.IsBroadReceiver(receiverType, out var broadReceiverText))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.BroadExtensionReceiver, block.SyntaxTree, ExtensionKeywordSpan(block, ref extensionKeyword), broadReceiverText));
            receiver = broadReceiverText;
        }
        else
        {
            receiver = ExtensionBlockHelper.ReceiverTypeText(receiverType);
            if (receiver is null)
            {
                return;
            }
        }

        ReportOutOfOrderReceiver(context, block, ref extensionKeyword, receiver, previousReceiver);
        if (TryHandleFirstReceivers(context, block, ref extensionKeyword, receiver, ref extensionCount, ref firstReceiver, ref previousReceiver))
        {
            return;
        }

        seenReceivers ??= CreateSeenReceivers(firstReceiver!, previousReceiver!);
        ReportDuplicateReceiver(context, block, ref extensionKeyword, receiver, seenReceivers);
        previousReceiver = receiver;
        extensionCount++;
    }

    /// <summary>Reports SST1707 when the current receiver sorts before the previous receiver.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    /// <param name="extensionKeyword">The block's extension keyword.</param>
    /// <param name="receiver">The current receiver text.</param>
    /// <param name="previousReceiver">The previous receiver text, when any.</param>
    private static void ReportOutOfOrderReceiver(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax block,
        ref SyntaxToken extensionKeyword,
        string receiver,
        string? previousReceiver)
    {
        if (!IsOutOfOrderReceiver(receiver, previousReceiver))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.OrderExtensionBlocks, block.SyntaxTree, ExtensionKeywordSpan(block, ref extensionKeyword)));
    }

    /// <summary>Handles the first and second receivers without allocating the seen-receiver set.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    /// <param name="extensionKeyword">The block's extension keyword.</param>
    /// <param name="receiver">The current receiver text.</param>
    /// <param name="extensionCount">The number of extension blocks seen so far.</param>
    /// <param name="firstReceiver">The first receiver text.</param>
    /// <param name="previousReceiver">The previous receiver text.</param>
    /// <returns><see langword="true"/> when the receiver was handled entirely.</returns>
    private static bool TryHandleFirstReceivers(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax block,
        ref SyntaxToken extensionKeyword,
        string receiver,
        ref int extensionCount,
        ref string? firstReceiver,
        ref string? previousReceiver)
    {
        if (extensionCount > 1)
        {
            return false;
        }

        if (extensionCount == 0)
        {
            firstReceiver = receiver;
            previousReceiver = receiver;
            extensionCount = 1;
            return true;
        }

        if (IsDuplicateImmediateReceiver(receiver, previousReceiver!))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.CombineExtensionBlocks, block.SyntaxTree, ExtensionKeywordSpan(block, ref extensionKeyword), receiver));
        }

        previousReceiver = receiver;
        extensionCount = SecondExtensionCount;
        return true;
    }

    /// <summary>Creates the seen-receiver set once a third extension block is encountered.</summary>
    /// <param name="firstReceiver">The first receiver text.</param>
    /// <param name="previousReceiver">The most recently seen receiver text.</param>
    /// <returns>The initialized receiver set.</returns>
    private static HashSet<string> CreateSeenReceivers(string firstReceiver, string previousReceiver)
        => new(StringComparer.Ordinal)
        {
            firstReceiver,
            previousReceiver,
        };

    /// <summary>Reports SST1701 when the receiver type was already seen.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    /// <param name="extensionKeyword">The block's extension keyword.</param>
    /// <param name="receiver">The current receiver text.</param>
    /// <param name="seenReceivers">The receiver types already seen.</param>
    private static void ReportDuplicateReceiver(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax block,
        ref SyntaxToken extensionKeyword,
        string receiver,
        HashSet<string> seenReceivers)
    {
        if (seenReceivers.Add(receiver))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.CombineExtensionBlocks, block.SyntaxTree, ExtensionKeywordSpan(block, ref extensionKeyword), receiver));
    }

    /// <summary>Returns the extension keyword span, reading the token lazily only when needed.</summary>
    /// <param name="block">The extension block.</param>
    /// <param name="extensionKeyword">The cached extension keyword token.</param>
    /// <returns>The extension keyword span.</returns>
    private static TextSpan ExtensionKeywordSpan(TypeDeclarationSyntax block, ref SyntaxToken extensionKeyword)
    {
        if (extensionKeyword.RawKind == 0)
        {
            extensionKeyword = block.GetFirstToken();
        }

        return extensionKeyword.Span;
    }
}
