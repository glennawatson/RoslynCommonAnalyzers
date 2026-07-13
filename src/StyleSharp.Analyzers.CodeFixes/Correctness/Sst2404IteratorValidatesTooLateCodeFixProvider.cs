// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Splits an iterator whose guards run too late (SST2404) into the two methods it should have been: the
/// declared method keeps the guards and then returns a private iterator local function, which keeps
/// everything else.
/// </summary>
/// <remarks>
/// <para>
/// The outer method stops being an iterator — its <c>yield</c> statements have all moved inside the local
/// function — so its body now runs when it is called, and the guards throw at the call site. The local
/// function needs no parameters: it closes over the method's, which is why the guards can move without any
/// of the arguments moving with them. Type parameters are in scope for the same reason, so a generic
/// iterator splits without a signature change.
/// </para>
/// <para>
/// An <c>async</c> iterator is reported but not fixed. Its cancellation token is bound to the iterator by
/// <c>[EnumeratorCancellation]</c> on the parameter, and moving the <c>async</c> body into a local function
/// would leave the attribute on a parameter of a method that is no longer the iterator — the token would
/// quietly stop being honoured. Splitting one of those correctly means deciding how the token is threaded
/// through, which is the author's call, not the fix's.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2404IteratorValidatesTooLateCodeFixProvider))]
[Shared]
public sealed class Sst2404IteratorValidatesTooLateCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The name given to the extracted iterator.</summary>
    private const string IteratorName = "Iterator";

    /// <summary>The highest suffix tried when the preferred name is taken.</summary>
    private const int MaximumNameSuffix = 64;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.IteratorValidatesTooLate.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Validate the arguments eagerly and return a private iterator",
            nameof(Sst2404IteratorValidatesTooLateCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Applies one SST2404 split for the reported iterator.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original when the reported shape no longer matches.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
        => TryRewrite(root, diagnostic) is { } edit
            ? document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))
            : document;

    /// <summary>Resolves the reported iterator and rewrites it as a validating wrapper plus an iterator.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape carries no safe fix.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { Body: { } body } method
            || ModifierListHelper.Contains(method.Modifiers, SyntaxKind.AsyncKeyword))
        {
            return null;
        }

        var guards = IteratorGuardAnalysis.CountLeadingGuards(body, method.ParameterList);
        if (guards == 0 || body.Statements.Count <= guards || !IteratorGuardAnalysis.IsIterator(body))
        {
            return null;
        }

        return new NodeReplacement(method, Split(method, body, guards));
    }

    /// <summary>Rewrites the method as guards, a return, and the iterator they were guarding.</summary>
    /// <param name="method">The reported method.</param>
    /// <param name="body">The method's body.</param>
    /// <param name="guards">The number of leading guard statements.</param>
    /// <returns>The rewritten method.</returns>
    private static MethodDeclarationSyntax Split(MethodDeclarationSyntax method, BlockSyntax body, int guards)
    {
        var name = CreateIteratorName(method);
        var lineBreak = LineEndingHelper.GetLineBreak(method);
        var statements = body.Statements;
        var rewritten = new StatementSyntax[guards + 2];
        for (var i = 0; i < guards; i++)
        {
            rewritten[i] = statements[i];
        }

        // The blank lines have to be real line breaks, in the file's own form. An elastic one is the
        // formatter's to remove, and it removes it — gluing the return to the guards above it and the local
        // function to the return.
        rewritten[guards] = SyntaxFactory.ReturnStatement(SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(name)))
            .WithLeadingTrivia(lineBreak)
            .WithTrailingTrivia(lineBreak, lineBreak);
        rewritten[guards + 1] = CreateIterator(method, statements, guards, name);

        return method
            .WithBody(body.WithStatements(SyntaxFactory.List(rewritten)))
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Builds the local function that keeps everything the guards were guarding.</summary>
    /// <param name="method">The reported method.</param>
    /// <param name="statements">The method's original statements.</param>
    /// <param name="guards">The number of leading guard statements.</param>
    /// <param name="name">The name to give the iterator.</param>
    /// <returns>The iterator local function.</returns>
    private static LocalFunctionStatementSyntax CreateIterator(
        MethodDeclarationSyntax method,
        SyntaxList<StatementSyntax> statements,
        int guards,
        string name)
    {
        var kept = new StatementSyntax[statements.Count - guards];
        for (var i = 0; i < kept.Length; i++)
        {
            kept[i] = statements[guards + i];
        }

        // The first kept statement carries the blank line that separated it from the guards; an elastic marker
        // in its place lets the formatter close that gap, so the iterator's body does not open on one. The
        // local function itself is left with no leading trivia at all: the blank line in front of it is the
        // return statement's, and one elastic trivia anywhere in that gap would hand the whole of it back to
        // the formatter, which would close it.
        kept[0] = kept[0].WithLeadingTrivia(SyntaxFactory.ElasticMarker);
        var returnType = method.ReturnType.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.ElasticSpace);
        return SyntaxFactory.LocalFunctionStatement(returnType, SyntaxFactory.Identifier(name))
            .WithParameterList(SyntaxFactory.ParameterList())
            .WithBody(SyntaxFactory.Block(SyntaxFactory.List(kept)));
    }

    /// <summary>Picks a name for the iterator that nothing in the method already uses.</summary>
    /// <param name="method">The reported method.</param>
    /// <returns>The iterator's name.</returns>
    private static string CreateIteratorName(MethodDeclarationSyntax method)
    {
        if (!IsNameUsed(method, IteratorName))
        {
            return IteratorName;
        }

        for (var suffix = 2; suffix <= MaximumNameSuffix; suffix++)
        {
            var candidate = IteratorName + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!IsNameUsed(method, candidate))
            {
                return candidate;
            }
        }

        return IteratorName + method.Identifier.ValueText;
    }

    /// <summary>Returns whether any identifier in the method already spells a name.</summary>
    /// <param name="method">The reported method.</param>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when the name is taken.</returns>
    private static bool IsNameUsed(MethodDeclarationSyntax method, string name)
    {
        var scan = new NameScan(name);
        DescendantTraversalHelper.VisitDescendantTokens(method, ref scan, VisitToken);
        return scan.Found;
    }

    /// <summary>Records whether a token spells the candidate name.</summary>
    /// <param name="token">The token being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once the name is found, which stops the walk.</returns>
    private static bool VisitToken(in SyntaxToken token, ref NameScan state)
    {
        if (!token.IsKind(SyntaxKind.IdentifierToken) || token.ValueText != state.Name)
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>The state threaded through the search for a name already in use.</summary>
    /// <param name="Name">The candidate name.</param>
    private record struct NameScan(string Name)
    {
        /// <summary>Gets or sets a value indicating whether the name is taken.</summary>
        public bool Found { get; set; }
    }
}
