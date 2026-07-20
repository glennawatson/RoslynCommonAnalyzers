// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a <c>TZConvert.GetTimeZoneInfo(id)</c> call with <c>System.TimeZoneInfo.FindSystemTimeZoneById(id)</c>
/// (PSH1419), keeping the single argument. The built-in call is written fully qualified so it compiles
/// whatever the file's usings are, and the analyzer has already confirmed the method exists in the
/// compilation.
/// </summary>
/// <remarks>
/// Only the <c>GetTimeZoneInfo</c> shape has a clean one-to-one replacement, so only it is offered a fix.
/// The <c>IanaToWindows</c> and <c>WindowsToIana</c> calls carry the same diagnostic id but return a
/// converted id through a different-shaped API (a <c>bool</c>-returning <c>TryConvert</c> with an
/// <c>out</c> parameter), so rewriting them would change the surrounding expression; they are reported for
/// a human to convert and left without a fix.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1419PreferBuiltInTimeZoneCodeFixProvider))]
[Shared]
public sealed class Psh1419PreferBuiltInTimeZoneCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The namespace qualifier used by the replacement expression.</summary>
    private const string SystemNamespaceName = "System";

    /// <summary>The built-in type used by the replacement expression.</summary>
    private const string TimeZoneInfoTypeName = "TimeZoneInfo";

    /// <summary>The built-in method used by the replacement expression.</summary>
    private const string FindSystemTimeZoneByIdMethodName = "FindSystemTimeZoneById";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.PreferBuiltInTimeZone.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use System.TimeZoneInfo.FindSystemTimeZoneById",
            nameof(Psh1419PreferBuiltInTimeZoneCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported call and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape is not a fixable <c>GetTimeZoneInfo</c> call.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is InvocationExpressionSyntax invocation
            && Psh1419PreferBuiltInTimeZoneAnalyzer.IsGetTimeZoneInfoInvocation(invocation)
            ? new NodeReplacement(invocation, Rewrite(invocation))
            : null;

    /// <summary>Builds the fully qualified <c>System.TimeZoneInfo.FindSystemTimeZoneById</c> call, keeping the argument.</summary>
    /// <param name="invocation">The reported call.</param>
    /// <returns>The replacement expression.</returns>
    private static InvocationExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
        => SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(SystemNamespaceName),
                    SyntaxFactory.IdentifierName(TimeZoneInfoTypeName)),
                SyntaxFactory.IdentifierName(FindSystemTimeZoneByIdMethodName)),
            invocation.ArgumentList.WithoutTrivia())
            .WithTriviaFrom(invocation);
}
