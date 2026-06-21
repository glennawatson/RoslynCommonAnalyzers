// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>Rewrites an explicit <c>ValueTuple&lt;...&gt;</c> type to tuple syntax (SST1141).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1141UseTupleSyntaxCodeFixProvider))]
[Shared]
public sealed class Sst1141UseTupleSyntaxCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseTupleSyntax.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => UseTupleSyntaxFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var tupleSpans = CreateTupleSpanSet(context.Diagnostics);
        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<GenericNameSyntax>() is not { } generic)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use tuple syntax",
                    cancellationToken => ReplaceAsync(context.Document, root, generic, tupleSpans, cancellationToken),
                    equivalenceKey: nameof(Sst1141UseTupleSyntaxCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<GenericNameSyntax>() is not { } generic)
        {
            return;
        }

        editor.ReplaceNode(ReplaceTarget(generic), BuildTuple(generic, null));
    }

    /// <summary>Replaces the explicit <c>ValueTuple&lt;...&gt;</c> spelling with tuple syntax.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="generic">The <c>ValueTuple&lt;...&gt;</c> generic name.</param>
    /// <returns>The updated document.</returns>
    internal static Document Replace(Document document, SyntaxNode root, GenericNameSyntax generic)
        => document.WithSyntaxRoot(root.ReplaceNode(ReplaceTarget(generic), BuildTuple(generic, null)));

    /// <summary>Replaces the explicit <c>ValueTuple&lt;...&gt;</c> spelling with tuple syntax.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="generic">The <c>ValueTuple&lt;...&gt;</c> generic name.</param>
    /// <param name="tupleSpans">The diagnostic spans to convert recursively.</param>
    /// <returns>The updated document.</returns>
    private static Document Replace(Document document, SyntaxNode root, GenericNameSyntax generic, HashSet<TextSpan> tupleSpans)
        => document.WithSyntaxRoot(root.ReplaceNode(ReplaceTarget(generic), BuildTuple(generic, tupleSpans)));

    /// <summary>Replaces an explicit value tuple, including nested tuple type arguments that bind to <see cref="ValueTuple"/>.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="generic">The <c>ValueTuple&lt;...&gt;</c> generic name.</param>
    /// <param name="tupleSpans">The diagnostic spans supplied by the host for this request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> ReplaceAsync(
        Document document,
        SyntaxNode root,
        GenericNameSyntax generic,
        HashSet<TextSpan> tupleSpans,
        CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            return Replace(document, root, generic);
        }

        AddNestedTupleSpans(model, generic, tupleSpans, cancellationToken);
        return Replace(document, root, generic, tupleSpans);
    }

    /// <summary>Adds nested value-tuple type arguments that semantic binding confirms are tuple types.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="generic">The outer value-tuple generic.</param>
    /// <param name="tupleSpans">The set receiving nested tuple spans.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    private static void AddNestedTupleSpans(SemanticModel model, GenericNameSyntax generic, HashSet<TextSpan> tupleSpans, CancellationToken cancellationToken)
    {
        tupleSpans.Add(generic.Span);
        foreach (var node in generic.TypeArgumentList.DescendantNodes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node is GenericNameSyntax nested
                && nested.Identifier.ValueText == "ValueTuple"
                && model.GetSymbolInfo(nested, cancellationToken).Symbol is INamedTypeSymbol { IsTupleType: true })
            {
                tupleSpans.Add(nested.Span);
            }
        }
    }

    /// <summary>Returns the node to replace — the qualified name when the generic is its right side.</summary>
    /// <param name="generic">The <c>ValueTuple&lt;...&gt;</c> generic name.</param>
    /// <returns>The outermost type node representing the value tuple.</returns>
    private static SyntaxNode ReplaceTarget(GenericNameSyntax generic)
        => generic.Parent is QualifiedNameSyntax qualified && qualified.Right == generic ? qualified : generic;

    /// <summary>Builds the <c>(T1, T2, ...)</c> tuple type from the value tuple's type arguments.</summary>
    /// <param name="generic">The <c>ValueTuple&lt;...&gt;</c> generic name.</param>
    /// <param name="tupleSpans">The diagnostic spans to convert recursively, or <see langword="null"/> for only the supplied generic.</param>
    /// <returns>The equivalent tuple type, carrying the replaced node's trivia.</returns>
    private static TupleTypeSyntax BuildTuple(GenericNameSyntax generic, HashSet<TextSpan>? tupleSpans)
        => (TupleTypeSyntax)SyntaxFactory.ParseTypeName(BuildTupleText(generic, tupleSpans)).WithTriviaFrom(ReplaceTarget(generic));

    /// <summary>Builds the tuple type text for one explicit value tuple.</summary>
    /// <param name="generic">The <c>ValueTuple&lt;...&gt;</c> generic name.</param>
    /// <param name="tupleSpans">The diagnostic spans to convert recursively, or <see langword="null"/> for only the supplied generic.</param>
    /// <returns>The equivalent tuple type text.</returns>
    private static string BuildTupleText(GenericNameSyntax generic, HashSet<TextSpan>? tupleSpans)
    {
        var arguments = generic.TypeArgumentList.Arguments;
        var builder = new StringBuilder("(");
        for (var i = 0; i < arguments.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            AppendTypeArgument(builder, arguments[i], tupleSpans);
        }

        builder.Append(')');
        return builder.ToString();
    }

    /// <summary>Appends a type argument, recursively converting nested diagnostics in Fix All.</summary>
    /// <param name="builder">The builder receiving the type text.</param>
    /// <param name="type">The type argument to append.</param>
    /// <param name="tupleSpans">The diagnostic spans to convert recursively, or <see langword="null"/> to keep nested types unchanged.</param>
    private static void AppendTypeArgument(StringBuilder builder, TypeSyntax type, HashSet<TextSpan>? tupleSpans)
    {
        if (tupleSpans is not null && TryGetNestedTupleGeneric(type, tupleSpans, out var nested))
        {
            builder.Append(BuildTupleText(nested, tupleSpans));
            return;
        }

        builder.Append(type.WithoutTrivia());
    }

    /// <summary>Gets a nested value-tuple generic only when its span was reported by the analyzer.</summary>
    /// <param name="type">The candidate type argument.</param>
    /// <param name="tupleSpans">The reported tuple diagnostic spans.</param>
    /// <param name="generic">The nested generic name.</param>
    /// <returns><see langword="true"/> when the type argument is a reported value-tuple generic.</returns>
    private static bool TryGetNestedTupleGeneric(TypeSyntax type, HashSet<TextSpan> tupleSpans, out GenericNameSyntax generic)
    {
        generic = type switch
        {
            GenericNameSyntax direct => direct,
            QualifiedNameSyntax { Right: GenericNameSyntax right } => right,
            _ => null!
        };

        return generic is not null && tupleSpans.Contains(generic.Span);
    }

    /// <summary>Creates a set of tuple diagnostic spans supplied by the host for one code-fix request.</summary>
    /// <param name="diagnostics">The diagnostics in the request.</param>
    /// <returns>The tuple diagnostic source spans.</returns>
    private static HashSet<TextSpan> CreateTupleSpanSet(ImmutableArray<Diagnostic> diagnostics)
    {
        var tupleSpans = new HashSet<TextSpan>();
        foreach (var diagnostic in diagnostics)
        {
            tupleSpans.Add(diagnostic.Location.SourceSpan);
        }

        return tupleSpans;
    }

    /// <summary>Fixes all explicit value-tuple diagnostics without asking <see cref="SyntaxEditor"/> to compose overlapping nodes.</summary>
    private sealed class UseTupleSyntaxFixAllProvider : DocumentBasedFixAllProvider
    {
        /// <summary>The shared provider instance.</summary>
        public static readonly UseTupleSyntaxFixAllProvider Instance = new();

        /// <inheritdoc/>
        protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
        {
            if (diagnostics.IsEmpty)
            {
                return document;
            }

            var root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            var tupleSpans = CreateTupleSpanSet(diagnostics);

            var replacements = new List<(SyntaxNode Target, TupleTypeSyntax Replacement)>();
            foreach (var diagnostic in diagnostics)
            {
                if (TryCreateReplacement(root, diagnostic, tupleSpans, out var target, out var replacement))
                {
                    replacements.Add((target, replacement));
                }
            }

            if (replacements.Count == 0)
            {
                return document;
            }

            var selected = SelectTopLevelTargets(replacements);
            var targets = new SyntaxNode[selected.Count];
            var replacementBySpan = new Dictionary<TextSpan, SyntaxNode>(selected.Count);
            for (var i = 0; i < selected.Count; i++)
            {
                targets[i] = selected[i].Target;
                replacementBySpan[selected[i].Target.Span] = selected[i].Replacement;
            }

            var updated = root.ReplaceNodes(targets, (original, _) => replacementBySpan[original.Span]);
            return document.WithSyntaxRoot(updated);
        }

        /// <summary>Creates the syntax replacement for one diagnostic.</summary>
        /// <param name="root">The syntax root.</param>
        /// <param name="diagnostic">The diagnostic to fix.</param>
        /// <param name="tupleSpans">The diagnostic spans to convert recursively, or <see langword="null"/> for only the supplied diagnostic.</param>
        /// <param name="target">The node to replace.</param>
        /// <param name="replacement">The tuple syntax replacement.</param>
        /// <returns><see langword="true"/> when a replacement could be created.</returns>
        private static bool TryCreateReplacement(
            SyntaxNode root,
            Diagnostic diagnostic,
            HashSet<TextSpan>? tupleSpans,
            out SyntaxNode target,
            out TupleTypeSyntax replacement)
        {
            target = null!;
            replacement = null!;
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<GenericNameSyntax>() is not { } generic)
            {
                return false;
            }

            target = ReplaceTarget(generic);
            replacement = BuildTuple(generic, tupleSpans);
            return true;
        }

        /// <summary>Selects only the outermost replacements so nested diagnostics are handled by the outer tuple rewrite.</summary>
        /// <param name="replacements">The candidate replacements.</param>
        /// <returns>The top-level replacements.</returns>
        private static List<(SyntaxNode Target, TupleTypeSyntax Replacement)> SelectTopLevelTargets(List<(SyntaxNode Target, TupleTypeSyntax Replacement)> replacements)
        {
            replacements.Sort(static (left, right) =>
            {
                var start = left.Target.SpanStart.CompareTo(right.Target.SpanStart);
                return start != 0 ? start : right.Target.Span.Length.CompareTo(left.Target.Span.Length);
            });

            var selected = new List<(SyntaxNode Target, TupleTypeSyntax Replacement)>();
            foreach (var candidate in replacements)
            {
                var contained = false;
                for (var i = 0; i < selected.Count; i++)
                {
                    if (selected[i].Target.Span.Contains(candidate.Target.Span))
                    {
                        contained = true;
                        break;
                    }
                }

                if (!contained)
                {
                    selected.Add(candidate);
                }
            }

            return selected;
        }
    }
}
