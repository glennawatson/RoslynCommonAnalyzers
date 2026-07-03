// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported <c>StringBuilder.Append</c> call so the builder does the formatting
/// work itself (PSH1203): <c>Append(string.Format(args...))</c> becomes
/// <c>AppendFormat(args...)</c> with the argument list carried over verbatim,
/// <c>Append(x.ToString())</c> drops the <c>ToString()</c> call, and
/// <c>Append(s.Substring(i[, n]))</c> becomes <c>Append(s, i, n)</c>, computing
/// <c>s.Length - i</c> when no count was given.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1203StringBuilderInnerAllocationCodeFixProvider))]
[Shared]
public sealed class Psh1203StringBuilderInnerAllocationCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The argument count of the <c>Substring(startIndex)</c> shape.</summary>
    private const int SubstringStartOnlyArgumentCount = 1;

    /// <summary>The argument count of the <c>Substring(startIndex, length)</c> shape.</summary>
    private const int SubstringStartAndLengthArgumentCount = 2;

    /// <summary>The syntactic shape of the call nested inside the Append argument.</summary>
    private enum InnerCallShape
    {
        /// <summary>The argument is not a rewritable inner call.</summary>
        None,

        /// <summary>The argument is a <c>string.Format(...)</c> call.</summary>
        Format,

        /// <summary>The argument is a parameterless <c>x.ToString()</c> call.</summary>
        ToString,

        /// <summary>The argument is an <c>s.Substring(...)</c> call on a simple receiver.</summary>
        Substring,
    }

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.StringBuilderInnerAllocation.Id);

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
            if (!TryGetInvocation(root, diagnostic, out var invocation))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Let StringBuilder do the formatting work",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, invocation!)),
                    equivalenceKey: nameof(Psh1203StringBuilderInnerAllocationCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetInvocation(editor.OriginalRoot, diagnostic, out var invocation))
        {
            return;
        }

        editor.ReplaceNode(invocation!, Rewrite(invocation!));
    }

    /// <summary>Replaces the reported Append invocation with its direct-formatting form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The reported Append invocation.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        => document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)));

    /// <summary>Finds the reported Append invocation for a diagnostic and re-validates its shape.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="invocation">The reported invocation when found.</param>
    /// <returns><see langword="true"/> when the invocation was found and is still rewritable.</returns>
    private static bool TryGetInvocation(SyntaxNode root, Diagnostic diagnostic, out InvocationExpressionSyntax? invocation)
    {
        invocation = null;
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (node is not IdentifierNameSyntax { Parent: MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax candidate } }
            || Classify(candidate, out _, out _) == InnerCallShape.None)
        {
            return false;
        }

        invocation = candidate;
        return true;
    }

    /// <summary>Classifies the call nested inside the Append argument.</summary>
    /// <param name="invocation">The outer Append invocation.</param>
    /// <param name="inner">The inner call passed as the Append argument.</param>
    /// <param name="innerAccess">The inner call's member access.</param>
    /// <returns>The syntactic shape of the inner call, or <see cref="InnerCallShape.None"/>.</returns>
    private static InnerCallShape Classify(
        InvocationExpressionSyntax invocation,
        out InvocationExpressionSyntax? inner,
        out MemberAccessExpressionSyntax? innerAccess)
    {
        inner = null;
        innerAccess = null;

        if (invocation.ArgumentList.Arguments.Count != 1
            || invocation.ArgumentList.Arguments[0].Expression is not InvocationExpressionSyntax innerCandidate
            || innerCandidate.Expression is not MemberAccessExpressionSyntax accessCandidate
            || !accessCandidate.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || accessCandidate.Name is not IdentifierNameSyntax innerName)
        {
            return InnerCallShape.None;
        }

        inner = innerCandidate;
        innerAccess = accessCandidate;
        return ClassifyInnerName(innerName.Identifier.ValueText, innerCandidate.ArgumentList.Arguments.Count, accessCandidate);
    }

    /// <summary>Maps the inner call's member name and argument count to its rewritable shape.</summary>
    /// <param name="innerName">The inner call's member name.</param>
    /// <param name="innerArgumentCount">The inner call's argument count.</param>
    /// <param name="innerAccess">The inner call's member access.</param>
    /// <returns>The syntactic shape of the inner call, or <see cref="InnerCallShape.None"/>.</returns>
    private static InnerCallShape ClassifyInnerName(string innerName, int innerArgumentCount, MemberAccessExpressionSyntax innerAccess)
        => innerName switch
        {
            "Format" => InnerCallShape.Format,
            "ToString" when innerArgumentCount == 0 => InnerCallShape.ToString,
            "Substring" when innerArgumentCount is SubstringStartOnlyArgumentCount or SubstringStartAndLengthArgumentCount
                && Psh1203StringBuilderInnerAllocationAnalyzer.IsSimpleReceiver(innerAccess.Expression) => InnerCallShape.Substring,
            _ => InnerCallShape.None,
        };

    /// <summary>Builds the direct-formatting invocation that replaces the reported one.</summary>
    /// <param name="invocation">The reported Append invocation.</param>
    /// <returns>The replacement invocation.</returns>
    private static InvocationExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
        => Classify(invocation, out var inner, out var innerAccess) switch
        {
            InnerCallShape.Format => RewriteFormat(invocation, inner!),
            InnerCallShape.ToString => RewriteToString(invocation, inner!, innerAccess!),
            InnerCallShape.Substring => RewriteSubstring(invocation, inner!, innerAccess!),
            _ => invocation,
        };

    /// <summary>Rewrites <c>Append(string.Format(args...))</c> to <c>AppendFormat(args...)</c>.</summary>
    /// <param name="invocation">The reported Append invocation.</param>
    /// <param name="inner">The <c>string.Format</c> call.</param>
    /// <returns>The replacement invocation.</returns>
    private static InvocationExpressionSyntax RewriteFormat(InvocationExpressionSyntax invocation, InvocationExpressionSyntax inner)
    {
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        return invocation
            .WithExpression(access.WithName(SyntaxFactory.IdentifierName("AppendFormat").WithTriviaFrom(access.Name)))
            .WithArgumentList(inner.ArgumentList.WithTriviaFrom(invocation.ArgumentList));
    }

    /// <summary>Rewrites <c>Append(x.ToString())</c> to <c>Append(x)</c>.</summary>
    /// <param name="invocation">The reported Append invocation.</param>
    /// <param name="inner">The <c>ToString</c> call.</param>
    /// <param name="innerAccess">The <c>ToString</c> call's member access.</param>
    /// <returns>The replacement invocation.</returns>
    private static InvocationExpressionSyntax RewriteToString(
        InvocationExpressionSyntax invocation,
        InvocationExpressionSyntax inner,
        MemberAccessExpressionSyntax innerAccess)
        => invocation.ReplaceNode(inner, innerAccess.Expression.WithTriviaFrom(inner));

    /// <summary>Rewrites <c>Append(s.Substring(i[, n]))</c> to <c>Append(s, i, n)</c>.</summary>
    /// <param name="invocation">The reported Append invocation.</param>
    /// <param name="inner">The <c>Substring</c> call.</param>
    /// <param name="innerAccess">The <c>Substring</c> call's member access.</param>
    /// <returns>The replacement invocation.</returns>
    private static InvocationExpressionSyntax RewriteSubstring(
        InvocationExpressionSyntax invocation,
        InvocationExpressionSyntax inner,
        MemberAccessExpressionSyntax innerAccess)
    {
        var receiver = innerAccess.Expression.WithoutTrivia();
        var start = inner.ArgumentList.Arguments[0].Expression.WithoutTrivia();
        var count = inner.ArgumentList.Arguments.Count == SubstringStartAndLengthArgumentCount
            ? inner.ArgumentList.Arguments[1].Expression.WithoutTrivia()
            : RemainingLength(receiver, start);

        var arguments = SyntaxFactory.SeparatedList<ArgumentSyntax>(new SyntaxNodeOrToken[]
        {
            SyntaxFactory.Argument(receiver),
            SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Argument(start),
            SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Argument(count),
        });

        return invocation.WithArgumentList(SyntaxFactory.ArgumentList(arguments).WithTriviaFrom(invocation.ArgumentList));
    }

    /// <summary>Builds the <c>receiver.Length - start</c> count expression for the single-argument Substring form.</summary>
    /// <param name="receiver">The Substring receiver, stripped of trivia.</param>
    /// <param name="start">The start index expression, stripped of trivia.</param>
    /// <returns>The count expression.</returns>
    private static BinaryExpressionSyntax RemainingLength(ExpressionSyntax receiver, ExpressionSyntax start)
        => SyntaxFactory.BinaryExpression(
            SyntaxKind.SubtractExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver,
                SyntaxFactory.IdentifierName("Length")),
            SyntaxFactory.Token(SyntaxKind.MinusToken).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space),
            ParenthesizeIfNeeded(start));

    /// <summary>Parenthesizes a start expression that would not bind as a subtraction operand.</summary>
    /// <param name="start">The start index expression.</param>
    /// <returns>The expression, wrapped in parentheses unless it is already primary.</returns>
    private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax start)
        => start switch
        {
            IdentifierNameSyntax
                or LiteralExpressionSyntax
                or MemberAccessExpressionSyntax
                or InvocationExpressionSyntax
                or ElementAccessExpressionSyntax
                or ParenthesizedExpressionSyntax => start,
            _ => SyntaxFactory.ParenthesizedExpression(start),
        };
}
