// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces a non-short-circuiting boolean <c>&amp;</c> / <c>|</c> with <c>&amp;&amp;</c> / <c>||</c> (SST1468).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1468UseShortCircuitOperatorCodeFixProvider))]
[Shared]
public sealed class Sst1468UseShortCircuitOperatorCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.UseShortCircuitOperator.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => ShortCircuitOperatorRewrite.FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            static binary => binary.IsKind(SyntaxKind.BitwiseAndExpression) ? "Use '&&'" : "Use '||'",
            nameof(Sst1468UseShortCircuitOperatorCodeFixProvider),
            ShortCircuitOperatorRewrite.Find,
            ShortCircuitOperatorRewrite.Apply);
}
