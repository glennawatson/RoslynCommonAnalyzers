// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A code fix provider for the <see cref="Sst1156ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer"/> analyzer.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1156ElementAccessExpressionArgumentMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public sealed class Sst1156ElementAccessExpressionArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <summary>Resolves the reported element access expression and splits its arguments onto their own lines.</summary>
    internal static readonly UniqueLineFix<ElementAccessExpressionSyntax> Fix = new(UniqueLineRewrites.ElementAccessArguments);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(Sst1156ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => Fix.FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            CodeFixResources.SST1150CodeFixTitle,
            $"{nameof(Sst1156ElementAccessExpressionArgumentMustBeOnUniqueLinesCodeFixProvider)}-Add",
            Fix.CanRewrite,
            Fix.TryRewrite);
}
