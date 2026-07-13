// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a property whose type is a mutable collection and whose <c>set</c> accessor lets a caller
/// replace that collection outright (SST2305). Callers want to add and remove items — which a get-only
/// property already allows — not to swap the instance the type, its subscriptions, and everything else
/// holding a reference are still pointing at.
/// </summary>
/// <remarks>
/// <para>
/// The rule reports the setter, never the getter's contents; a getter that hands back a fresh copy on
/// every read is a different problem with a different answer.
/// </para>
/// <para>
/// Every shape the rule leaves alone is recognized on syntax alone, so nothing is bound until a
/// property has an ordinary, caller-visible <c>set</c> accessor: an <c>init</c> accessor is a different
/// syntax kind, and <c>private set</c>, <c>required</c>, <c>override</c>, an explicit interface
/// implementation, and any attribute on the property or its containing type all reject before the
/// semantic model is touched. The attribute test is the serialization escape hatch: a contract that
/// needs a setter says so with an attribute, and being conservative there costs one missed report and
/// saves a stream of false ones.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.CollectionPropertyShouldBeReadOnly);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Finds the <c>set</c> accessor a fix could remove, rejecting every exempt shape on syntax alone.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns>The removable setter, or <see langword="null"/> when the property is not a candidate.</returns>
    /// <remarks>
    /// An <c>init</c> accessor parses as its own kind and is therefore never returned: the object is
    /// built once and then settled, which is exactly what the rule is asking for.
    /// </remarks>
    internal static AccessorDeclarationSyntax? FindRemovableSetter(PropertyDeclarationSyntax property)
    {
        if (property.AccessorList is not { } accessorList || IsExemptDeclaration(property))
        {
            return null;
        }

        var accessors = accessorList.Accessors;
        for (var i = 0; i < accessors.Count; i++)
        {
            var accessor = accessors[i];
            if (!accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                continue;
            }

            var isCallerFacing = accessor.AttributeLists.Count == 0
                && !ModifierListHelper.Contains(accessor.Modifiers, SyntaxKind.PrivateKeyword);
            return isCallerFacing ? accessor : null;
        }

        return null;
    }

    /// <summary>Reports one property that hands a caller the power to replace its collection.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (FindRemovableSetter(property) is null)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken) is not { } symbol
            || !CollectionTypeClassification.IsMutableCollection(symbol.Type)
            || InterfaceImplementationLookup.ImplementsInterfaceMember(symbol))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            DesignRules.CollectionPropertyShouldBeReadOnly,
            property.Identifier.GetLocation(),
            property.Identifier.ValueText));
    }

    /// <summary>Returns whether the declaration itself puts the property outside the rule.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns><see langword="true"/> when the setter is not the author's to remove, or is deliberate.</returns>
    /// <remarks>
    /// A <c>required</c> property must stay settable for an object initializer to satisfy it, and a
    /// <c>private set</c> keeps the collection under the type's own control. An attribute — on the
    /// property or on its type — is the serialization escape hatch: a contract that needs a setter says
    /// so, the rule cannot know every attribute that means it, and so it steps back from all of them.
    /// </remarks>
    private static bool IsExemptDeclaration(PropertyDeclarationSyntax property)
        => property.AttributeLists.Count > 0
            || property.ExplicitInterfaceSpecifier is not null
            || ModifierListHelper.ContainsEither(property.Modifiers, SyntaxKind.PrivateKeyword, SyntaxKind.OverrideKeyword)
            || ModifierListHelper.Contains(property.Modifiers, SyntaxKind.RequiredKeyword)
            || property.Parent is BaseTypeDeclarationSyntax { AttributeLists.Count: > 0 };
}
