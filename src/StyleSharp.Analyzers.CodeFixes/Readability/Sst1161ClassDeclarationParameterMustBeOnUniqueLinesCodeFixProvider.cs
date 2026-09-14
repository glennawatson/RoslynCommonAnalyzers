// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A code fix provider for the <see cref="Sst1161ClassDeclarationParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1161ClassDeclarationParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1161ClassDeclarationParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <summary>Resolves the reported class declaration and splits its parameters onto their own lines.</summary>
    internal static readonly UniqueLineFix<ClassDeclarationSyntax> Fix = new(UniqueLineRewrites.ClassParameters);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1161ClassDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => Fix.FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            CodeFixResources.SST1150CodeFixTitle,
            $"{nameof(Sst1161ClassDeclarationParameterMustBeOnUniqueLinesCodeFixProvider)}-Add",
            Fix.CanRewrite,
            Fix.TryRewrite);
}
