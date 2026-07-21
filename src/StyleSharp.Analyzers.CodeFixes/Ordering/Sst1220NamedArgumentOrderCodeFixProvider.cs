// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace StyleSharp.Analyzers;

/// <summary>Reorders an all-named argument list to match the parameter declaration order (SST1220).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1220NamedArgumentOrderCodeFixProvider))]
[Shared]
public sealed class Sst1220NamedArgumentOrderCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(OrderingRules.NamedArgumentOrder.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Order the named arguments by declaration",
            nameof(Sst1220NamedArgumentOrderCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported argument list and reorders it to declaration order.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ArgumentListSyntax>() is not { Parent: { } call } argumentList
            || argumentList.Arguments.Count < 2
            || model.GetSymbolInfo(call).Symbol is not IMethodSymbol method)
        {
            return null;
        }

        var reordered = Reorder(argumentList, method);
        return reordered is null ? null : new NodeReplacement(argumentList, reordered);
    }

    /// <summary>Rebuilds the argument list in parameter-declaration order, keeping separators and slot trivia.</summary>
    /// <param name="argumentList">The argument list.</param>
    /// <param name="method">The bound method whose parameter order the arguments should follow.</param>
    /// <returns>The reordered argument list, or <see langword="null"/> when an argument does not bind by name.</returns>
    private static ArgumentListSyntax? Reorder(ArgumentListSyntax argumentList, IMethodSymbol method)
    {
        var arguments = argumentList.Arguments;
        var count = arguments.Count;
        var positions = new int[count];
        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            if (arguments[i].NameColon is not { Name.Identifier.ValueText: var name })
            {
                return null;
            }

            var position = Sst1220NamedArgumentOrderAnalyzer.ParameterPosition(method, name);
            if (position < 0)
            {
                return null;
            }

            positions[i] = position;
            order[i] = i;
        }

        Array.Sort(order, (left, right) => positions[left] - positions[right]);

        var rebuilt = new ArgumentSyntax[count];
        for (var slot = 0; slot < count; slot++)
        {
            var moved = arguments[order[slot]];
            rebuilt[slot] = moved
                .WithLeadingTrivia(arguments[slot].GetLeadingTrivia())
                .WithTrailingTrivia(arguments[slot].GetTrailingTrivia());
        }

        return argumentList.WithArguments(SyntaxFactory.SeparatedList(rebuilt, arguments.GetSeparators()));
    }
}
