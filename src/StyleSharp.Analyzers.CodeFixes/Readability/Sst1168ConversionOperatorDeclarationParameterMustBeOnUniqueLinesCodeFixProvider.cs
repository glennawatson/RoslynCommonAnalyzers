// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A code fix provider for the <see cref="Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <summary>Resolves the reported conversion operator declaration and splits its parameters onto their own lines.</summary>
    internal static readonly UniqueLineFix<ConversionOperatorDeclarationSyntax> Fix = new(UniqueLineRewrites.ConversionOperatorParameters);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => Fix.FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            CodeFixResources.SST1150CodeFixTitle,
            $"{nameof(Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider)}-Add",
            Fix.CanRewrite,
            Fix.TryRewrite);
}
