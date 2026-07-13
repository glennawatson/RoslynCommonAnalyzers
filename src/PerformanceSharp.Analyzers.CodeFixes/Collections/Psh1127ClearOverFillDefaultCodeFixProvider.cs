// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported default-valued <c>Array.Fill</c> call to <c>Array.Clear</c> (PSH1127).
/// <c>Fill(buffer, default)</c> becomes <c>Clear(buffer)</c> where the whole-array overload
/// exists (.NET 6+) and <c>Clear(buffer, 0, buffer.Length)</c> where it does not, while the
/// ranged <c>Fill(buffer, default, start, count)</c> becomes <c>Clear(buffer, start, count)</c>.
/// The rewritten call is speculatively bound before the fix is offered, so a replacement that
/// would not compile is never suggested.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1127ClearOverFillDefaultCodeFixProvider))]
[Shared]
public sealed class Psh1127ClearOverFillDefaultCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The name of the array length property used by the ranged fallback.</summary>
    private const string LengthPropertyName = "Length";

    /// <summary>The index of the start-index argument in the ranged Fill overload.</summary>
    private const int StartIndexArgumentIndex = 2;

    /// <summary>The index of the count argument in the ranged Fill overload.</summary>
    private const int CountArgumentIndex = 3;

    /// <summary>The metadata name of the array type that hosts Clear.</summary>
    private const string ArrayMetadataName = "System.Array";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.ClearOverFillDefault.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use Array.Clear", nameof(Psh1127ClearOverFillDefaultCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces a reported Fill call with its Clear form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The Fill invocation to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SemanticModel model, InvocationExpressionSyntax invocation)
        => TryGetReplacement(model, invocation, out var replacement)
            ? document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement!))
            : document;

    /// <summary>Resolves the reported Fill call and builds its Clear replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && TryGetReplacement(model, invocation, out var replacement)
            ? new NodeReplacement(invocation, replacement!)
            : null;

    /// <summary>Builds the Clear replacement for a reported Fill call, and proves it binds.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The Fill invocation to rewrite.</param>
    /// <param name="replacement">The replacement invocation when one could be built and bound.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetReplacement(SemanticModel model, InvocationExpressionSyntax invocation, out InvocationExpressionSyntax? replacement)
    {
        replacement = null;
        if (!Psh1127ClearOverFillDefaultAnalyzer.IsFillDefaultShape(invocation)
            || model.Compilation.GetTypeByMetadataName(ArrayMetadataName) is not { } arrayType)
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var clearArguments = arguments.Count == Psh1127ClearOverFillDefaultAnalyzer.RangedFillArgumentCount
            ? BuildRangedArguments(arguments)
            : BuildWholeArrayArguments(arguments[0].Expression, Psh1127ClearOverFillDefaultAnalyzer.HasWholeArrayClear(arrayType));
        if (clearArguments is null)
        {
            return false;
        }

        var candidate = invocation
            .WithExpression(RenameToClear(invocation.Expression))
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(clearArguments)))
            .WithTriviaFrom(invocation);

        if (!BindsToArrayClear(model, invocation.SpanStart, candidate, arrayType))
        {
            return false;
        }

        replacement = candidate.WithAdditionalAnnotations(Formatter.Annotation);
        return true;
    }

    /// <summary>Builds the argument list for the ranged Clear(array, startIndex, count) form.</summary>
    /// <param name="arguments">The original Fill arguments.</param>
    /// <returns>The Clear arguments.</returns>
    private static ArgumentSyntax[] BuildRangedArguments(SeparatedSyntaxList<ArgumentSyntax> arguments)
        => [arguments[0].WithoutTrivia(), arguments[StartIndexArgumentIndex].WithoutTrivia(), arguments[CountArgumentIndex].WithoutTrivia()];

    /// <summary>Builds the argument list for the whole-array Clear form.</summary>
    /// <param name="arrayExpression">The array expression.</param>
    /// <param name="hasWholeArrayClear">Whether the single-parameter Clear overload exists.</param>
    /// <returns>The Clear arguments, or <see langword="null"/> when the array cannot be evaluated twice.</returns>
    private static ArgumentSyntax[]? BuildWholeArrayArguments(ExpressionSyntax arrayExpression, bool hasWholeArrayClear)
    {
        var array = arrayExpression.WithoutTrivia();
        if (hasWholeArrayClear)
        {
            return [SyntaxFactory.Argument(array)];
        }

        if (!Psh1127ClearOverFillDefaultAnalyzer.IsRepeatableExpression(arrayExpression))
        {
            return null;
        }

        var zero = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
        var length = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            array,
            SyntaxFactory.IdentifierName(LengthPropertyName));
        return [SyntaxFactory.Argument(array), SyntaxFactory.Argument(zero), SyntaxFactory.Argument(length)];
    }

    /// <summary>Renames the invoked <c>Fill</c> name to <c>Clear</c>, preserving the receiver form.</summary>
    /// <param name="callee">The invoked expression.</param>
    /// <returns>The callee naming Clear.</returns>
    private static ExpressionSyntax RenameToClear(ExpressionSyntax callee)
    {
        var clear = SyntaxFactory.IdentifierName(Psh1127ClearOverFillDefaultAnalyzer.ClearMethodName);
        return callee is MemberAccessExpressionSyntax access
            ? access.WithName(clear)
            : clear;
    }

    /// <summary>Speculatively binds the rewritten call and confirms it resolves to <c>Array.Clear</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The original call's position, used as the speculative binding context.</param>
    /// <param name="candidate">The rewritten invocation.</param>
    /// <param name="arrayType">The <c>System.Array</c> type in the current compilation.</param>
    /// <returns><see langword="true"/> when the replacement binds to Array.Clear.</returns>
    private static bool BindsToArrayClear(SemanticModel model, int position, InvocationExpressionSyntax candidate, INamedTypeSymbol arrayType)
        => model.GetSpeculativeSymbolInfo(position, candidate, SpeculativeBindingOption.BindAsExpression).Symbol
                is IMethodSymbol { IsStatic: true, Name: Psh1127ClearOverFillDefaultAnalyzer.ClearMethodName } clear
            && SymbolEqualityComparer.Default.Equals(clear.ContainingType, arrayType);
}
