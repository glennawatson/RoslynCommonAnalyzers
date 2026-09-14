// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Adds the <c>sealed</c> modifier to an attribute class (PSH1401).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1401SealAttributeTypesCodeFixProvider))]
[Shared]
public sealed class Psh1401SealAttributeTypesCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(
        ReportedNode.Ancestor<ClassDeclarationSyntax>,
        static (current, _) => SealedModifierRewrite.AddSealed((ClassDeclarationSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.SealAttributeTypes.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Seal the attribute type",
            nameof(Psh1401SealAttributeTypesCodeFixProvider),
            ReportedNode.Ancestor<ClassDeclarationSyntax>,
            SealedModifierRewrite.AddSealed);
}
