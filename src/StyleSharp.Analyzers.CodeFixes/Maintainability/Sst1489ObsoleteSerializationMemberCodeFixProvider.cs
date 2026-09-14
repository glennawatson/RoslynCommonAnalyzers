// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a formatter-based serialization member from an exception type (SST1489) — either the
/// <c>(SerializationInfo, StreamingContext)</c> constructor or the <c>GetObjectData</c> override.
/// </summary>
/// <remarks>
/// Only the reported member is removed. A <c>[Serializable]</c> attribute on the type is left alone: it
/// is still meaningful to other serializers, and deciding whether the type should keep it is a judgement
/// the fix has no business making.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1489ObsoleteSerializationMemberCodeFixProvider))]
[Shared]
public sealed class Sst1489ObsoleteSerializationMemberCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TrySelect);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.ObsoleteSerializationMember.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Remove the obsolete serialization member",
            nameof(Sst1489ObsoleteSerializationMemberCodeFixProvider),
            GetMember,
            Apply);

    /// <summary>Applies the fix for one serialization member.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="member">The member to remove.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, MemberDeclarationSyntax member)
    {
        var updated = root.RemoveNode(member, SyntaxRemoveOptions.KeepUnbalancedDirectives);
        return updated is null ? document : document.WithSyntaxRoot(updated);
    }

    /// <summary>Resolves the diagnostic's span to the serialization member it reported.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The member, or <see langword="null"/> when the shape no longer matches.</returns>
    private static MemberDeclarationSyntax? GetMember(SyntaxNode root, Diagnostic diagnostic)
    {
        var member = DiagnosticEnclosingNode.Find<MemberDeclarationSyntax>(root, diagnostic);
        return member is ConstructorDeclarationSyntax or MethodDeclarationSyntax ? member : null;
    }

    /// <summary>Resolves the diagnostic's span to the serialization member the batch removes.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The removal, keeping unbalanced directives, or <see langword="null"/> when the shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NodeRemoval? TrySelect(SyntaxNode root, Diagnostic diagnostic) =>
        GetMember(root, diagnostic) is { } member ? new NodeRemoval(member) : null;
}
