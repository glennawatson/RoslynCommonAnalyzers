// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
public sealed class Sst1489ObsoleteSerializationMemberCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.ObsoleteSerializationMember.Id);

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
            if (GetMember(root, diagnostic) is not { } member)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the obsolete serialization member",
                    _ => Task.FromResult(Apply(context.Document, root, member)),
                    equivalenceKey: nameof(Sst1489ObsoleteSerializationMemberCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (GetMember(editor.OriginalRoot, diagnostic) is not { } member)
        {
            return;
        }

        editor.RemoveNode(member, SyntaxRemoveOptions.KeepUnbalancedDirectives);
    }

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
        var member = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MemberDeclarationSyntax>();
        return member is ConstructorDeclarationSyntax or MethodDeclarationSyntax ? member : null;
    }
}
