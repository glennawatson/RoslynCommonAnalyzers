// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Drops the <c>ToCharArray()</c> from a local initialized by <c>string.ToCharArray()</c> and only
/// iterated (PSH1226), retyping the local so it holds the string itself. A <c>char[]</c> declaration
/// becomes <c>string</c>; a <c>var</c> declaration is left to re-infer the string. Every iteration
/// use — <c>foreach</c>, <c>Length</c>, an index read — compiles unchanged on the string.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1226IterateStringWithoutCopyCodeFixProvider))]
[Shared]
public sealed class Psh1226IterateStringWithoutCopyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The contextual keyword marking an implicitly typed local.</summary>
    private const string VarKeyword = "var";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.IterateStringWithoutCopy.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Iterate the string directly", nameof(Psh1226IterateStringWithoutCopyCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported copy and builds the retyped declaration that drops it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is InvocationExpressionSyntax invocation
            && Psh1226IterateStringWithoutCopyAnalyzer.TryGetRetypeableLocal(invocation, out var declarator, out var localDeclaration)
            ? new NodeReplacement(localDeclaration, Rewrite(invocation, declarator, localDeclaration))
            : null;

    /// <summary>Rebuilds the local declaration without the copy and with a string type.</summary>
    /// <param name="invocation">The reported <c>ToCharArray</c> invocation.</param>
    /// <param name="declarator">The variable declarator the copy initializes.</param>
    /// <param name="localDeclaration">The local declaration statement.</param>
    /// <returns>The rewritten local declaration.</returns>
    private static LocalDeclarationStatementSyntax Rewrite(
        InvocationExpressionSyntax invocation,
        VariableDeclaratorSyntax declarator,
        LocalDeclarationStatementSyntax localDeclaration)
    {
        var receiver = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
        var newValue = receiver.WithTriviaFrom(invocation);
        var newDeclarator = declarator.WithInitializer(declarator.Initializer!.WithValue(newValue));

        var declaration = (VariableDeclarationSyntax)declarator.Parent!;
        var newDeclaration = declaration
            .WithType(RetypeToString(declaration.Type))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(newDeclarator));

        return localDeclaration.WithDeclaration(newDeclaration);
    }

    /// <summary>Retypes a <c>char[]</c> declaration to <c>string</c>, leaving a <c>var</c> declaration to re-infer it.</summary>
    /// <param name="type">The declared type syntax.</param>
    /// <returns>The declared type the string initializer needs.</returns>
    private static TypeSyntax RetypeToString(TypeSyntax type)
        => type is IdentifierNameSyntax { Identifier.ValueText: VarKeyword }
            ? type
            : SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)).WithTriviaFrom(type);
}
