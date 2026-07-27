// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Replaces a local anonymous type with the tuple carrying the same named members (PSH1023).</summary>
/// <remarks>
/// Only the creation expression changes. A tuple exposes its elements by the same member syntax, so every
/// <c>value.Member</c> read the analyzer allowed keeps working untouched — which is exactly why the rule
/// reports only locals whose uses are member reads.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1023PreferTupleOverAnonymousTypeCodeFixProvider))]
[Shared]
public sealed class Psh1023PreferTupleOverAnonymousTypeCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.PreferTupleOverAnonymousType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use a tuple",
            nameof(Psh1023PreferTupleOverAnonymousTypeCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported anonymous type and builds the equivalent tuple.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not AnonymousObjectCreationExpressionSyntax creation)
        {
            return null;
        }

        var initializers = creation.Initializers;
        var arguments = new ArgumentSyntax[initializers.Count];
        for (var i = 0; i < initializers.Count; i++)
        {
            if (BuildArgument(initializers[i]) is not { } argument)
            {
                return null;
            }

            arguments[i] = argument;
        }

        var tuple = SyntaxFactory.TupleExpression(SyntaxFactory.SeparatedList(arguments));
        return new NodeReplacement(creation, tuple.WithTriviaFrom(creation));
    }

    /// <summary>Builds one tuple element from an anonymous type's member declarator.</summary>
    /// <param name="declarator">The anonymous type member.</param>
    /// <returns>The tuple element, or <see langword="null"/> when the member has no usable name.</returns>
    private static ArgumentSyntax? BuildArgument(AnonymousObjectMemberDeclaratorSyntax declarator)
    {
        var name = declarator.NameEquals?.Name.Identifier.ValueText ?? InferName(declarator.Expression);
        return name is null
            ? null
            : SyntaxFactory.Argument(
                SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(name)),
                default,
                declarator.Expression.WithoutLeadingTrivia());
    }

    /// <summary>Reads the name an unnamed anonymous member would have taken from its expression.</summary>
    /// <param name="expression">The member's expression.</param>
    /// <returns>The inferred name, or <see langword="null"/> when the expression names nothing.</returns>
    private static string? InferName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        _ => null,
    };
}
