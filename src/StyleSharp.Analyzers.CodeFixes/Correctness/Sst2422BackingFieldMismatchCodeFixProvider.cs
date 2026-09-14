// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Points a mismatched property's getter at the field its setter writes (SST2422), so the property reads
/// back what it stores.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2422BackingFieldMismatchCodeFixProvider))]
[Shared]
public sealed class Sst2422BackingFieldMismatchCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.BackingFieldMismatch.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var title = !context.Diagnostics.IsEmpty && context.Diagnostics[0].Properties.TryGetValue(Sst2422BackingFieldMismatchAnalyzer.SetterFieldKey, out var name)
            ? $"Return '{name}' from the getter"
            : "Return the setter's field from the getter";
        return ReplaceNodeCodeFix.RegisterAsync(
            context,
            title,
            nameof(Sst2422BackingFieldMismatchCodeFixProvider),
            CanRewrite,
            TryRewrite);
    }

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryFindGetterRead(root, diagnostic, out _, out _);

    /// <summary>Resolves the getter's field read and repoints it at the setter's field.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryFindGetterRead(root, diagnostic, out var read, out var setterField)
            ? new NodeReplacement(read, Repoint(read, setterField))
            : null;

    /// <summary>Resolves the field the reported getter reads and the field the setter writes.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="read">The getter's field-read expression.</param>
    /// <param name="setterField">The name of the field the setter writes.</param>
    /// <returns><see langword="true"/> when both still resolve.</returns>
    private static bool TryFindGetterRead(
        SyntaxNode root,
        Diagnostic diagnostic,
        [NotNullWhen(true)] out ExpressionSyntax? read,
        [NotNullWhen(true)] out string? setterField)
    {
        read = null;
        if (!diagnostic.Properties.TryGetValue(Sst2422BackingFieldMismatchAnalyzer.SetterFieldKey, out setterField)
            || setterField is null
            || root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<PropertyDeclarationSyntax>() is not { AccessorList: { } accessors })
        {
            return false;
        }

        read = GetterFieldRead(accessors);
        return read is not null;
    }

    /// <summary>Rebuilds a field reference to name a different field, keeping its trivia and receiver.</summary>
    /// <param name="read">The original field-reference expression.</param>
    /// <param name="fieldName">The field to name instead.</param>
    /// <returns>The repointed expression.</returns>
    private static ExpressionSyntax Repoint(ExpressionSyntax read, string fieldName) =>
        read is MemberAccessExpressionSyntax member
            ? member.WithName(SyntaxFactory.IdentifierName(fieldName))
            : SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(read.GetLeadingTrivia(), fieldName, read.GetTrailingTrivia()));

    /// <summary>Gets the single field a property's getter reads, when its body reduces to one.</summary>
    /// <param name="accessors">The property's accessor list.</param>
    /// <returns>The field-read expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? GetterFieldRead(AccessorListSyntax accessors)
    {
        var list = accessors.Accessors;
        for (var i = 0; i < list.Count; i++)
        {
            if (!list[i].IsKind(SyntaxKind.GetAccessorDeclaration))
            {
                continue;
            }

            return Sst2422BackingFieldMismatchAnalyzer.GetterFieldRead(list[i]);
        }

        return null;
    }
}
