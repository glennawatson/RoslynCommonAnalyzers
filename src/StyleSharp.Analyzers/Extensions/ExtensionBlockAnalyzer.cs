// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports issues with C# 14 extension blocks declared in a class: an empty extension block
/// (SST1700), a second extension block that repeats an earlier block's receiver type (SST1701),
/// extension blocks separated by other members (SST1702), a container class not named with an
/// accepted extension-container suffix (SST1704), and classic extension methods mixed in with extension blocks
/// (SST1705). A class with no extension block bails after a single membership scan, so the common
/// case is cheap; on the Roslyn 4.8 floor the syntax cannot occur, so nothing is reported.
/// </summary>
/// <remarks>
/// Diagnostics: SST1700, SST1701, SST1702, SST1704, SST1705, SST1706, SST1707.
/// </remarks>
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
        // 'scan' carries the per-block tracking state (merge keys, previous receiver, seen set).
        var reportedContainerNaming = false;
        var sawExtension = false;
        var groupEnded = false;
        var scan = default(BlockScanState);
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
                if (!ExtensionContainerNaming.HasValidSuffix(declaration.Identifier.ValueText))
                {
                    context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.ExtensionContainerNaming, declaration.SyntaxTree, declaration.Identifier.Span, declaration.Identifier.ValueText));
                }
            }

            sawExtension = true;
            ProcessBlock(context, block, groupEnded, ref scan);
        }
    }

    /// <summary>Applies the per-block rules (SST1700/1701/1702/1706/1707) to one extension block.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    /// <param name="groupEnded">Whether a non-extension member already interrupted the block run.</param>
    /// <param name="scan">The per-block tracking state, updated on return.</param>
    private static void ProcessBlock(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax block,
        bool groupEnded,
        ref BlockScanState scan)
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

        // Two blocks are only mergeable when they share a receiver type AND identical generic
        // constraints; a 'where' clause that differs makes the merge impossible to compile, so the
        // duplicate/combine rule keys on the constraint-aware merge key while ordering and the
        // diagnostic message keep using the plain receiver type text.
        var mergeKey = MergeKey(block, receiver);

        ReportOutOfOrderReceiver(context, block, ref extensionKeyword, receiver, scan.PreviousReceiverDisplay);
        if (TryHandleFirstReceivers(context, block, ref extensionKeyword, receiver, mergeKey, ref scan))
        {
            return;
        }

        scan.SeenKeys ??= CreateSeenKeys(scan.FirstKey!, scan.PreviousKey!);
        ReportDuplicateReceiver(context, block, ref extensionKeyword, receiver, mergeKey, scan.SeenKeys);
        scan.PreviousKey = mergeKey;
        scan.PreviousReceiverDisplay = receiver;
        scan.ExtensionCount++;
    }

    /// <summary>
    /// Builds the key that decides whether two blocks are mergeable: the receiver type text plus the
    /// block's generic constraint clauses. Blocks that share a receiver type but carry different
    /// <c>where</c> constraints produce different keys and so are not reported as combinable. The
    /// common constraint-free case returns the receiver text without allocating.
    /// </summary>
    /// <param name="block">The extension block.</param>
    /// <param name="receiver">The receiver type text.</param>
    /// <returns>The constraint-aware merge key.</returns>
    private static string MergeKey(TypeDeclarationSyntax block, string receiver)
    {
        var constraints = block.ConstraintClauses;
        if (constraints.Count == 0)
        {
            return receiver;
        }

        var builder = new System.Text.StringBuilder(receiver);
        for (var i = 0; i < constraints.Count; i++)
        {
            builder.Append('\u0001').Append(constraints[i].ToString());
        }

        return builder.ToString();
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
    /// <param name="receiver">The current receiver text, used for the diagnostic message.</param>
    /// <param name="mergeKey">The current block's constraint-aware merge key.</param>
    /// <param name="scan">The per-block tracking state, updated on return.</param>
    /// <returns><see langword="true"/> when the receiver was handled entirely.</returns>
    private static bool TryHandleFirstReceivers(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax block,
        ref SyntaxToken extensionKeyword,
        string receiver,
        string mergeKey,
        ref BlockScanState scan)
    {
        switch (scan.ExtensionCount)
        {
            case > 1:
                return false;
            case 0:
                {
                    scan.FirstKey = mergeKey;
                    scan.PreviousKey = mergeKey;
                    scan.PreviousReceiverDisplay = receiver;
                    scan.ExtensionCount = 1;
                    return true;
                }
        }

        if (IsDuplicateImmediateReceiver(mergeKey, scan.PreviousKey!))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.CombineExtensionBlocks, block.SyntaxTree, ExtensionKeywordSpan(block, ref extensionKeyword), receiver));
        }

        scan.PreviousKey = mergeKey;
        scan.PreviousReceiverDisplay = receiver;
        scan.ExtensionCount = SecondExtensionCount;
        return true;
    }

    /// <summary>Creates the seen merge-key set once a third extension block is encountered.</summary>
    /// <param name="firstKey">The first block's merge key.</param>
    /// <param name="previousKey">The most recently seen merge key.</param>
    /// <returns>The initialized merge-key set.</returns>
    private static HashSet<string> CreateSeenKeys(string firstKey, string previousKey)
        => new(StringComparer.Ordinal)
        {
            firstKey,
            previousKey
        };

    /// <summary>Reports SST1701 when the block's merge key was already seen.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    /// <param name="extensionKeyword">The block's extension keyword.</param>
    /// <param name="receiver">The current receiver text, used for the diagnostic message.</param>
    /// <param name="mergeKey">The current block's constraint-aware merge key.</param>
    /// <param name="seenKeys">The merge keys already seen.</param>
    private static void ReportDuplicateReceiver(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax block,
        ref SyntaxToken extensionKeyword,
        string receiver,
        string mergeKey,
        HashSet<string> seenKeys)
    {
        if (seenKeys.Add(mergeKey))
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

    /// <summary>
    /// Mutable tracking state threaded through the per-block extension checks: the running block
    /// count, the first and previous blocks' merge keys, the previous block's displayed receiver
    /// (for ordering), and the lazily created set of merge keys seen from the third block onward.
    /// Passed by <c>ref</c> so a single value carries the whole scan instead of many parameters.
    /// </summary>
    private record struct BlockScanState
    {
        /// <summary>Gets or sets the number of extension blocks seen so far.</summary>
        public int ExtensionCount { get; set; }

        /// <summary>Gets or sets the first block's merge key.</summary>
        public string? FirstKey { get; set; }

        /// <summary>Gets or sets the previous block's merge key.</summary>
        public string? PreviousKey { get; set; }

        /// <summary>Gets or sets the previous block's displayed receiver type, used for ordering.</summary>
        public string? PreviousReceiverDisplay { get; set; }

        /// <summary>Gets or sets the merge keys already seen in earlier blocks.</summary>
        public HashSet<string>? SeenKeys { get; set; }
    }
}
