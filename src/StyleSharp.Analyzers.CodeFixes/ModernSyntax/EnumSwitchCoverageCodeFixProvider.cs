// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Adds explicit enum cases or arms for SST2205, SST2206, and SST2242.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EnumSwitchCoverageCodeFixProvider))]
[Shared]
public sealed class EnumSwitchCoverageCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.CompleteEnumSwitchStatement.Id,
        ModernSyntaxRules.CompleteEnumSwitchExpression.Id,
        ModernSyntaxRules.CompleteEnumSwitchStatementMapping.Id);

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
            // A section appended before the closing brace lands inside whatever region closes there, so a
            // switch carrying a directive gets no action rather than one that cannot run.
            if (CarriesADirective(root, diagnostic))
            {
                continue;
            }

            if (diagnostic.Properties.ContainsKey(EnumSwitchCoverageAnalyzer.CatchAllProperty))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Add a catch-all section",
                        _ => Task.FromResult(AddCatchAll(context.Document, root, diagnostic)),
                        equivalenceKey: "CatchAll"),
                    diagnostic);
                continue;
            }

            if (!diagnostic.Properties.ContainsKey(EnumSwitchCoverageAnalyzer.MissingMembersProperty))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    diagnostic.Id == ModernSyntaxRules.CompleteEnumSwitchExpression.Id
                        ? "Add missing enum arms"
                        : "Add missing enum cases",
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
            "SST2206" => ApplySwitchExpression(document, root, diagnostic, encodedMembers),
            "SST2205" or "SST2242" => ApplySwitchStatement(document, root, diagnostic, encodedMembers),
            _ => document
        };
    }

    /// <summary>Returns whether the reported switch carries a directive among its cases.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns><see langword="true"/> when an appended case would land inside a region.</returns>
    private static bool CarriesADirective(SyntaxNode root, Diagnostic diagnostic)
    {
        var reported = root.FindNode(diagnostic.Location.SourceSpan);
        return reported.FirstAncestorOrSelf<SwitchStatementSyntax>() is { } switchStatement
            ? DirectiveBoundaries.Cross(switchStatement, switchStatement.Span)
            : reported.FirstAncestorOrSelf<SwitchExpressionSyntax>() is { } switchExpression
            && DirectiveBoundaries.Cross(switchExpression, switchExpression.Span);
    }

    /// <summary>Adds a <c>default</c> section that states the switch handles the rest deliberately.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    private static Document AddCatchAll(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        // The section is appended after the last one and before the closing brace, which is where the
        // directive closing a region over the tail sits — so the new section would land inside it.
        var switchStatement = FindAncestor<SwitchStatementSyntax>(root, diagnostic.Location.SourceSpan);
        if (switchStatement is null || DirectiveBoundaries.Cross(switchStatement, switchStatement.Span))
        {
            return document;
        }

        var section = SyntaxFactory.SwitchSection(
            SyntaxFactory.SingletonList<SwitchLabelSyntax>(SyntaxFactory.DefaultSwitchLabel()),
            SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.BreakStatement()));
        var updated = switchStatement.AddSections(section).WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(switchStatement, updated));
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
        if (switchStatement is null || DirectiveBoundaries.Cross(switchStatement, switchStatement.Span))
        {
            return document;
        }

        // The values stack onto one section: a section each would give the switch several bodies that do
        // the same nothing, which reads as a mistake in the mapping rather than a stub.
        var members = missingMembers.Split(EnumSwitchCoverageAnalyzer.MissingMembersSeparator);
        var labels = new SwitchLabelSyntax[members.Length];
        for (var i = 0; i < members.Length; i++)
        {
            labels[i] = SyntaxFactory.CaseSwitchLabel(SyntaxFactory.ParseExpression(members[i]));
        }

        var updated = switchStatement.AddSections(SyntaxFactory.SwitchSection(
            SyntaxFactory.List(labels),
            SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.BreakStatement())))
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
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
