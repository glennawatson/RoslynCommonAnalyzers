// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported search-for-position-zero (PSH1221) into the prefix test it was asking for:
/// <c>text.IndexOf(v) == 0</c> becomes <c>text.StartsWith(v)</c>, and <c>!= 0</c> becomes
/// <c>!text.StartsWith(v)</c>.
/// </summary>
/// <remarks>
/// The arguments move across untouched, which is the whole point: whatever comparison the
/// <c>IndexOf</c> call named — an ordinal <c>char</c>, an explicit <see cref="StringComparison"/>, or
/// the current culture by default — the <c>StartsWith</c> call names the same one. The rewrite is
/// bound before it is offered, so the overload it needs is known to exist on the target framework.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1221UseStartsWithOverIndexOfCodeFixProvider))]
[Shared]
public sealed class Psh1221UseStartsWithOverIndexOfCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseStartsWithOverIndexOf.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Test the prefix with StartsWith",
            nameof(Psh1221UseStartsWithOverIndexOfCodeFixProvider),
            CanRewrite,
            TryRewrite);

    /// <summary>Resolves the reported comparison and builds its prefix test.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not BinaryExpressionSyntax comparison
            || Psh1221UseStartsWithOverIndexOfAnalyzer.TryGetIndexOfCall(comparison) is not { } indexOf
            || !BindsToStartsWith(model, comparison, indexOf))
        {
            return null;
        }

        var prefixTest = Psh1221UseStartsWithOverIndexOfAnalyzer.BuildPrefixTest(comparison, indexOf);
        return new NodeReplacement(comparison, prefixTest.WithTriviaFrom(comparison));
    }

    /// <summary>Confirms the renamed call still binds to a boolean <c>string.StartsWith</c>.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="comparison">The reported comparison.</param>
    /// <param name="indexOf">The <c>IndexOf</c> invocation.</param>
    /// <returns><see langword="true"/> when the rewrite compiles.</returns>
    private static bool BindsToStartsWith(SemanticModel model, BinaryExpressionSyntax comparison, InvocationExpressionSyntax indexOf)
    {
        var access = (MemberAccessExpressionSyntax)indexOf.Expression;
        var name = SyntaxFactory.IdentifierName(Psh1221UseStartsWithOverIndexOfAnalyzer.StartsWithMethodName);
        var rewritten = indexOf.Update(access.Update(access.Expression, access.OperatorToken, name), indexOf.ArgumentList);
        return model.GetSpeculativeSymbolInfo(comparison.SpanStart, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol
            is IMethodSymbol
            {
                Name: Psh1221UseStartsWithOverIndexOfAnalyzer.StartsWithMethodName,
                IsStatic: false,
                ReturnType.SpecialType: SpecialType.System_Boolean,
                ContainingType.SpecialType: SpecialType.System_String,
            };
    }

    /// <summary>Checks exact framework overloads without constructing a prefix test.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the renamed call binds to a boolean string prefix test.</returns>
    private static bool CanRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is BinaryExpressionSyntax comparison
            && Psh1221UseStartsWithOverIndexOfAnalyzer.TryGetIndexOfCall(comparison) is { } indexOf
            && (HasExactStartsWithOverload(model, indexOf) || BindsToStartsWith(model, comparison, indexOf));

    /// <summary>Proves an identity-argument call has an exact string overload after renaming.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="indexOf">The original search invocation.</param>
    /// <returns>Whether the framework overload is unambiguous without speculative binding.</returns>
    private static bool HasExactStartsWithOverload(SemanticModel model, InvocationExpressionSyntax indexOf)
    {
        if (indexOf.Expression is not MemberAccessExpressionSyntax access
            || ConditionalAccessSpeculation.ReachedThroughConditionalAccess(access.Expression)
            || model.GetSymbolInfo(indexOf).Symbol is not IMethodSymbol { IsStatic: false, ContainingType.SpecialType: SpecialType.System_String } search
            || !HasIdentityArguments(model, indexOf.ArgumentList.Arguments, search.Parameters))
        {
            return false;
        }

        // Other conversions and named arguments retain the speculative guard.
        var members = search.ContainingType.GetMembers(Psh1221UseStartsWithOverIndexOfAnalyzer.StartsWithMethodName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol
                {
                    IsStatic: false,
                    IsGenericMethod: false,
                    DeclaredAccessibility: Accessibility.Public,
                    ReturnType.SpecialType: SpecialType.System_Boolean
                } prefix
                && HasMatchingParameters(search.Parameters, prefix.Parameters))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks that every positional argument has the original parameter's exact type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="arguments">The arguments to preserve.</param>
    /// <param name="parameters">The bound search parameters.</param>
    /// <returns>Whether overload resolution can rely on identity conversions alone.</returns>
    private static bool HasIdentityArguments(SemanticModel model, SeparatedSyntaxList<ArgumentSyntax> arguments, ImmutableArray<IParameterSymbol> parameters)
    {
        if (arguments.Count != parameters.Length)
        {
            return false;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is not null
                || arguments[i].RefKindKeyword.RawKind != 0
                || !SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(arguments[i].Expression).Type, parameters[i].Type))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks that a prefix overload accepts the same argument types by value.</summary>
    /// <param name="search">The search parameters.</param>
    /// <param name="prefix">The prefix-test parameters.</param>
    /// <returns>Whether the prefix overload preserves the identity conversions.</returns>
    private static bool HasMatchingParameters(ImmutableArray<IParameterSymbol> search, ImmutableArray<IParameterSymbol> prefix)
    {
        if (prefix.Length != search.Length)
        {
            return false;
        }

        for (var i = 0; i < search.Length; i++)
        {
            if (prefix[i].RefKind != RefKind.None
                || !SymbolEqualityComparer.Default.Equals(prefix[i].Type, search[i].Type))
            {
                return false;
            }
        }

        return true;
    }
}
