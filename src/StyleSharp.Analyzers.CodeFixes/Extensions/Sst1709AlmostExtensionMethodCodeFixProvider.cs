// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Converts an almost-extension helper (SST1709) into a C# 14 <c>extension(Receiver) { … }</c> block
/// member: the first parameter becomes the block receiver, the method drops <c>static</c> and that
/// parameter, and its documentation goes with it minus the tag naming the receiver. The member joins a
/// block that already declares the same receiver when the class has one, rather than opening a second.
/// </summary>
/// <remarks>
/// The rewrite is the one <see cref="ExtensionBlockMemberCodeFixProvider"/> performs for a classic
/// <c>this</c>-parameter method; only the test for which methods qualify differs, so it is shared rather
/// than written twice.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1709AlmostExtensionMethodCodeFixProvider))]
[Shared]
public sealed class Sst1709AlmostExtensionMethodCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ExtensionRules.AlmostExtensionMethod.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Convert to an extension block member",
            nameof(Sst1709AlmostExtensionMethodCodeFixProvider),
            ExtensionBlockMemberCodeFixProvider.TryRewriteAlmostExtension);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, ExtensionBlockMemberCodeFixProvider.TryRewriteAlmostExtension);
}
