// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Converts an almost-extension helper (SST1709) into a C# 14 <c>extension(Receiver) { … }</c> block
/// member: the first parameter becomes the block receiver, the method drops <c>static</c> and that
/// parameter, and everything else moves inside the block. The rewritten member is re-parsed and rejected
/// if it does not form a valid extension block, so a fix that would not compile is never offered.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1709AlmostExtensionMethodCodeFixProvider))]
[Shared]
public sealed class Sst1709AlmostExtensionMethodCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>Parses the synthesized block with the C# 14 extension syntax available.</summary>
    private static readonly Microsoft.CodeAnalysis.CSharp.CSharpParseOptions ExtensionParseOptions =
        new(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ExtensionRules.AlmostExtensionMethod.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Convert to an extension block member",
            nameof(Sst1709AlmostExtensionMethodCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported method and rewrites it as an extension block.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } method
            || !Sst1709AlmostExtensionMethodAnalyzer.IsAlmostExtensionMethod(method))
        {
            return null;
        }

        var block = BuildExtensionBlock(method);
        return block is null ? null : new NodeReplacement(method, block);
    }

    /// <summary>Builds the extension block that replaces the almost-extension method.</summary>
    /// <param name="method">The almost-extension method.</param>
    /// <returns>The extension block member, or <see langword="null"/> when it does not parse cleanly.</returns>
    private static MemberDeclarationSyntax? BuildExtensionBlock(MethodDeclarationSyntax method)
    {
        var firstParameter = method.ParameterList.Parameters[0];
        if (firstParameter.Type is not { } receiverType)
        {
            return null;
        }

        var inner = method
            .WithModifiers(WithoutStatic(method.Modifiers))
            .WithParameterList(method.ParameterList.WithParameters(method.ParameterList.Parameters.RemoveAt(0)))
            .WithLeadingTrivia(SyntaxFactory.TriviaList())
            .WithTrailingTrivia(SyntaxFactory.TriviaList());

        var blockText =
            "extension(" + receiverType + " " + firstParameter.Identifier.ValueText + ")\n{\n"
            + inner.NormalizeWhitespace().ToFullString()
            + "\n}";

        if (SyntaxFactory.ParseMemberDeclaration(blockText, options: ExtensionParseOptions) is not { } parsed
            || parsed.ContainsDiagnostics
            || !ExtensionBlockHelper.IsExtensionBlock(parsed))
        {
            return null;
        }

        return parsed
            .WithLeadingTrivia(method.GetLeadingTrivia())
            .WithTrailingTrivia(method.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>Returns the modifier list with any <c>static</c> keyword removed.</summary>
    /// <param name="modifiers">The method's modifiers.</param>
    /// <returns>The modifiers without <c>static</c>.</returns>
    private static SyntaxTokenList WithoutStatic(SyntaxTokenList modifiers)
    {
        var kept = new List<SyntaxToken>(modifiers.Count);
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (!modifiers[i].IsKind(SyntaxKind.StaticKeyword))
            {
                kept.Add(modifiers[i]);
            }
        }

        return SyntaxFactory.TokenList(kept);
    }
}
