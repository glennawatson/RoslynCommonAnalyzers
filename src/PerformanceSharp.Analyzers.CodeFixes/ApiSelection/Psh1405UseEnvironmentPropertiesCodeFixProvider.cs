// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a reported process/thread call chain with the direct Environment property (PSH1405):
/// <c>Process.GetCurrentProcess().Id</c> becomes <c>System.Environment.ProcessId</c>,
/// <c>Process.GetCurrentProcess().MainModule.FileName</c> becomes
/// <c>System.Environment.ProcessPath</c>, and <c>Thread.CurrentThread.ManagedThreadId</c> becomes
/// <c>System.Environment.CurrentManagedThreadId</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1405UseEnvironmentPropertiesCodeFixProvider))]
[Shared]
public sealed class Psh1405UseEnvironmentPropertiesCodeFixProvider : CodeFixProvider
{
    /// <summary>The namespace qualifier used by the replacement expression.</summary>
    private const string SystemNamespaceName = "System";

    /// <summary>The environment type name used by the replacement expression.</summary>
    private const string EnvironmentTypeName = "Environment";

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.UseEnvironmentProperties.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            static _ => nameof(Psh1405UseEnvironmentPropertiesCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported chain and builds its direct Environment property replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetChain(root, diagnostic) is { } access
            && Psh1405UseEnvironmentPropertiesAnalyzer.TryGetReplacementPropertyName(access, out var propertyName)
            ? new NodeReplacement(access, CreateReplacement(propertyName).WithTriviaFrom(access))
            : null;

    /// <summary>Returns the reported chain when the diagnostic location covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported chain, or <see langword="null"/> when the location is not a member access.</returns>
    private static MemberAccessExpressionSyntax? TryGetChain(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) as MemberAccessExpressionSyntax;

    /// <summary>Words the action with the environment property that replaces the reported chain.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when the chain no longer maps to a property.</returns>
    private static string? TryCreateTitle(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetChain(root, diagnostic) is { } access && Psh1405UseEnvironmentPropertiesAnalyzer.TryGetReplacementPropertyName(access, out var propertyName)
            ? $"Use System.Environment.{propertyName}"
            : null;

    /// <summary>Builds the fully qualified <c>System.Environment</c> property replacement.</summary>
    /// <param name="propertyName">The Environment property name.</param>
    /// <returns>The replacement expression.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MemberAccessExpressionSyntax CreateReplacement(string propertyName) =>
        SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(SystemNamespaceName),
                SyntaxFactory.IdentifierName(EnvironmentTypeName)),
            SyntaxFactory.IdentifierName(propertyName));
}
