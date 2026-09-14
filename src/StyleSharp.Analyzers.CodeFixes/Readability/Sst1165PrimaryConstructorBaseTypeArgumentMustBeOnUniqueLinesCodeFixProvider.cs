// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A code fix provider for the <see cref="Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesAnalyzer"/> analyzer.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <summary>Resolves the reported primary constructor base type and splits its arguments onto their own lines.</summary>
    internal static readonly UniqueLineFix<PrimaryConstructorBaseTypeSyntax> Fix = new(UniqueLineRewrites.PrimaryConstructorBaseTypeArguments);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => Fix.FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            CodeFixResources.SST1150CodeFixTitle,
            $"{nameof(Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesCodeFixProvider)}-Add",
            Fix.CanRewrite,
            Fix.TryRewrite);
}
