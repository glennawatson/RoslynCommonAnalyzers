// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Marks a reported member <c>static</c> (PSH1414) and repairs the call sites that would otherwise
/// stop compiling. An unqualified call keeps binding once the member is static; a
/// <c>this.Foo(...)</c> call does not, so each one is rewritten to <c>Foo(...)</c> in the same
/// edit.
/// <para>
/// The fix is offered only when it can see every call site and prove all of them are safe. The
/// member's type must be declared in a single file, and every reference to the member must be
/// either unqualified or <c>this</c>-qualified — a call through some other instance
/// (<c>other.Foo()</c>) would break, so where one exists the diagnostic is left for a human.
/// </para>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1414MarkMembersStaticCodeFixProvider))]
[Shared]
public sealed class Psh1414MarkMembersStaticCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The code action title.</summary>
    private const string Title = "Make static";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.MarkMembersStatic.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (TryPlan(root, model, diagnostic) is not { } plan)
            {
                continue;
            }

            var document = context.Document;
            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    _ => Task.FromResult(document.WithSyntaxRoot(ApplyToRoot(root, plan))),
                    nameof(Psh1414MarkMembersStaticCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryPlan(editor.OriginalRoot, editor.SemanticModel, diagnostic) is not { } plan)
        {
            return;
        }

        ApplyPlan(editor, plan);
    }

    /// <summary>Applies the static rewrite to one member, for callers that already hold the root and model.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="member">The member to declare static.</param>
    /// <returns>The updated document, or the original when the rewrite is not provably safe.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SemanticModel model, MemberDeclarationSyntax member)
        => TryPlanForMember(model, member) is { } plan
            ? document.WithSyntaxRoot(ApplyToRoot(root, plan))
            : document;

    /// <summary>Rewrites the root: the member becomes static, and its this-qualified call sites lose the receiver.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="plan">The resolved edit plan.</param>
    /// <returns>The rewritten root.</returns>
    private static SyntaxNode ApplyToRoot(SyntaxNode root, StaticFixPlan plan)
    {
        var member = plan.Member;
        var qualified = plan.QualifiedReferences;

        // The member's own recursive this-calls are rewritten inside the new member, so the outer
        // ReplaceNodes never has to swap a node and one of its own descendants in the same pass.
        var inside = new List<MemberAccessExpressionSyntax>(qualified.Count);
        var replacements = new Dictionary<SyntaxNode, SyntaxNode>(qualified.Count + 1);
        for (var i = 0; i < qualified.Count; i++)
        {
            var access = qualified[i];
            if (member.Span.Contains(access.Span))
            {
                inside.Add(access);
                continue;
            }

            replacements[access] = Unqualify(access);
        }

        var rewrittenMember = inside.Count == 0
            ? member
            : member.ReplaceNodes(inside, static (original, _) => Unqualify(original));

        replacements[member] = AddStaticModifier(rewrittenMember);
        return root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
    }

    /// <summary>Resolves the edit plan for a member the caller already located.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="member">The member to declare static.</param>
    /// <returns>The edit plan, or <see langword="null"/> when the fix is not provably safe.</returns>
    private static StaticFixPlan? TryPlanForMember(SemanticModel model, MemberDeclarationSyntax member)
    {
        if (!Psh1414MarkMembersStaticAnalyzer.IsEligibleDeclaration(member)
            || member.Parent is not TypeDeclarationSyntax typeDeclaration
            || model.GetDeclaredSymbol(member) is not { } symbol
            || symbol.ContainingType.DeclaringSyntaxReferences.Length != 1)
        {
            return null;
        }

        var qualified = new List<MemberAccessExpressionSyntax>(2);
        return TryCollectReferences(model, typeDeclaration, symbol, qualified)
            ? new StaticFixPlan(member, qualified)
            : null;
    }

    /// <summary>Queues the member's static modifier and the <c>this.</c> strips onto the editor.</summary>
    /// <param name="editor">The document editor.</param>
    /// <param name="plan">The resolved edit plan.</param>
    private static void ApplyPlan(DocumentEditor editor, StaticFixPlan plan)
    {
        var qualified = plan.QualifiedReferences;
        for (var i = 0; i < qualified.Count; i++)
        {
            editor.ReplaceNode(qualified[i], static (current, _) => Unqualify((MemberAccessExpressionSyntax)current));
        }

        editor.ReplaceNode(plan.Member, static (current, _) => AddStaticModifier((MemberDeclarationSyntax)current));
    }

    /// <summary>Rewrites <c>this.Foo</c> to <c>Foo</c>, keeping the surrounding trivia.</summary>
    /// <param name="access">The this-qualified member access.</param>
    /// <returns>The unqualified name.</returns>
    private static SimpleNameSyntax Unqualify(MemberAccessExpressionSyntax access)
        => access.Name.WithTriviaFrom(access).WithAdditionalAnnotations(Formatter.Annotation);

    /// <summary>Inserts <c>static</c> after the member's accessibility modifier.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The member, declared static.</returns>
    private static MemberDeclarationSyntax AddStaticModifier(MemberDeclarationSyntax member)
    {
        var modifiers = member.Modifiers;
        var index = 0;
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.PrivateKeyword)
                || modifiers[i].IsKind(SyntaxKind.ProtectedKeyword)
                || modifiers[i].IsKind(SyntaxKind.InternalKeyword))
            {
                index = i + 1;
            }
        }

        var staticToken = SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        return member.WithModifiers(modifiers.Insert(index, staticToken)).WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>Resolves the member and every call site, and refuses the fix when one cannot be repaired.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The edit plan, or <see langword="null"/> when the fix is not provably safe.</returns>
    private static StaticFixPlan? TryPlan(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is MemberDeclarationSyntax member
            ? TryPlanForMember(model, member)
            : null;

    /// <summary>Collects the this-qualified call sites, failing when any reference has another receiver.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="typeDeclaration">The member's containing type declaration.</param>
    /// <param name="symbol">The member being made static.</param>
    /// <param name="qualified">Receives the this-qualified references that must be rewritten.</param>
    /// <returns><see langword="true"/> when every reference is unqualified or this-qualified.</returns>
    private static bool TryCollectReferences(
        SemanticModel model,
        TypeDeclarationSyntax typeDeclaration,
        ISymbol symbol,
        List<MemberAccessExpressionSyntax> qualified)
    {
        var state = new ReferenceScanState(symbol, model, qualified);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, ReferenceScanState>(typeDeclaration, ref state, VisitReference);
        return !state.Unsafe;
    }

    /// <summary>Classifies one reference to the member as unqualified, this-qualified, or unfixable.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once an unfixable reference is found.</returns>
    private static bool VisitReference(IdentifierNameSyntax identifier, ref ReferenceScanState state)
    {
        if (identifier.Identifier.ValueText != state.Symbol.Name
            || !SymbolEqualityComparer.Default.Equals(
                state.Model.GetSymbolInfo(identifier).Symbol,
                state.Symbol))
        {
            return true;
        }

        if (identifier.Parent is not MemberAccessExpressionSyntax access || access.Name != identifier)
        {
            return true;
        }

        if (access.Expression is not ThisExpressionSyntax)
        {
            state.Unsafe = true;
            return false;
        }

        state.Qualified.Add(access);
        return true;
    }

    /// <summary>The edits a single PSH1414 fix will make.</summary>
    /// <param name="Member">The member to declare static.</param>
    /// <param name="QualifiedReferences">The this-qualified call sites to unqualify.</param>
    private readonly record struct StaticFixPlan(
        MemberDeclarationSyntax Member,
        List<MemberAccessExpressionSyntax> QualifiedReferences);

    /// <summary>Tracks the call sites found while scanning the containing type.</summary>
    /// <param name="Symbol">The member being made static.</param>
    /// <param name="Model">The semantic model.</param>
    /// <param name="Qualified">The this-qualified references found so far.</param>
    private record struct ReferenceScanState(
        ISymbol Symbol,
        SemanticModel Model,
        List<MemberAccessExpressionSyntax> Qualified)
    {
        /// <summary>Gets or sets a value indicating whether a reference was found that the fix cannot repair.</summary>
        public bool Unsafe { get; set; }
    }
}
