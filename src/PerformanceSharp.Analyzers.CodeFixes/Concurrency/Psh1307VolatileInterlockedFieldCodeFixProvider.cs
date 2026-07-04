// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Wraps a reported plain field access in the matching Volatile call (PSH1307): a read
/// becomes <c>Volatile.Read(ref field)</c> and a simple assignment becomes
/// <c>Volatile.Write(ref field, value)</c>. Compound assignments and increments are reported
/// without a fix — they need an <c>Interlocked</c> read-modify-write, which changes more than
/// visibility. The Volatile type is spelled simple when the System.Threading import makes it
/// resolve, and fully qualified otherwise.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1307VolatileInterlockedFieldCodeFixProvider))]
[Shared]
public sealed class Psh1307VolatileInterlockedFieldCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The simple name of the volatile type.</summary>
    private const string VolatileTypeName = "Volatile";

    /// <summary>The read method name.</summary>
    private const string ReadMethodName = "Read";

    /// <summary>The write method name.</summary>
    private const string WriteMethodName = "Write";

    /// <summary>The namespace the simple spelling requires.</summary>
    private const string ThreadingNamespace = "System.Threading";

    /// <summary>The fully qualified spelling used when the simple name does not resolve.</summary>
    private const string QualifiedVolatileExpression = "global::System.Threading.Volatile";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.VolatileInterlockedField.Id);

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
            if (TryRewrite(root, model, diagnostic) is not { } rewrite)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    rewrite.Title,
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(rewrite.Original, rewrite.Replacement))),
                    equivalenceKey: nameof(Psh1307VolatileInterlockedFieldCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryRewrite(editor.OriginalRoot, editor.SemanticModel, diagnostic) is not { } rewrite)
        {
            return;
        }

        editor.ReplaceNode(rewrite.Original, rewrite.Replacement);
    }

    /// <summary>Resolves the reported access and builds its Volatile wrapper.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The rewrite parts, or <see langword="null"/> for unfixable access shapes.</returns>
    private static (SyntaxNode Original, SyntaxNode Replacement, string Title)? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not ExpressionSyntax usage
            || usage is not (IdentifierNameSyntax or MemberAccessExpressionSyntax))
        {
            return null;
        }

        var volatileSpelling = ResolvesVolatile(model, usage.SpanStart) ? VolatileTypeName : QualifiedVolatileExpression;
        if (usage.Parent is AssignmentExpressionSyntax assignment && assignment.Left == usage)
        {
            if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                return null;
            }

            var write = BuildVolatileCall(
                volatileSpelling,
                WriteMethodName,
                usage,
                SyntaxFactory.Argument(assignment.Right.WithoutTrivia()).WithLeadingTrivia(SyntaxFactory.Space));
            return (assignment, write.WithTriviaFrom(assignment), "Use Volatile.Write");
        }

        if (Psh1307VolatileInterlockedFieldAnalyzer.IsWriteAccess(usage))
        {
            return null;
        }

        var read = BuildVolatileCall(volatileSpelling, ReadMethodName, usage, extraArgument: null);
        return (usage, read.WithTriviaFrom(usage), "Use Volatile.Read");
    }

    /// <summary>Builds a <c>Volatile.X(ref field[, value])</c> invocation.</summary>
    /// <param name="volatileSpelling">The volatile type spelling.</param>
    /// <param name="methodName">Read or Write.</param>
    /// <param name="field">The field access to take by ref.</param>
    /// <param name="extraArgument">The value argument for writes, or <see langword="null"/>.</param>
    /// <returns>The invocation.</returns>
    private static InvocationExpressionSyntax BuildVolatileCall(
        string volatileSpelling,
        string methodName,
        ExpressionSyntax field,
        ArgumentSyntax? extraArgument)
    {
        var refArgument = SyntaxFactory.Argument(field.WithoutTrivia())
            .WithRefOrOutKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword).WithTrailingTrivia(SyntaxFactory.Space));
        var arguments = extraArgument is null
            ? SyntaxFactory.SingletonSeparatedList(refArgument)
            : SyntaxFactory.SeparatedList(ImmutableArrays.Of(refArgument, extraArgument));

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression(volatileSpelling),
                SyntaxFactory.IdentifierName(methodName)),
            SyntaxFactory.ArgumentList(arguments));
    }

    /// <summary>Returns whether the volatile type resolves by simple name at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <returns><see langword="true"/> when the simple spelling binds.</returns>
    private static bool ResolvesVolatile(SemanticModel model, int position)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: VolatileTypeName))
        {
            if (candidate is INamedTypeSymbol named && named.ContainingNamespace.ToDisplayString() == ThreadingNamespace)
            {
                return true;
            }
        }

        return false;
    }
}
