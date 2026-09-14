// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant anonymous-type member name (SST1173), keeping the inferred name.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantAnonymousTypeMemberNameCodeFixProvider))]
[Shared]
public sealed class RedundantAnonymousTypeMemberNameCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(
        ReportedNode.Ancestor<AnonymousObjectMemberDeclaratorSyntax>,
        static (current, _) => OmitName((AnonymousObjectMemberDeclaratorSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantAnonymousTypeMemberName.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Omit the redundant member name",
            nameof(RedundantAnonymousTypeMemberNameCodeFixProvider),
            ReportedNode.Ancestor<AnonymousObjectMemberDeclaratorSyntax>,
            OmitName);

    /// <summary>Removes a member declarator's explicit name, keeping the inferred one.</summary>
    /// <param name="declarator">The reported member declarator.</param>
    /// <returns>The declarator without its name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AnonymousObjectMemberDeclaratorSyntax OmitName(AnonymousObjectMemberDeclaratorSyntax declarator) =>
        declarator.Update(null, declarator.Expression.WithTriviaFrom(declarator));
}
