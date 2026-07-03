// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
public sealed class Psh1405UseEnvironmentPropertiesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The namespace qualifier used by the replacement expression.</summary>
    private const string SystemNamespaceName = "System";

    /// <summary>The environment type name used by the replacement expression.</summary>
    private const string EnvironmentTypeName = "Environment";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.UseEnvironmentProperties.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (TryGetChain(root, diagnostic) is not { } access
                || !Psh1405UseEnvironmentPropertiesAnalyzer.TryGetReplacementPropertyName(access, out var propertyName))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use System.Environment.{propertyName}",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, access)),
                    equivalenceKey: nameof(Psh1405UseEnvironmentPropertiesCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetChain(editor.OriginalRoot, diagnostic) is not { } access
            || !Psh1405UseEnvironmentPropertiesAnalyzer.TryGetReplacementPropertyName(access, out var propertyName))
        {
            return;
        }

        editor.ReplaceNode(access, CreateReplacement(propertyName).WithTriviaFrom(access));
    }

    /// <summary>Replaces the reported chain with its direct Environment property form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="access">The reported chain to rewrite.</param>
    /// <returns>The updated document, unchanged when the shape no longer matches.</returns>
    internal static Document Apply(Document document, SyntaxNode root, MemberAccessExpressionSyntax access)
        => Psh1405UseEnvironmentPropertiesAnalyzer.TryGetReplacementPropertyName(access, out var propertyName)
            ? document.WithSyntaxRoot(root.ReplaceNode(access, CreateReplacement(propertyName).WithTriviaFrom(access)))
            : document;

    /// <summary>Returns the reported chain when the diagnostic location covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported chain, or <see langword="null"/> when the location is not a member access.</returns>
    private static MemberAccessExpressionSyntax? TryGetChain(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) as MemberAccessExpressionSyntax;

    /// <summary>Builds the fully qualified <c>System.Environment</c> property replacement.</summary>
    /// <param name="propertyName">The Environment property name.</param>
    /// <returns>The replacement expression.</returns>
    private static MemberAccessExpressionSyntax CreateReplacement(string propertyName)
        => SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(SystemNamespaceName),
                SyntaxFactory.IdentifierName(EnvironmentTypeName)),
            SyntaxFactory.IdentifierName(propertyName));
}
