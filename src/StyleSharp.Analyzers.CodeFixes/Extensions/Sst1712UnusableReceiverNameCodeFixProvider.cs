// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Drops the receiver name from an extension block that declares only static members (SST1712),
/// leaving the <c>extension(Receiver)</c> form. Only the name goes: the receiver type and how it is
/// passed are what the block extends and are left alone.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1712UnusableReceiverNameCodeFixProvider))]
[Shared]
public sealed class Sst1712UnusableReceiverNameCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ExtensionRules.UnusableReceiverName.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Drop the receiver name",
            nameof(Sst1712UnusableReceiverNameCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites the receiver parameter without its name.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    /// <remarks>
    /// The type carries the space that separated it from the name, so it is trimmed with the name —
    /// otherwise the block is left reading <c>extension(string )</c>. The identifier is cleared to the
    /// default token rather than a missing one: a missing token is still a token, and the parser produces
    /// no identifier slot at all for the nameless form.
    /// </remarks>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ParameterSyntax>() is not { Type: { } receiverType } parameter
            || parameter.Parent?.Parent is not TypeDeclarationSyntax block
            || !ExtensionBlockHelper.IsExtensionBlock(block)
            || !Sst1712UnusableReceiverNameAnalyzer.DeclaresOnlyStaticMembers(block))
        {
            return null;
        }

        var updated = parameter
            .WithType(receiverType.WithoutTrailingTrivia())
            .WithIdentifier(default);
        return new NodeReplacement(parameter, updated);
    }
}
