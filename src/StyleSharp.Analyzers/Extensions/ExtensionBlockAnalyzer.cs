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
/// (SST1705). It also reports a broad extension receiver (SST1706) on either style: an extension block whose
/// receiver is <c>object</c>/<c>dynamic</c>, and a classic <c>this</c>-parameter extension method whose
/// receiver is <c>object</c>, <c>dynamic</c>, or an unconstrained type parameter (<c>this T</c>) — each
/// attaches the member to every type in the solution. A class with no extension block bails after a single
/// membership scan, so the common case is cheap; on the Roslyn 4.8 floor the block syntax cannot occur.
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
        context.RegisterSyntaxNodeAction(AnalyzeClassicExtensionMethod, SyntaxKind.MethodDeclaration);
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

    /// <summary>Reports SST1706 for a classic <c>this</c>-parameter extension method on a broad receiver.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <remarks>
    /// This is the classic-method half of SST1706. Extension-block receivers are handled in the class walk;
    /// a classic method carries its receiver on the first parameter, so it is matched here on the parameter's
    /// <c>this</c> modifier. The check stays syntactic: <c>object</c>/<c>dynamic</c>/<c>System.Object</c> by
    /// text, and an unconstrained type parameter by the absence of a matching constraint clause.
    /// </remarks>
    private static void AnalyzeClassicExtensionMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!ExtensionBlockHelper.IsClassicExtensionMethod(method))
        {
            return;
        }

        var receiverType = method.ParameterList.Parameters[0].Type;
        if (!TryGetBroadReceiverText(method, receiverType, out var receiverText))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ExtensionRules.BroadExtensionReceiver, receiverType!.GetLocation(), receiverText));
    }

    /// <summary>Returns the broad-receiver display text for a classic extension method, when the receiver is broad.</summary>
    /// <param name="method">The classic extension method.</param>
    /// <param name="receiverType">The receiver parameter's type syntax.</param>
    /// <param name="text">The broad receiver text when matched.</param>
    /// <returns><see langword="true"/> for <c>object</c>, <c>dynamic</c>, or an unconstrained type parameter.</returns>
    private static bool TryGetBroadReceiverText(MethodDeclarationSyntax method, TypeSyntax? receiverType, out string text)
        => ExtensionBlockHelper.IsBroadReceiver(receiverType, out text)
            || IsUnconstrainedTypeParameterReceiver(method, receiverType, out text);

    /// <summary>Returns whether a classic extension receiver is one of the method's unconstrained type parameters.</summary>
    /// <param name="method">The classic extension method.</param>
    /// <param name="receiverType">The receiver parameter's type syntax.</param>
    /// <param name="text">The type parameter name when matched.</param>
    /// <returns><see langword="true"/> for <c>this T</c> where <c>T</c> is a type parameter with no constraint.</returns>
    private static bool IsUnconstrainedTypeParameterReceiver(MethodDeclarationSyntax method, TypeSyntax? receiverType, out string text)
    {
        text = string.Empty;
        if (receiverType is not IdentifierNameSyntax identifier || method.TypeParameterList is not { } typeParameters)
        {
            return false;
        }

        var name = identifier.Identifier.ValueText;
        if (!NamesTypeParameter(typeParameters, name) || HasConstraintClause(method.ConstraintClauses, name))
        {
            return false;
        }

        text = name;
        return true;
    }

    /// <summary>Returns whether a type-parameter list declares a parameter of the given name.</summary>
    /// <param name="typeParameters">The method's type parameter list.</param>
    /// <param name="name">The candidate type parameter name.</param>
    /// <returns><see langword="true"/> when the name is a declared type parameter.</returns>
    private static bool NamesTypeParameter(TypeParameterListSyntax typeParameters, string name)
    {
        var parameters = typeParameters.Parameters;
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a constraint clause narrows the given type parameter.</summary>
    /// <param name="constraintClauses">The method's constraint clauses.</param>
    /// <param name="name">The type parameter name.</param>
    /// <returns><see langword="true"/> when a <c>where</c> clause targets the name.</returns>
    private static bool HasConstraintClause(SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses, string name)
    {
        for (var i = 0; i < constraintClauses.Count; i++)
        {
            if (constraintClauses[i].Name.Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

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
