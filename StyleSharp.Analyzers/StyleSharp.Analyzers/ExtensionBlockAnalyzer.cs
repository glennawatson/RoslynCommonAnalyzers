// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports issues with C# 14 extension blocks declared in a class: an empty extension block
/// (SST1700) and a second extension block that repeats an earlier block's receiver type (SST1701).
/// The class members are walked once with an inner look-back over earlier members, so no
/// intermediate collection is allocated; classes with no extension blocks pay only the membership
/// test. On the Roslyn 4.8 floor the syntax cannot occur, so nothing is reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExtensionBlockAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ExtensionRules.EmptyExtensionBlock,
        ExtensionRules.CombineExtensionBlocks);

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
        var members = ((ClassDeclarationSyntax)context.Node).Members;
        for (var index = 0; index < members.Count; index++)
        {
            if (members[index] is not TypeDeclarationSyntax block || !ExtensionBlockHelper.IsExtensionBlock(block))
            {
                continue;
            }

            if (block.Members.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(ExtensionRules.EmptyExtensionBlock, block.GetFirstToken().GetLocation()));
            }

            ReportDuplicateReceiver(context, members, index, block);
        }
    }

    /// <summary>Reports the block when an earlier extension block in the same class shares its receiver type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="members">The class members.</param>
    /// <param name="index">The index of the current block.</param>
    /// <param name="block">The current extension block.</param>
    private static void ReportDuplicateReceiver(SyntaxNodeAnalysisContext context, SyntaxList<MemberDeclarationSyntax> members, int index, TypeDeclarationSyntax block)
    {
        var receiver = ExtensionBlockHelper.ReceiverTypeText(block);
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
                context.ReportDiagnostic(Diagnostic.Create(ExtensionRules.CombineExtensionBlocks, block.GetFirstToken().GetLocation(), receiver));
                return;
            }
        }
    }
}
