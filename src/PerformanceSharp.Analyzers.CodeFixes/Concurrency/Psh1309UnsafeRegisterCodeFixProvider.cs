// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Renames a reported <c>Register</c> call to <c>UnsafeRegister</c> (PSH1309). The analyzer
/// only reports the two-argument callback-and-state overloads, which have UnsafeRegister
/// twins with identical signatures, so the arguments carry over unchanged.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1309UnsafeRegisterCodeFixProvider))]
[Shared]
public sealed class Psh1309UnsafeRegisterCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.UseUnsafeRegister.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use UnsafeRegister", nameof(Psh1309UnsafeRegisterCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported invocation's <c>Register</c> name and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && Psh1309UnsafeRegisterAnalyzer.IsRegisterShape(invocation)
            && ((MemberAccessExpressionSyntax)invocation.Expression).Name is { } name
            ? new NodeReplacement(name, Rewrite(name))
            : null;

    /// <summary>Builds the <c>UnsafeRegister</c> name replacement.</summary>
    /// <param name="name">The original member name node.</param>
    /// <returns>The renamed identifier carrying the original trivia.</returns>
    private static IdentifierNameSyntax Rewrite(SimpleNameSyntax name)
        => SyntaxFactory.IdentifierName(Psh1309UnsafeRegisterAnalyzer.UnsafeRegisterMethodName).WithTriviaFrom(name);
}
