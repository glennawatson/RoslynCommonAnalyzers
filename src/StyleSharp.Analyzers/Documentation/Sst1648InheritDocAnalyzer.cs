// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>&lt;inheritdoc&gt;</c> element on an element that has nothing to inherit
/// documentation from (SST1648) — it is not an override, an interface implementation, nor a
/// type with a documented base type or interface.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1648InheritDocAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The declaration kinds that may legitimately carry inheritdoc.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.MethodDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.EventDeclaration);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DocumentationRules.InheritDocValid);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Reports an inheritdoc element that has no base to inherit documentation from.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var documentation = XmlDocumentationHelper.GetDocumentationComment(context.Node);
        if (documentation is null || !XmlDocumentationHelper.IsInheritDoc(documentation))
        {
            return;
        }

        // An explicit interface specifier in the syntax (e.g. `object IFoo.Bar`) is a
        // definitive interface implementation regardless of whether the interface type
        // currently binds. Checking it here avoids a false positive when the interface
        // lives in another assembly that is momentarily unresolved (incomplete semantics),
        // where the semantic ExplicitInterfaceImplementations set comes back empty.
        if (HasExplicitInterfaceSpecifier(context.Node))
        {
            return;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken);
        if (symbol is null || Inherits(symbol))
        {
            return;
        }

        var name = MemberOrder.NameToken((MemberDeclarationSyntax)context.Node);
        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.InheritDocValid, name.GetLocation(), name.ValueText));
    }

    /// <summary>Returns whether the symbol inherits or implements a base it can inherit documentation from.</summary>
    /// <param name="symbol">The declared symbol.</param>
    /// <returns><see langword="true"/> when inheritdoc is appropriate.</returns>
    private static bool Inherits(ISymbol symbol) =>
        symbol is INamedTypeSymbol type
            ? type.Interfaces.Length > 0
              || (type.TypeKind == TypeKind.Class && type.BaseType is { SpecialType: not SpecialType.System_Object })
            : symbol.IsOverride || ImplementsInterfaceMember(symbol);

    /// <summary>Returns whether the member declares an explicit interface specifier in its syntax.</summary>
    /// <param name="node">The member declaration syntax.</param>
    /// <returns><see langword="true"/> when the member explicitly implements an interface member.</returns>
    private static bool HasExplicitInterfaceSpecifier(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.ExplicitInterfaceSpecifier is not null,
        PropertyDeclarationSyntax property => property.ExplicitInterfaceSpecifier is not null,
        IndexerDeclarationSyntax indexer => indexer.ExplicitInterfaceSpecifier is not null,
        EventDeclarationSyntax @event => @event.ExplicitInterfaceSpecifier is not null,
        _ => false
    };

    /// <summary>Returns whether the member implicitly implements an interface member of its containing type.</summary>
    /// <param name="symbol">The member symbol.</param>
    /// <returns><see langword="true"/> when the member implements an interface member.</returns>
    private static bool ImplementsInterfaceMember(ISymbol symbol)
    {
        var containingType = symbol.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        foreach (var @interface in containingType.AllInterfaces)
        {
            foreach (var member in @interface.GetMembers())
            {
                var implementation = containingType.FindImplementationForInterfaceMember(member);
                if (SymbolEqualityComparer.Default.Equals(implementation, symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
