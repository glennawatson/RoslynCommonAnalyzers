// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports adjacent type or namespace members that are not separated by a blank line
/// (SST1516). Each container is visited once and its members walked pairwise, so the
/// no-diagnostic path costs a single line lookup per member.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ElementSpacingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The container kinds whose members are checked for separation.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.NamespaceDeclaration,
        SyntaxKind.FileScopedNamespaceDeclaration,
        SyntaxKind.CompilationUnit);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.ElementsSeparatedByBlankLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Walks the container's members and reports any pair without a blank line between them.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var members = Members(context.Node);
        if (members.Count < 2)
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        var previous = members[0];
        for (var index = 1; index < members.Count; index++)
        {
            var current = members[index];
            var previousEndLine = LayoutHelpers.EndLine(text, previous.GetLastToken());
            var currentStartLine = LayoutHelpers.ContentStartLine(text, current);

            if (currentStartLine <= previousEndLine + 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(LayoutRules.ElementsSeparatedByBlankLine, current.GetFirstToken().GetLocation()));
            }

            previous = current;
        }
    }

    /// <summary>Returns the member list of a supported container node.</summary>
    /// <param name="node">The container node.</param>
    /// <returns>The container's members, or an empty list.</returns>
    private static SyntaxList<MemberDeclarationSyntax> Members(SyntaxNode node) => node switch
    {
        TypeDeclarationSyntax type => type.Members,
        NamespaceDeclarationSyntax ns => ns.Members,
        FileScopedNamespaceDeclarationSyntax file => file.Members,
        CompilationUnitSyntax unit => unit.Members,
        _ => default,
    };
}
