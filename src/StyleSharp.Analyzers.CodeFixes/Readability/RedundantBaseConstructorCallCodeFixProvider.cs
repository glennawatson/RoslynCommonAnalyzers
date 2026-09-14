// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant parameterless <c>: base()</c> constructor initializer (SST1178).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantBaseConstructorCallCodeFixProvider))]
[Shared]
public sealed class RedundantBaseConstructorCallCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindConstructor, static (current, _) => RemoveInitializer((ConstructorDeclarationSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantBaseConstructorCall.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Remove the redundant ': base()' call",
            nameof(RedundantBaseConstructorCallCodeFixProvider),
            FindConstructor,
            RemoveInitializer);

    /// <summary>Drops a constructor's initializer, leaving the parameter list to flow into the body.</summary>
    /// <param name="constructor">The constructor whose initializer is redundant.</param>
    /// <returns>The constructor without its initializer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConstructorDeclarationSyntax RemoveInitializer(ConstructorDeclarationSyntax constructor) =>
        constructor.WithInitializer(null);

    /// <summary>Finds the constructor that owns the reported initializer.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The constructor, or <see langword="null"/> when the reported node is not its initializer.</returns>
    private static ConstructorDeclarationSyntax? FindConstructor(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is ConstructorInitializerSyntax { Parent: ConstructorDeclarationSyntax constructor }
            ? constructor
            : null;
}
