// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Adds the <c>sealed</c> modifier to a class nothing derives from (PSH1411).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1411SealNonDerivedTypeCodeFixProvider))]
[Shared]
public sealed class Psh1411SealNonDerivedTypeCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(
        ReportedNode.Ancestor<ClassDeclarationSyntax>,
        static (current, _) => SealedModifierRewrite.AddSealed((ClassDeclarationSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.SealNonDerivedType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(context, "Seal the type", nameof(Psh1411SealNonDerivedTypeCodeFixProvider), ReportedNode.Ancestor<ClassDeclarationSyntax>, SealedModifierRewrite.AddSealed);
}
