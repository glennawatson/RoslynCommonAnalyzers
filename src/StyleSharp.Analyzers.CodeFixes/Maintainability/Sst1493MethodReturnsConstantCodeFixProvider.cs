// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.FindSymbols;

namespace StyleSharp.Analyzers;

/// <summary>
/// Turns a method whose body is a single constant into a get-only property (SST1493), and drops the empty
/// argument list from every call site so the value is read where it used to be computed.
/// </summary>
/// <remarks>
/// <para>
/// The fix is only offered when the whole change can be made correctly. Every reference to the method must be
/// a plain call whose value is used: a method group handed to a delegate, a <c>nameof</c>, and a call written
/// as a statement of its own all survive the method but not the property, so a method used any of those ways
/// is left for a human. So is a method with an overload, whose name a property would collide with.
/// </para>
/// <para>
/// Whether the value then belongs as a <c>const</c> is a question the property does not close off, and this
/// fix does not guess at: a property keeps the member's accessibility, its place in the type, and the shape
/// of every call site, none of which a field would.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1493MethodReturnsConstantCodeFixProvider))]
[Shared]
public sealed class Sst1493MethodReturnsConstantCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.MethodReturnsConstant.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } method)
            {
                continue;
            }

            // The call sites are resolved before the fix is offered, so a method no property could replace
            // produces no light bulb at all rather than a rewrite that does not compile.
            var callSites = await TryGetRewritableCallSitesAsync(context.Document, method, context.CancellationToken).ConfigureAwait(false);
            if (callSites is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Expose '{method.Identifier.ValueText}' as a get-only property",
                    cancellationToken => ConvertAsync(context.Document, method, cancellationToken),
                    equivalenceKey: nameof(Sst1493MethodReturnsConstantCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Converts the method to a get-only property and rewrites every call site.</summary>
    /// <param name="document">The document that declares the method.</param>
    /// <param name="method">The method whose body is a constant.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated solution, unchanged when the method can no longer be converted.</returns>
    internal static async Task<Solution> ConvertAsync(Document document, MethodDeclarationSyntax method, CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;
        var callSites = await TryGetRewritableCallSitesAsync(document, method, cancellationToken).ConfigureAwait(false);
        if (callSites is null)
        {
            return solution;
        }

        for (var i = 0; i < callSites.Count; i++)
        {
            var callSite = callSites[i];
            var targets = new List<SyntaxNode>(callSite.Invocations.Count + 1);
            for (var j = 0; j < callSite.Invocations.Count; j++)
            {
                targets.Add(callSite.Invocations[j]);
            }

            if (callSite.Id == document.Id)
            {
                targets.Add(method);
            }

            solution = solution.WithDocumentSyntaxRoot(callSite.Id, callSite.Root.ReplaceNodes(targets, Replace));
        }

        return solution;
    }

    /// <summary>Builds the node that takes one rewrite target's place.</summary>
    /// <param name="original">The node as it was found.</param>
    /// <param name="rewritten">The node with any nested rewrites already applied.</param>
    /// <returns>The property, or the call site with its empty argument list dropped.</returns>
    private static SyntaxNode Replace(SyntaxNode original, SyntaxNode rewritten)
    {
        if (rewritten is MethodDeclarationSyntax method)
        {
            return CreateProperty(method);
        }

        var invocation = (InvocationExpressionSyntax)rewritten;
        return invocation.Expression.WithTriviaFrom(invocation);
    }

    /// <summary>Builds the get-only property that replaces the method.</summary>
    /// <param name="method">The method being converted.</param>
    /// <returns>The property declaration.</returns>
    /// <remarks>
    /// A block body becomes an expression body, because the constant it returned is the whole property. The
    /// method's modifiers, return type, name and documentation carry over untouched.
    /// </remarks>
    private static PropertyDeclarationSyntax CreateProperty(MethodDeclarationSyntax method)
    {
        var value = Sst1493MethodReturnsConstantAnalyzer.TryGetConstantBody(method);
        var body = method.ExpressionBody
            ?? SyntaxFactory.ArrowExpressionClause(
                SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken).WithTrailingTrivia(SyntaxFactory.Space),
                value!.WithoutTrivia());

        return SyntaxFactory.PropertyDeclaration(
                default,
                method.Modifiers,
                method.ReturnType,
                explicitInterfaceSpecifier: null,
                method.Identifier.WithTrailingTrivia(SyntaxFactory.Space),
                accessorList: null,
                body,
                initializer: null,
                SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(method.GetLeadingTrivia())
            .WithTrailingTrivia(method.GetTrailingTrivia())
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Finds every call site of the method, provided all of them can become property reads.</summary>
    /// <param name="document">The document that declares the method.</param>
    /// <param name="method">The method whose body is a constant.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The call sites by document, or <see langword="null"/> when the method cannot become a property.</returns>
    private static async Task<List<DocumentCallSites>?> TryGetRewritableCallSitesAsync(
        Document document,
        MethodDeclarationSyntax method,
        CancellationToken cancellationToken)
    {
        if (Sst1493MethodReturnsConstantAnalyzer.TryGetConstantBody(method) is null)
        {
            return null;
        }

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        // An overload would collide with the property's name, so the whole type must know the name once.
        if (model?.GetDeclaredSymbol(method, cancellationToken) is not { ContainingType: { } containingType } symbol
            || containingType.GetMembers(symbol.Name).Length > 1)
        {
            return null;
        }

        var byDocument = new List<DocumentCallSites>(1);
        if (await GetOrAddAsync(byDocument, document, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        var references = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution, cancellationToken).ConfigureAwait(false);
        return await TryAddCallSitesAsync(byDocument, references, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Resolves every reference to the call it makes, and gives up as soon as one is not a call.</summary>
    /// <param name="byDocument">The call sites gathered so far.</param>
    /// <param name="references">The references the symbol finder returned.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The call sites by document, or <see langword="null"/> when a reference cannot be rewritten.</returns>
    private static async Task<List<DocumentCallSites>?> TryAddCallSitesAsync(
        List<DocumentCallSites> byDocument,
        IEnumerable<ReferencedSymbol> references,
        CancellationToken cancellationToken)
    {
        foreach (var referenced in references)
        {
            foreach (var reference in referenced.Locations)
            {
                if (reference.IsImplicit
                    || await GetOrAddAsync(byDocument, reference.Document, cancellationToken).ConfigureAwait(false) is not { } sites
                    || TryGetCallSite(sites.Root, reference.Location.SourceSpan) is not { } invocation)
                {
                    return null;
                }

                sites.Invocations.Add(invocation);
            }
        }

        return byDocument;
    }

    /// <summary>Gets the rewrite entry for one document, adding it with its root the first time it is seen.</summary>
    /// <param name="byDocument">The call sites gathered so far.</param>
    /// <param name="document">The document to look up.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entry, or <see langword="null"/> when the document has no syntax root.</returns>
    private static async Task<DocumentCallSites?> GetOrAddAsync(List<DocumentCallSites> byDocument, Document document, CancellationToken cancellationToken)
    {
        for (var i = 0; i < byDocument.Count; i++)
        {
            if (byDocument[i].Id == document.Id)
            {
                return byDocument[i];
            }
        }

        if (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) is not { } root)
        {
            return null;
        }

        var sites = new DocumentCallSites(document.Id, root);
        byDocument.Add(sites);
        return sites;
    }

    /// <summary>Resolves one reference to the call it makes, when that call can become a property read.</summary>
    /// <param name="root">The referencing document's syntax root.</param>
    /// <param name="span">The reference's source span.</param>
    /// <returns>The invocation, or <see langword="null"/> when the reference is not a plain call.</returns>
    /// <remarks>
    /// A property cannot be handed to a delegate, named by <c>nameof</c>, or written as a statement of its own —
    /// <c>Limit;</c> is not a statement. Each of those references leaves the method exactly as it is.
    /// </remarks>
    private static InvocationExpressionSyntax? TryGetCallSite(SyntaxNode root, TextSpan span)
    {
        var name = root.FindNode(span, getInnermostNodeForTie: true);
        var called = name.Parent switch
        {
            MemberAccessExpressionSyntax access when access.Name == name => access,
            MemberBindingExpressionSyntax binding when binding.Name == name => (ExpressionSyntax)binding,
            _ => name as ExpressionSyntax,
        };

        return called?.Parent is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } invocation
            && invocation.Expression == called
            && invocation.Parent is not ExpressionStatementSyntax
                ? invocation
                : null;
    }

    /// <summary>The call sites to rewrite in one document.</summary>
    private sealed class DocumentCallSites
    {
        /// <summary>Initializes a new instance of the <see cref="DocumentCallSites"/> class.</summary>
        /// <param name="id">The document's id.</param>
        /// <param name="root">The document's syntax root.</param>
        public DocumentCallSites(DocumentId id, SyntaxNode root)
        {
            Id = id;
            Root = root;
            Invocations = new List<InvocationExpressionSyntax>(1);
        }

        /// <summary>Gets the document's id.</summary>
        public DocumentId Id { get; }

        /// <summary>Gets the document's syntax root.</summary>
        public SyntaxNode Root { get; }

        /// <summary>Gets the calls to rewrite in the document.</summary>
        public List<InvocationExpressionSyntax> Invocations { get; }
    }
}
