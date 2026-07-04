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
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (TryGetRegisterName(root, diagnostic) is not { } name)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use UnsafeRegister",
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(name, Rewrite(name)))),
                    equivalenceKey: nameof(Psh1309UnsafeRegisterCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetRegisterName(editor.OriginalRoot, diagnostic) is not { } name)
        {
            return;
        }

        editor.ReplaceNode(name, Rewrite(name));
    }

    /// <summary>Returns the <c>Register</c> member name of the reported invocation when its shape still matches.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The member name node, or <see langword="null"/> when the shape no longer matches.</returns>
    private static SimpleNameSyntax? TryGetRegisterName(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && Psh1309UnsafeRegisterAnalyzer.IsRegisterShape(invocation)
            ? ((MemberAccessExpressionSyntax)invocation.Expression).Name
            : null;

    /// <summary>Builds the <c>UnsafeRegister</c> name replacement.</summary>
    /// <param name="name">The original member name node.</param>
    /// <returns>The renamed identifier carrying the original trivia.</returns>
    private static IdentifierNameSyntax Rewrite(SimpleNameSyntax name)
        => SyntaxFactory.IdentifierName(Psh1309UnsafeRegisterAnalyzer.UnsafeRegisterMethodName).WithTriviaFrom(name);
}
