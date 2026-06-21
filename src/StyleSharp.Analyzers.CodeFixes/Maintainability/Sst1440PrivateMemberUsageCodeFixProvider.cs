// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes unused private members reported by SST1440.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1440PrivateMemberUsageCodeFixProvider))]
[Shared]
public sealed class Sst1440PrivateMemberUsageCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.RemoveUnusedPrivateMember.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            if (!TryCreateReplacement(root, diagnostic, out _, out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove unused private member",
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: nameof(Sst1440PrivateMemberUsageCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryCreateReplacement(editor.OriginalRoot, diagnostic, out var oldNode, out var replacement) || oldNode is null)
        {
            return;
        }

        if (replacement is null)
        {
            editor.RemoveNode(oldNode, SyntaxRemoveOptions.KeepNoTrivia);
            return;
        }

        editor.ReplaceNode(oldNode, replacement);
    }

    /// <summary>Applies one unused-member fix.</summary>
    /// <param name="document">The document.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        if (!TryCreateReplacement(root, diagnostic, out var oldNode, out var replacement) || oldNode is null)
        {
            return document;
        }

        SyntaxNode? updated = replacement is null
            ? root.RemoveNode(oldNode, SyntaxRemoveOptions.KeepNoTrivia)
            : root.ReplaceNode(oldNode, replacement);
        return updated is null ? document : document.WithSyntaxRoot(updated);
    }

    /// <summary>Creates the member removal or narrowed field/event declaration edit.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The node to remove or replace.</param>
    /// <param name="replacement">The replacement node, or <see langword="null"/> for removal.</param>
    /// <returns><see langword="true"/> when a safe edit was found.</returns>
    private static bool TryCreateReplacement(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode, out SyntaxNode? replacement)
    {
        oldNode = null;
        replacement = null;
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        if (token.Parent?.FirstAncestorOrSelf<VariableDeclaratorSyntax>() is { } variable)
        {
            return TryRemoveVariable(variable, out oldNode, out replacement);
        }

        if (token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member)
        {
            return false;
        }

        oldNode = member;
        return true;
    }

    /// <summary>Creates a narrowed field or event declaration when a combined declaration still has other variables.</summary>
    /// <param name="variable">The variable declarator to remove.</param>
    /// <param name="oldNode">The node to remove or replace.</param>
    /// <param name="replacement">The replacement node, or <see langword="null"/> for removal.</param>
    /// <returns><see langword="true"/> when a safe edit was found.</returns>
    private static bool TryRemoveVariable(VariableDeclaratorSyntax variable, out SyntaxNode? oldNode, out SyntaxNode? replacement)
    {
        oldNode = null;
        replacement = null;
        if (variable.Parent is not VariableDeclarationSyntax declaration)
        {
            return false;
        }

        switch (declaration.Parent)
        {
            case FieldDeclarationSyntax field:
                {
                    oldNode = field;
                    replacement = RemoveVariable(field, declaration, variable);
                    return true;
                }

            case EventFieldDeclarationSyntax eventField:
                {
                    oldNode = eventField;
                    replacement = RemoveVariable(eventField, declaration, variable);
                    return true;
                }
        }

        return false;
    }

    /// <summary>Removes one variable from a field declaration or returns <see langword="null"/> when the declaration should be removed.</summary>
    /// <param name="field">The field declaration.</param>
    /// <param name="declaration">The variable declaration.</param>
    /// <param name="variable">The variable to remove.</param>
    /// <returns>The replacement field declaration, or <see langword="null"/>.</returns>
    private static FieldDeclarationSyntax? RemoveVariable(
        FieldDeclarationSyntax field,
        VariableDeclarationSyntax declaration,
        VariableDeclaratorSyntax variable)
    {
        var variables = RemoveVariable(declaration.Variables, variable);
        return variables.Count == 0 ? null : field.WithDeclaration(declaration.WithVariables(variables));
    }

    /// <summary>Removes one variable from an event-field declaration or returns <see langword="null"/> when the declaration should be removed.</summary>
    /// <param name="eventField">The event-field declaration.</param>
    /// <param name="declaration">The variable declaration.</param>
    /// <param name="variable">The variable to remove.</param>
    /// <returns>The replacement event-field declaration, or <see langword="null"/>.</returns>
    private static EventFieldDeclarationSyntax? RemoveVariable(
        EventFieldDeclarationSyntax eventField,
        VariableDeclarationSyntax declaration,
        VariableDeclaratorSyntax variable)
    {
        var variables = RemoveVariable(declaration.Variables, variable);
        return variables.Count == 0 ? null : eventField.WithDeclaration(declaration.WithVariables(variables));
    }

    /// <summary>Removes one variable declarator from a separated list.</summary>
    /// <param name="variables">The original variables.</param>
    /// <param name="variable">The variable to remove.</param>
    /// <returns>The updated variable list.</returns>
    private static SeparatedSyntaxList<VariableDeclaratorSyntax> RemoveVariable(
        SeparatedSyntaxList<VariableDeclaratorSyntax> variables,
        VariableDeclaratorSyntax variable)
    {
        var kept = new List<VariableDeclaratorSyntax>(variables.Count);
        for (var i = 0; i < variables.Count; i++)
        {
            if (variables[i] != variable)
            {
                kept.Add(variables[i].WithoutTrivia());
            }
        }

        return SyntaxFactory.SeparatedList(kept);
    }
}
