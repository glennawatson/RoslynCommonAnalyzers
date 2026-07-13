// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a static field that is visible outside its type and can still be changed (SST1499) — either
/// because it is not <c>readonly</c>, or because it is a <c>readonly</c> array or mutable collection whose
/// contents any caller can rewrite.
/// </summary>
/// <remarks>
/// <para>
/// A <c>const</c> field is a value, not a variable, and is never reported. A field nobody outside the type
/// can see is not global state, so a private field — and a field of any accessibility inside a private
/// nested type — is left alone. <c>[ThreadStatic]</c> says the state is deliberately per-thread, which is
/// the opposite of the shared state this rule is about.
/// </para>
/// <para>
/// A <c>readonly</c> field is only reported when its type is one whose contents are known to be changeable:
/// an array, or a collection named in <see cref="MutableCollectionTypes"/>. Anything else — a string, a
/// number, an immutable or frozen collection, a read-only wrapper, a type you wrote — passes. An analyzer
/// cannot prove a hand-written type immutable, and the alternative to admitting that is reporting every
/// singleton in the codebase.
/// </para>
/// <para>
/// The clean path is a modifier scan: a field that is not <c>static</c>, or that declares no accessibility
/// keyword at all (and so is private), is rejected before the semantic model is touched. Only a field that
/// is already static and already named as visible pays for a bind.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1499MutableStaticFieldAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.MutableStaticField);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var mutableTypes = new MutableCollectionTypes(start.Compilation);
            var optionsByTree = new ConcurrentDictionary<SyntaxTree, MutableStaticFieldOptions>();
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, mutableTypes, optionsByTree),
                SyntaxKind.FieldDeclaration);
        });
    }

    /// <summary>Returns whether a field declaration can still be changed once it is constructed.</summary>
    /// <param name="declaration">The field declaration.</param>
    /// <param name="type">The field's type.</param>
    /// <param name="mutableTypes">The known mutable collection types.</param>
    /// <returns><see langword="true"/> when the field, or the collection it holds, can be rewritten.</returns>
    internal static bool IsMutable(FieldDeclarationSyntax declaration, ITypeSymbol type, MutableCollectionTypes mutableTypes)
        => !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.ReadOnlyKeyword) || mutableTypes.IsMutable(type);

    /// <summary>Reports a visible static field whose value or contents can be changed.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="mutableTypes">The known mutable collection types.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        MutableCollectionTypes mutableTypes,
        ConcurrentDictionary<SyntaxTree, MutableStaticFieldOptions> optionsByTree)
    {
        var declaration = (FieldDeclarationSyntax)context.Node;
        if (!IsDeclaredVisibleStatic(declaration))
        {
            return;
        }

        var variables = declaration.Declaration.Variables;
        if (context.SemanticModel.GetDeclaredSymbol(variables[0], context.CancellationToken) is not IFieldSymbol field
            || !IsVisibleOutsideItsType(field, GetOptions(context, optionsByTree))
            || !IsMutable(declaration, field.Type, mutableTypes))
        {
            return;
        }

        for (var i = 0; i < variables.Count; i++)
        {
            var identifier = variables[i].Identifier;
            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.MutableStaticField,
                identifier.GetLocation(),
                identifier.ValueText));
        }
    }

    /// <summary>Returns whether a field declaration is a static one that names an accessibility beyond private.</summary>
    /// <param name="declaration">The field declaration.</param>
    /// <returns><see langword="true"/> when the declaration is worth binding.</returns>
    /// <remarks>
    /// A field that declares no accessibility keyword is private, and a <c>const</c> is a value rather than
    /// a variable, so both are rejected here — on syntax alone, before any symbol is touched.
    /// </remarks>
    private static bool IsDeclaredVisibleStatic(FieldDeclarationSyntax declaration)
    {
        var modifiers = declaration.Modifiers;
        if (!ModifierListHelper.Contains(modifiers, SyntaxKind.StaticKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.ConstKeyword))
        {
            return false;
        }

        if (!ModifierListHelper.Contains(modifiers, SyntaxKind.PublicKeyword)
            && !ModifierListHelper.Contains(modifiers, SyntaxKind.ProtectedKeyword)
            && !ModifierListHelper.Contains(modifiers, SyntaxKind.InternalKeyword))
        {
            return false;
        }

        return !HasThreadStaticAttribute(declaration.AttributeLists);
    }

    /// <summary>Returns whether the field is visible to code outside the type that declares it.</summary>
    /// <param name="field">The field symbol.</param>
    /// <param name="options">The resolved settings.</param>
    /// <returns><see langword="true"/> when callers outside the type can reach the field.</returns>
    /// <remarks>
    /// The containing types are walked as well as the field: a <c>public</c> field of a <c>private</c>
    /// nested class is only reachable from the class that nests it, and an assembly-visible one is only
    /// global inside the assembly — which <c>include_internal</c> decides whether to report.
    /// </remarks>
    private static bool IsVisibleOutsideItsType(IFieldSymbol field, in MutableStaticFieldOptions options)
    {
        var beyondAssembly = IsVisibleBeyondAssembly(field.DeclaredAccessibility);
        var beyondType = field.DeclaredAccessibility != Accessibility.Private;
        for (var type = field.ContainingType; type is not null; type = type.ContainingType)
        {
            beyondAssembly &= IsVisibleBeyondAssembly(type.DeclaredAccessibility);
            beyondType &= type.DeclaredAccessibility != Accessibility.Private;
        }

        return beyondAssembly || (options.IncludeInternal && beyondType);
    }

    /// <summary>Returns whether an accessibility lets code in another assembly see the declaration.</summary>
    /// <param name="accessibility">The declared accessibility.</param>
    /// <returns><see langword="true"/> when the declaration is part of the assembly's public surface.</returns>
    private static bool IsVisibleBeyondAssembly(Accessibility accessibility)
        => accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;

    /// <summary>Returns whether a field declaration carries the per-thread attribute.</summary>
    /// <param name="lists">The declaration's attribute lists.</param>
    /// <returns><see langword="true"/> when the state is deliberately per-thread.</returns>
    /// <remarks>The attribute is matched on its name; binding it would cost a lookup to learn nothing more.</remarks>
    private static bool HasThreadStaticAttribute(SyntaxList<AttributeListSyntax> lists)
    {
        for (var i = 0; i < lists.Count; i++)
        {
            var attributes = lists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (GetSimpleName(attributes[j].Name) is "ThreadStatic" or "ThreadStaticAttribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased name.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>Reads the settings for the field's tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns>The resolved settings.</returns>
    private static MutableStaticFieldOptions GetOptions(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, MutableStaticFieldOptions> optionsByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (optionsByTree.TryGetValue(tree, out var options))
        {
            return options;
        }

        options = MutableStaticFieldOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        optionsByTree.TryAdd(tree, options);
        return options;
    }
}
