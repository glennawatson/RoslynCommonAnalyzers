// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Replaces a parameterless value-type construction with <c>default(T)</c> (SST1129).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1129DefaultValueTypeConstructorCodeFixProvider))]
[Shared]
public sealed class Sst1129DefaultValueTypeConstructorCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(ReportedNode.Find<ObjectCreationExpressionSyntax>, static (current, _) => CreateDefault((ObjectCreationExpressionSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.DefaultValueTypeConstructor.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Use 'default'",
            nameof(Sst1129DefaultValueTypeConstructorCodeFixProvider),
            ReportedNode.Find<ObjectCreationExpressionSyntax>,
            CreateDefault);

    /// <summary>Builds the <c>default(T)</c> expression that takes the construction's place and trivia.</summary>
    /// <param name="creation">The object-creation expression.</param>
    /// <returns>The default expression.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static DefaultExpressionSyntax CreateDefault(ObjectCreationExpressionSyntax creation) =>
        SyntaxFactory.DefaultExpression(
            SyntaxFactory.Token(creation.GetLeadingTrivia(), SyntaxKind.DefaultKeyword, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker)),
            SyntaxFactory.Token(SyntaxKind.OpenParenToken),
            creation.Type.WithoutTrivia(),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.CloseParenToken, creation.GetTrailingTrivia()));
}
