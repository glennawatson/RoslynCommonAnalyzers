// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a non-private instance <c>readonly</c> field whose type is a mutable collection (SST2322).
/// <c>readonly</c> stops the field being reassigned, but it does nothing to the collection it points at, so
/// a caller that can see the field can still add, remove, or clear its items.
/// </summary>
/// <remarks>
/// <para>
/// "Mutable collection" is exactly the set <see cref="MutableCollectionTypes"/> names — a <c>List&lt;T&gt;</c>,
/// a <c>Dictionary&lt;,&gt;</c>, a <c>HashSet&lt;T&gt;</c>, a <c>Collection&lt;T&gt;</c>, an array, and the rest —
/// so an immutable or frozen collection, a read-only wrapper, an <c>IReadOnlyList&lt;T&gt;</c>, a scalar, or any
/// user type passes without a word.
/// </para>
/// <para>
/// The scope is deliberately the complement of two neighbouring rules and never overlaps them. A <c>private</c>
/// field keeps the collection under the type's own control and is left alone; a <c>static</c> field is shared
/// state rather than a member of an instance's surface, and is a separate concern. Only a field that is
/// visible outside the type, is an instance field, and is <c>readonly</c> is a candidate here.
/// </para>
/// <para>
/// The clean path is a modifier scan: a field that is not <c>readonly</c>, that is <c>static</c>, or that names
/// no accessibility beyond private is rejected on syntax alone, before the semantic model is touched. Only a
/// field that is already non-private, instance, and <c>readonly</c> pays for a bind, and even then only to
/// learn whether its type is one of the mutable shapes.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2322ReadonlyMutableCollectionFieldAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.ReadonlyMutableCollectionField);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var mutableTypes = new MutableCollectionTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, mutableTypes),
                SyntaxKind.FieldDeclaration);
        });
    }

    /// <summary>Reports each declarator of a visible instance readonly field that holds a mutable collection.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="mutableTypes">The known mutable collection types.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, MutableCollectionTypes mutableTypes)
    {
        var declaration = (FieldDeclarationSyntax)context.Node;
        if (!IsVisibleInstanceReadonly(declaration.Modifiers))
        {
            return;
        }

        // Every declarator in one declaration shares the field's type and accessibility, so one bind settles them all.
        var variables = declaration.Declaration.Variables;
        if (context.SemanticModel.GetDeclaredSymbol(variables[0], context.CancellationToken) is not IFieldSymbol field
            || !mutableTypes.IsMutable(field.Type))
        {
            return;
        }

        for (var i = 0; i < variables.Count; i++)
        {
            var identifier = variables[i].Identifier;
            context.ReportDiagnostic(DiagnosticHelper.Create(
                DesignRules.ReadonlyMutableCollectionField,
                identifier.GetLocation(),
                identifier.ValueText));
        }
    }

    /// <summary>Returns whether a field declaration is a non-private instance <c>readonly</c> field.</summary>
    /// <param name="modifiers">The field declaration's modifiers.</param>
    /// <returns><see langword="true"/> when the declaration is worth binding.</returns>
    /// <remarks>
    /// A field with no accessibility keyword is private, and a <c>private protected</c> field carries the
    /// <c>private</c> keyword, so a declaration that names <c>private</c> at all is rejected here — on syntax
    /// alone, before any symbol is touched. A <c>static</c> field is out of scope, and a field with no
    /// <c>readonly</c> keyword is a different, more obvious problem this rule leaves to others.
    /// </remarks>
    private static bool IsVisibleInstanceReadonly(SyntaxTokenList modifiers)
    {
        if (!ModifierListHelper.Contains(modifiers, SyntaxKind.ReadOnlyKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.StaticKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.PrivateKeyword))
        {
            return false;
        }

        return ModifierListHelper.Contains(modifiers, SyntaxKind.PublicKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.ProtectedKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.InternalKeyword);
    }
}
