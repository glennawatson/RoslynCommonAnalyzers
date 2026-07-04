// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes the intermediate object cast from a reported round-trip cast (PSH1015), turning
/// <c>(int)(object)value</c> into <c>(int)value</c>. The analyzer already proved a direct,
/// non-user-defined conversion exists, so the rewrite compiles and keeps the same result. The
/// inner cast's operand carries over unchanged — a parenthesized operand keeps its
/// parentheses, so precedence cannot shift.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1015BoxingRoundTripCastCodeFixProvider))]
[Shared]
public sealed class Psh1015BoxingRoundTripCastCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.BoxingRoundTripCast.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Cast directly without boxing", nameof(Psh1015BoxingRoundTripCastCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported outer cast and builds its direct-cast replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is CastExpressionSyntax cast
            && Psh1015BoxingRoundTripCastAnalyzer.TryGetObjectCast(cast) is not null
            ? new NodeReplacement(cast, Rewrite(cast))
            : null;

    /// <summary>Builds the direct cast, dropping the intermediate object cast.</summary>
    /// <param name="cast">The outer cast to rewrite.</param>
    /// <returns>The outer cast applied straight to the inner operand.</returns>
    private static CastExpressionSyntax Rewrite(CastExpressionSyntax cast)
    {
        var objectCast = Psh1015BoxingRoundTripCastAnalyzer.TryGetObjectCast(cast)!;
        return cast.WithExpression(objectCast.Expression.WithoutTrivia()).WithTriviaFrom(cast);
    }
}
