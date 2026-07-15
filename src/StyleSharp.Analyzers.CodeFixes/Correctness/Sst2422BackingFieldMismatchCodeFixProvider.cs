// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Points a mismatched property's getter at the field its setter writes (SST2422), so the property reads
/// back what it stores.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2422BackingFieldMismatchCodeFixProvider))]
[Shared]
public sealed class Sst2422BackingFieldMismatchCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.BackingFieldMismatch.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var title = context.Diagnostics.Length > 0 && context.Diagnostics[0].Properties.TryGetValue(Sst2422BackingFieldMismatchAnalyzer.SetterFieldKey, out var name)
            ? $"Return '{name}' from the getter"
            : "Return the setter's field from the getter";
        return ReplaceNodeCodeFix.RegisterAsync(
            context,
            title,
            nameof(Sst2422BackingFieldMismatchCodeFixProvider),
            TryRewrite);
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the getter's field read and repoints it at the setter's field.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (!diagnostic.Properties.TryGetValue(Sst2422BackingFieldMismatchAnalyzer.SetterFieldKey, out var setterField)
            || setterField is null
            || root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<PropertyDeclarationSyntax>() is not { AccessorList: { } accessors }
            || GetterFieldRead(accessors) is not { } read)
        {
            return null;
        }

        return new NodeReplacement(read, Repoint(read, setterField));
    }

    /// <summary>Rebuilds a field reference to name a different field, keeping its trivia and receiver.</summary>
    /// <param name="read">The original field-reference expression.</param>
    /// <param name="fieldName">The field to name instead.</param>
    /// <returns>The repointed expression.</returns>
    private static ExpressionSyntax Repoint(ExpressionSyntax read, string fieldName)
        => read is MemberAccessExpressionSyntax member
            ? member.WithName(SyntaxFactory.IdentifierName(fieldName))
            : SyntaxFactory.IdentifierName(fieldName).WithTriviaFrom(read);

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

            var getter = list[i];
            if (getter.ExpressionBody is { Expression: { } expression })
            {
                return AsFieldReference(expression);
            }

            return getter.Body is { Statements: [ReturnStatementSyntax { Expression: { } returned }] } ? AsFieldReference(returned) : null;
        }

        return null;
    }

    /// <summary>Reduces an expression to a plain field reference, if it is one.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The field-reference expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? AsFieldReference(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } member => member,
        _ => null,
    };
}
