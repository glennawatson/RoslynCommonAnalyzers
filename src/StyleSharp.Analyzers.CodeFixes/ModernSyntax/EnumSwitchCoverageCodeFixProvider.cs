// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Adds explicit enum cases or arms for SST2205 and SST2206.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EnumSwitchCoverageCodeFixProvider))]
[Shared]
public sealed class EnumSwitchCoverageCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.CompleteEnumSwitchStatement.Id,
        ModernSyntaxRules.CompleteEnumSwitchExpression.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.ContainsKey(EnumSwitchCoverageAnalyzer.MissingMembersProperty))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    diagnostic.Id == ModernSyntaxRules.CompleteEnumSwitchStatement.Id
                        ? "Add missing enum cases"
                        : "Add missing enum arms",
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: diagnostic.Id),
                diagnostic);
        }
    }

    /// <summary>Applies one enum switch coverage fix.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        if (!diagnostic.Properties.TryGetValue(EnumSwitchCoverageAnalyzer.MissingMembersProperty, out var missingMembers)
            || missingMembers is null
            || string.IsNullOrWhiteSpace(missingMembers))
        {
            return document;
        }

        var encodedMembers = missingMembers;
        return diagnostic.Id switch
        {
            "SST2205" => ApplySwitchStatement(document, root, diagnostic, encodedMembers),
            "SST2206" => ApplySwitchExpression(document, root, diagnostic, encodedMembers),
            _ => document
        };
    }

    /// <summary>Adds switch statement sections for missing enum values.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="missingMembers">The encoded missing members.</param>
    /// <returns>The updated document.</returns>
    private static Document ApplySwitchStatement(Document document, SyntaxNode root, Diagnostic diagnostic, string missingMembers)
    {
        var switchStatement = FindAncestor<SwitchStatementSyntax>(root, diagnostic.Location.SourceSpan);
        if (switchStatement is null)
        {
            return document;
        }

        var updated = switchStatement;
        var members = missingMembers.Split(EnumSwitchCoverageAnalyzer.MissingMembersSeparator);
        for (var i = 0; i < members.Length; i++)
        {
            var section = SyntaxFactory.SwitchSection(
                SyntaxFactory.SingletonList<SwitchLabelSyntax>(SyntaxFactory.CaseSwitchLabel(SyntaxFactory.ParseExpression(members[i]))),
                SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.BreakStatement()));
            updated = updated.AddSections(section);
        }

        updated = updated.WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(switchStatement, updated));
    }

    /// <summary>Adds switch expression arms for missing enum values.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="missingMembers">The encoded missing members.</param>
    /// <returns>The updated document.</returns>
    private static Document ApplySwitchExpression(Document document, SyntaxNode root, Diagnostic diagnostic, string missingMembers)
    {
        var switchExpression = FindAncestor<SwitchExpressionSyntax>(root, diagnostic.Location.SourceSpan);
        if (switchExpression is null)
        {
            return document;
        }

        var updated = switchExpression;
        var members = missingMembers.Split(EnumSwitchCoverageAnalyzer.MissingMembersSeparator);
        for (var i = 0; i < members.Length; i++)
        {
            var arm = SyntaxFactory.SwitchExpressionArm(
                SyntaxFactory.ConstantPattern(SyntaxFactory.ParseExpression(members[i])),
                SyntaxFactory.ParseExpression("throw new global::System.NotImplementedException()"));
            updated = updated.AddArms(arm);
        }

        updated = updated.WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(switchExpression, updated));
    }

    /// <summary>Finds the node at a span or one of its ancestors.</summary>
    /// <typeparam name="T">The ancestor node type to find.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <returns>The matching node, or <see langword="null"/>.</returns>
    private static T? FindAncestor<T>(SyntaxNode root, TextSpan span)
        where T : SyntaxNode
    {
        var node = root.FindToken(span.Start).Parent;
        while (node is not null)
        {
            if (node is T matched)
            {
                return matched;
            }

            node = node.Parent;
        }

        return null;
    }
}
