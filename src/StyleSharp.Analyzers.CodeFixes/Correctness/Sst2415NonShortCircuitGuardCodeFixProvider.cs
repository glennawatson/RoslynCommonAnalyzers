// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Short-circuits a boolean <c>&amp;</c> / <c>|</c> whose right operand does work (SST2415), so the left
/// operand actually guards it. The fix title states plainly that this changes behaviour — the right operand
/// stops running when the left decides the result.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2415NonShortCircuitGuardCodeFixProvider))]
[Shared]
public sealed class Sst2415NonShortCircuitGuardCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.NonShortCircuitGuard.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => ShortCircuitOperatorRewrite.FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            static binary => $"Short-circuit with '{(binary.IsKind(SyntaxKind.BitwiseAndExpression) ? "&&" : "||")}' — the right operand will no longer run when the left decides",
            nameof(Sst2415NonShortCircuitGuardCodeFixProvider),
            ShortCircuitOperatorRewrite.Find,
            ShortCircuitOperatorRewrite.Apply);
}
