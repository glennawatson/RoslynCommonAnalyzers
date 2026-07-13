// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported array-based stream call to the memory-based overload (PSH1314):
/// <c>stream.ReadAsync(buffer, offset, count)</c> becomes
/// <c>stream.ReadAsync(buffer.AsMemory(offset, count))</c>, and a trailing cancellation token is
/// carried over. The rewritten call is speculatively bound and required to resolve to an overload
/// whose first parameter really is a memory, so the fix is never offered where
/// <c>AsMemory</c> is out of scope or the overload does not exist.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider))]
[Shared]
public sealed class Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The metadata name of the memory type the reading overload takes.</summary>
    private const string MemoryMetadataName = "System.Memory`1";

    /// <summary>The metadata name of the memory type the writing overload takes.</summary>
    private const string ReadOnlyMemoryMetadataName = "System.ReadOnlyMemory`1";

    /// <summary>The index of the trailing cancellation-token argument in the array overload.</summary>
    private const int TokenArgumentIndex = 3;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.UseMemoryBasedStreamOverloads.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use the Memory overload", nameof(Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces a reported array-based stream call with its memory-based form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The stream call to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SemanticModel model, InvocationExpressionSyntax invocation)
        => TryGetReplacement(model, invocation, out var replacement)
            ? document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement!))
            : document;

    /// <summary>Resolves the reported stream call and builds its memory-based replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && TryGetReplacement(model, invocation, out var replacement)
            ? new NodeReplacement(invocation, replacement!)
            : null;

    /// <summary>Builds the memory-based replacement for a reported stream call, and proves it binds.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The stream call to rewrite.</param>
    /// <param name="replacement">The replacement invocation when one could be built and bound.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetReplacement(SemanticModel model, InvocationExpressionSyntax invocation, out InvocationExpressionSyntax? replacement)
    {
        replacement = null;
        if (!Psh1314UseMemoryBasedStreamOverloadsAnalyzer.IsArrayOverloadShape(invocation))
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var asMemory = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                arguments[0].Expression.WithoutTrivia(),
                SyntaxFactory.IdentifierName(Psh1314UseMemoryBasedStreamOverloadsAnalyzer.AsMemoryMethodName)),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
            [
                arguments[1].WithoutTrivia(),
                arguments[2].WithoutTrivia(),
            ])));

        var replacementArguments = arguments.Count > TokenArgumentIndex
            ? new[] { SyntaxFactory.Argument(asMemory), arguments[TokenArgumentIndex].WithoutTrivia() }
            : [SyntaxFactory.Argument(asMemory)];

        var candidate = invocation
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(replacementArguments)))
            .WithTriviaFrom(invocation);

        if (!BindsToMemoryOverload(model, invocation.SpanStart, candidate))
        {
            return false;
        }

        replacement = candidate.WithAdditionalAnnotations(Formatter.Annotation);
        return true;
    }

    /// <summary>Speculatively binds the rewritten call and confirms it takes a memory first.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The original call's position, used as the speculative binding context.</param>
    /// <param name="candidate">The rewritten invocation.</param>
    /// <returns><see langword="true"/> when the replacement binds to a memory-based overload.</returns>
    private static bool BindsToMemoryOverload(SemanticModel model, int position, InvocationExpressionSyntax candidate)
    {
        if (model.GetSpeculativeSymbolInfo(position, candidate, SpeculativeBindingOption.BindAsExpression).Symbol
            is not IMethodSymbol { Parameters.Length: > 0 } bound)
        {
            return false;
        }

        var definition = bound.Parameters[0].Type.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(definition, model.Compilation.GetTypeByMetadataName(MemoryMetadataName))
            || SymbolEqualityComparer.Default.Equals(definition, model.Compilation.GetTypeByMetadataName(ReadOnlyMemoryMetadataName));
    }
}
