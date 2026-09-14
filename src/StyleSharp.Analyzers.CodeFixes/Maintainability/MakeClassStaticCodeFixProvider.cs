// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Adds the <c>static</c> modifier to a class whose members are all static (SST1432).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeClassStaticCodeFixProvider))]
[Shared]
public sealed class MakeClassStaticCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(
        DiagnosticEnclosingNode.Find<ClassDeclarationSyntax>,
        static (current, _) => MakeStatic((ClassDeclarationSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.MakeClassStatic.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Mark the class 'static'",
            nameof(MakeClassStaticCodeFixProvider),
            DiagnosticEnclosingNode.Find<ClassDeclarationSyntax>,
            MakeStatic);

    /// <summary>Builds the class declaration with <c>static</c> inserted after the access modifiers.</summary>
    /// <param name="declaration">The class declaration to mark static.</param>
    /// <returns>The rewritten declaration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ClassDeclarationSyntax MakeStatic(ClassDeclarationSyntax declaration) =>
        ClassModifierInsertion.InsertBeforePartial(declaration, SyntaxKind.StaticKeyword, takePartialIndentation: false);
}
