// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes unused private members reported by SST1440.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1440PrivateMemberUsageCodeFixProvider))]
[Shared]
public sealed class Sst1440PrivateMemberUsageCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.RemoveUnusedPrivateMember.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Remove unused private member",
            nameof(Sst1440PrivateMemberUsageCodeFixProvider),
            static (root, diagnostic) => FindTarget(root, diagnostic) is not null,
            Apply);

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryCreateReplacement(editor.OriginalRoot, diagnostic, out var oldNode, out var replacement))
        {
            return;
        }

        if (replacement is null)
        {
            editor.RemoveNode(oldNode, SyntaxRemoveOptions.KeepUnbalancedDirectives);
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
        if (!TryCreateReplacement(root, diagnostic, out var oldNode, out var replacement))
        {
            return document;
        }

        var updated = replacement is null
            ? root.RemoveNode(oldNode, SyntaxRemoveOptions.KeepUnbalancedDirectives)
            : root.ReplaceNode(oldNode, replacement);
        return updated is null ? document : document.WithSyntaxRoot(updated);
    }

    /// <summary>Creates the member removal or narrowed field/event declaration edit.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The node to remove or replace.</param>
    /// <param name="replacement">The replacement node, or <see langword="null"/> for removal.</param>
    /// <returns><see langword="true"/> when a safe edit was found.</returns>
    private static bool TryCreateReplacement(SyntaxNode root, Diagnostic diagnostic, [NotNullWhen(true)] out SyntaxNode? oldNode, out SyntaxNode? replacement)
    {
        replacement = null;
        oldNode = FindTarget(root, diagnostic);
        switch (oldNode)
        {
            case VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: FieldDeclarationSyntax field } declaration } variable:
                {
                    oldNode = field;
                    replacement = RemoveVariable(field, declaration, variable);
                    return true;
                }

            case VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: EventFieldDeclarationSyntax eventField } declaration } variable:
                {
                    oldNode = eventField;
                    replacement = RemoveVariable(eventField, declaration, variable);
                    return true;
                }
        }

        return oldNode is not null;
    }

    /// <summary>Resolves the reported field or event variable, or the reported member, without building the edit.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The variable declarator of a field or event, the member declaration, or <see langword="null"/> when neither applies.</returns>
    private static SyntaxNode? FindTarget(SyntaxNode root, Diagnostic diagnostic)
    {
        var parent = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent;
        if (parent?.FirstAncestorOrSelf<VariableDeclaratorSyntax>() is not { } variable)
        {
            return parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        }

        return variable.Parent is VariableDeclarationSyntax { Parent: FieldDeclarationSyntax or EventFieldDeclarationSyntax } ? variable : null;
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
