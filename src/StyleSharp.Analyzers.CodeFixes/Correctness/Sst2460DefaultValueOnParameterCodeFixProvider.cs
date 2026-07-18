// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces a parameter's inert <c>[DefaultValue]</c> with the interop
/// <c>[DefaultParameterValue]</c> that records the intended default (SST2460).
/// </summary>
/// <remarks>
/// Only the single-value form has an interop counterpart, so the two-argument converter form
/// (<c>[DefaultValue(typeof(T), "...")]</c>) is left for the author, and the fix is withheld when the stored
/// value cannot be the parameter's default. The rewrite keeps the source's suffix choice
/// (<c>DefaultParameterValue</c> vs <c>DefaultParameterValueAttribute</c>) and qualifies the name with
/// <c>global::System.Runtime.InteropServices</c> when that namespace is not already imported. It never adds
/// <c>[Optional]</c>; the author decides whether the parameter should actually become optional.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2460DefaultValueOnParameterCodeFixProvider))]
[Shared]
public sealed class Sst2460DefaultValueOnParameterCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The namespace that owns the interop parameter-default attribute.</summary>
    private const string InteropNamespace = "System.Runtime.InteropServices";

    /// <summary>The interop attribute's unqualified spelling without the redundant suffix.</summary>
    private const string DefaultParameterValueName = "DefaultParameterValue";

    /// <summary>The interop attribute's unqualified spelling with the explicit suffix.</summary>
    private const string DefaultParameterValueSuffixedName = "DefaultParameterValueAttribute";

    /// <summary>The suffix an attribute name carries when it is spelled in full.</summary>
    private const string AttributeSuffix = "Attribute";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(CorrectnessRules.DefaultValueOnParameter.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use [DefaultParameterValue]",
            nameof(Sst2460DefaultValueOnParameterCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported attribute and rewrites it to the interop attribute.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no safe rewrite exists.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<AttributeSyntax>() is not { } attribute
            || attribute.ArgumentList is not { Arguments.Count: 1 } argumentList)
        {
            return null;
        }

        var argument = argumentList.Arguments[0];
        if (argument is not { NameEquals: null, NameColon: null }
            || model.Compilation.GetTypeByMetadataName(Sst2460DefaultValueOnParameterAnalyzer.DefaultParameterValueMetadataName) is not { } interopAttribute
            || attribute.FirstAncestorOrSelf<ParameterSyntax>() is not { } parameter
            || model.GetDeclaredSymbol(parameter) is not IParameterSymbol parameterSymbol
            || !model.ClassifyConversion(argument.Expression, parameterSymbol.Type).IsImplicit)
        {
            return null;
        }

        var name = BuildName(attribute.Name, interopAttribute, model, attribute.SpanStart);
        return new NodeReplacement(attribute, attribute.WithName(name));
    }

    /// <summary>Builds the interop attribute name, keeping the source's suffix and qualifying when needed.</summary>
    /// <param name="originalName">The reported attribute's name syntax.</param>
    /// <param name="interopAttribute">The resolved interop attribute symbol.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The position at which the name is written.</param>
    /// <returns>The replacement name syntax.</returns>
    private static NameSyntax BuildName(NameSyntax originalName, INamedTypeSymbol interopAttribute, SemanticModel model, int position)
    {
        var suffixed = SimpleName(originalName)?.EndsWith(AttributeSuffix, StringComparison.Ordinal) == true;
        var baseName = suffixed ? DefaultParameterValueSuffixedName : DefaultParameterValueName;

        // ToMinimalDisplayString drops the namespace only when it is already imported; a remaining dot means
        // the name must be qualified, and it is qualified from the root so it binds regardless of local names.
        var text = interopAttribute.ToMinimalDisplayString(model, position).IndexOf('.') < 0
            ? baseName
            : "global::" + InteropNamespace + "." + baseName;

        return SyntaxFactory.ParseName(text).WithTriviaFrom(originalName);
    }

    /// <summary>Reduces an attribute name to its rightmost identifier text.</summary>
    /// <param name="name">The attribute name syntax.</param>
    /// <returns>The simple identifier text, or <see langword="null"/> for an unexpected shape.</returns>
    private static string? SimpleName(NameSyntax name) => name switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => null,
    };
}
