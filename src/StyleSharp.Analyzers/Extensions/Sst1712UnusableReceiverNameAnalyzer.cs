// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>extension(Receiver receiver) { … }</c> block that names its receiver but declares only
/// static members (SST1712). A static extension member is declared on the receiver type, so the name is
/// not in scope for any of them and the block names something nothing can read. The fix drops the name,
/// leaving the <c>extension(Receiver)</c> form. An empty block is left to SST1700.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1712UnusableReceiverNameAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ExtensionRules.UnusableReceiverName);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // An extension block has no syntax kind to register on across every Roslyn slot, so the
        // containing class is walked instead.
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    /// <summary>Returns whether every member of a block is static.</summary>
    /// <param name="block">The extension block.</param>
    /// <returns><see langword="true"/> when the block has members and all of them are static.</returns>
    /// <remarks>An empty block names nothing either, but that is the shape SST1700 reports.</remarks>
    internal static bool DeclaresOnlyStaticMembers(TypeDeclarationSyntax block)
    {
        var members = block.Members;
        if (members.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < members.Count; index++)
        {
            if (!ModifierListHelper.Contains(members[index].Modifiers, SyntaxKind.StaticKeyword))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reports every block in a class that names a receiver no member can read.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var containingClass = (ClassDeclarationSyntax)context.Node;
        foreach (var member in containingClass.Members)
        {
            if (member is TypeDeclarationSyntax block && ExtensionBlockHelper.IsExtensionBlock(block))
            {
                AnalyzeBlock(in context, block);
            }
        }
    }

    /// <summary>Reports one block whose receiver name is unusable.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    private static void AnalyzeBlock(in SyntaxNodeAnalysisContext context, TypeDeclarationSyntax block)
    {
        if (block.ParameterList?.Parameters is not { Count: > 0 } parameters
            || parameters[0] is not { Type: { } receiverType } receiver
            || receiver.Identifier.IsKind(SyntaxKind.None)
            || receiver.Identifier.ValueText.Length == 0
            || !DeclaresOnlyStaticMembers(block))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ExtensionRules.UnusableReceiverName,
            receiver.Identifier.GetLocation(),
            receiver.Identifier.ValueText,
            ExtensionBlockHelper.ReceiverTypeText(receiverType) ?? receiverType.ToString()));
    }
}
