// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace StyleSharp.Analyzers;

/// <summary>
/// Repairs the two mechanical halves of the disposal pattern (SST2300): a <c>Dispose()</c> on a
/// finalizable type that never suppresses finalization, and a <c>Dispose(bool)</c> that is public.
/// </summary>
/// <remarks>
/// Only these two clauses are fixed, and deliberately so. Synthesizing the pattern itself — a
/// <c>Dispose(bool)</c> the type does not have — would mean deciding which of the type's fields are
/// managed, which are not, and which of them the finalizer path is allowed to touch. That is the design
/// the rule is asking the author to do; a code fix that guessed at it would produce a body that compiles
/// and disposes nothing.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2300DisposePatternCodeFixProvider))]
[Shared]
public sealed class Sst2300DisposePatternCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>
    /// The call the fix inserts, written from the global namespace so it binds wherever it lands. The
    /// simplifier shortens it back to <c>GC.SuppressFinalize(this)</c> in the ordinary file.
    /// </summary>
    private const string SuppressFinalizeCall = "global::System.GC.SuppressFinalize(this);";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DesignRules.DisposePattern.Id);

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
            if (FindMethod(root, diagnostic) is not { } method
                || !diagnostic.Properties.TryGetValue(Sst2300DisposePatternAnalyzer.ClauseKey, out var clause))
            {
                continue;
            }

            RegisterClauseFix(context, root, diagnostic, method, clause);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (FindMethod(editor.OriginalRoot, diagnostic) is not { } method
            || !diagnostic.Properties.TryGetValue(Sst2300DisposePatternAnalyzer.ClauseKey, out var clause))
        {
            return;
        }

        var replacement = GetReplacementModifiers(diagnostic);
        editor.ReplaceNode(method, (current, _) => Rewrite((MethodDeclarationSyntax)current, clause, replacement));
    }

    /// <summary>Applies the fix for one repairable clause.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="method">The method the clause was reported on.</param>
    /// <param name="clause">The failing clause.</param>
    /// <param name="replacement">The modifiers a public <c>Dispose(bool)</c> should declare instead.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, MethodDeclarationSyntax method, string? clause, string? replacement)
        => document.WithSyntaxRoot(root.ReplaceNode(method, Rewrite(method, clause, replacement)));

    /// <summary>Registers the code action for one repairable clause.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    /// <param name="method">The method the clause was reported on.</param>
    /// <param name="clause">The failing clause.</param>
    private static void RegisterClauseFix(
        CodeFixContext context,
        SyntaxNode root,
        Diagnostic diagnostic,
        MethodDeclarationSyntax method,
        string? clause)
    {
        var replacement = GetReplacementModifiers(diagnostic);
        var title = clause == Sst2300DisposePatternAnalyzer.SuppressFinalizeClause
            ? "Call 'GC.SuppressFinalize(this)' in 'Dispose()'"
            : $"Declare 'Dispose(bool)' as '{replacement}'";

        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                _ => Task.FromResult(Apply(context.Document, root, method, clause, replacement)),
                equivalenceKey: clause),
            diagnostic);
    }

    /// <summary>Reads the modifiers a public <c>Dispose(bool)</c> should declare instead.</summary>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    /// <returns>The replacement modifiers, which the analyzer supplies for the public-overload clause.</returns>
    private static string GetReplacementModifiers(Diagnostic diagnostic)
        => diagnostic.Properties.TryGetValue(Sst2300DisposePatternAnalyzer.ReplacementModifiersKey, out var replacement) && replacement is not null
            ? replacement
            : Sst2300DisposePatternAnalyzer.OverridableModifiers;

    /// <summary>Resolves the diagnostic's span to the method it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The method declaration, or <see langword="null"/> when the shape no longer matches.</returns>
    private static MethodDeclarationSyntax? FindMethod(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>();

    /// <summary>Rewrites the reported method for its clause.</summary>
    /// <param name="method">The method the clause was reported on.</param>
    /// <param name="clause">The failing clause.</param>
    /// <param name="replacement">The modifiers a public <c>Dispose(bool)</c> should declare instead.</param>
    /// <returns>The rewritten method.</returns>
    private static MethodDeclarationSyntax Rewrite(MethodDeclarationSyntax method, string? clause, string? replacement)
        => clause == Sst2300DisposePatternAnalyzer.SuppressFinalizeClause
            ? AddSuppressFinalize(method)
            : WithReplacementModifiers(method, replacement);

    /// <summary>Appends <c>GC.SuppressFinalize(this)</c> to a <c>Dispose()</c> body.</summary>
    /// <param name="method">The <c>Dispose()</c> declaration.</param>
    /// <returns>The rewritten method.</returns>
    /// <remarks>
    /// The call goes last: it is what the method promises once the cleanup above it has actually run. An
    /// expression-bodied <c>Dispose()</c> becomes a block, because it now has two things to say.
    /// </remarks>
    private static MethodDeclarationSyntax AddSuppressFinalize(MethodDeclarationSyntax method)
    {
        var suppress = BuildSuppressFinalizeStatement();
        if (method.Body is { } body)
        {
            return method.WithBody(body.AddStatements(suppress));
        }

        if (method.ExpressionBody is not { } expressionBody)
        {
            return method;
        }

        var existing = SyntaxFactory.ExpressionStatement(expressionBody.Expression);
        var block = SyntaxFactory.Block(existing, suppress).WithAdditionalAnnotations(Formatter.Annotation);
        return method
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithBody(block);
    }

    /// <summary>Builds the <c>GC.SuppressFinalize(this);</c> statement.</summary>
    /// <returns>The statement, annotated so it is simplified and indented into place.</returns>
    /// <remarks>
    /// The call is written fully qualified from the global namespace and left to the simplifier, which
    /// shortens it to <c>GC.SuppressFinalize(this)</c> only where that binds to the same method. A file
    /// without <c>using System;</c>, or one where something else is already called <c>GC</c>, keeps the
    /// qualified form and still compiles — the fix never writes a name it has not proved binds.
    /// </remarks>
    private static StatementSyntax BuildSuppressFinalizeStatement()
        => SyntaxFactory.ParseStatement(SuppressFinalizeCall)
            .WithAdditionalAnnotations(Simplifier.Annotation)
            .WithAdditionalAnnotations(Formatter.Annotation);

    /// <summary>Replaces a <c>Dispose(bool)</c>'s <c>public</c> with the modifiers the pattern asks for.</summary>
    /// <param name="method">The <c>Dispose(bool)</c> declaration.</param>
    /// <param name="replacement">The modifiers to declare instead.</param>
    /// <returns>The rewritten method.</returns>
    /// <remarks>
    /// A sealed type gets <c>private</c> — nothing can override it, and <c>protected</c> on a sealed type
    /// is a contradiction the compiler itself complains about. Any <c>virtual</c> already on the
    /// declaration is dropped, because the replacement carries the one that belongs there (or, on a sealed
    /// type, none at all).
    /// </remarks>
    private static MethodDeclarationSyntax WithReplacementModifiers(MethodDeclarationSyntax method, string? replacement)
    {
        var modifiers = method.Modifiers;
        var overridable = replacement != Sst2300DisposePatternAnalyzer.SealedModifiers;
        var rewritten = new List<SyntaxToken>(modifiers.Count + 1);
        for (var i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            if (modifier.IsKind(SyntaxKind.PublicKeyword))
            {
                AddReplacement(rewritten, modifier, overridable);
            }
            else if (!modifier.IsKind(SyntaxKind.VirtualKeyword))
            {
                rewritten.Add(modifier);
            }
        }

        return method.WithModifiers(SyntaxFactory.TokenList(rewritten));
    }

    /// <summary>Writes the replacement modifiers in place of the <c>public</c> token, keeping its trivia.</summary>
    /// <param name="rewritten">The modifier list being built.</param>
    /// <param name="modifier">The <c>public</c> token being replaced.</param>
    /// <param name="overridable">Whether the type can still be derived from.</param>
    private static void AddReplacement(List<SyntaxToken> rewritten, SyntaxToken modifier, bool overridable)
    {
        if (!overridable)
        {
            rewritten.Add(SyntaxFactory.Token(modifier.LeadingTrivia, SyntaxKind.PrivateKeyword, modifier.TrailingTrivia));
            return;
        }

        var space = SyntaxFactory.TriviaList(SyntaxFactory.Space);
        rewritten.Add(SyntaxFactory.Token(modifier.LeadingTrivia, SyntaxKind.ProtectedKeyword, space));
        rewritten.Add(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.VirtualKeyword, modifier.TrailingTrivia));
    }
}
