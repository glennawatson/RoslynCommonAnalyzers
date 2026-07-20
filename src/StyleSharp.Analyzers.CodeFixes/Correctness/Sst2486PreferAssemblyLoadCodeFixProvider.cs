// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites <c>Assembly.LoadWithPartialName("X")</c> to <c>Assembly.Load("X")</c> (SST2486) by swapping only the
/// invoked member name and keeping the same string argument. This is the one mechanical, behaviour-preserving case:
/// <c>Assembly.Load(string)</c> exists on every target framework and takes the same argument, so the edit always
/// compiles. <c>Assembly.LoadFrom</c> and <c>Assembly.LoadFile</c> are reported without a fix, because moving off
/// them changes the load context and can alter behaviour — the author replaces those calls by hand.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2486PreferAssemblyLoadCodeFixProvider))]
[Shared]
public sealed class Sst2486PreferAssemblyLoadCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The deprecated partial-name API this fix replaces.</summary>
    private const string LoadWithPartialNameName = "LoadWithPartialName";

    /// <summary>The recommended API the call is swapped to.</summary>
    private const string LoadName = "Load";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.PreferAssemblyLoad.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Replace LoadWithPartialName with Assembly.Load",
            nameof(Sst2486PreferAssemblyLoadCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves a reported LoadWithPartialName call and renames it to Load.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The name node and its Load replacement, or <see langword="null"/> when the reported call is LoadFrom or LoadFile.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation
            || invocation.Expression is not MemberAccessExpressionSyntax
            {
                Name: IdentifierNameSyntax { Identifier.ValueText: LoadWithPartialNameName } name,
            })
        {
            return null;
        }

        var replacement = name.WithIdentifier(SyntaxFactory.Identifier(LoadName).WithTriviaFrom(name.Identifier));
        return new NodeReplacement(name, replacement);
    }
}
