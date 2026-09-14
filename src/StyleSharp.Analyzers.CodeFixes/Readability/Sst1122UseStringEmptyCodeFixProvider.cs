// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Replaces an empty string literal with <c>string.Empty</c> (SST1122).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1122UseStringEmptyCodeFixProvider))]
[Shared]
public sealed class Sst1122UseStringEmptyCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(ReportedNode.Find<LiteralExpressionSyntax>, static (current, _) => CreateStringEmpty((LiteralExpressionSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseStringEmpty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Use string.Empty",
            nameof(Sst1122UseStringEmptyCodeFixProvider),
            ReportedNode.Find<LiteralExpressionSyntax>,
            CreateStringEmpty);

    /// <summary>Builds the <c>string.Empty</c> access that takes the literal's place and trivia.</summary>
    /// <param name="literal">The empty string literal.</param>
    /// <returns>The member access.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MemberAccessExpressionSyntax CreateStringEmpty(LiteralExpressionSyntax literal) =>
        SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(literal.GetLeadingTrivia(), SyntaxKind.StringKeyword, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker))),
            SyntaxFactory.Token(SyntaxKind.DotToken),
            SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), "Empty", literal.GetTrailingTrivia())));
}
