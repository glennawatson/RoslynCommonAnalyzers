// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A code fix provider for the <see cref="Sst1151MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1151MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1151MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <summary>Resolves the reported method declaration and splits its parameters onto their own lines.</summary>
    internal static readonly UniqueLineFix<BaseMethodDeclarationSyntax> Fix = new(UniqueLineRewrites.MethodParameters);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1151MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => Fix.FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            CodeFixResources.SST1150CodeFixTitle,
            $"{nameof(Sst1151MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider)}-Add",
            Fix.CanRewrite,
            Fix.TryRewrite);
}
