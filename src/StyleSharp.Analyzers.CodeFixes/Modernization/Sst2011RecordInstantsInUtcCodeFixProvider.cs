// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a recorded local-clock read to the UTC clock (SST2011): <c>DateTime.Now</c> becomes
/// <c>DateTime.UtcNow</c>, <c>DateTimeOffset.Now</c> becomes <c>DateTimeOffset.UtcNow</c>,
/// <c>DateTime.Today</c> becomes <c>DateTime.UtcNow.Date</c>, and <c>DateTimeOffset.Now.DateTime</c> becomes
/// <c>DateTimeOffset.UtcNow.UtcDateTime</c>.
/// </summary>
/// <remarks>
/// <para>
/// The fix changes the recorded value — that is what the rule is asking for — but not the type of the
/// expression, so nothing downstream stops compiling. Both halves of that are proved rather than assumed:
/// the rewritten access is bound speculatively and its type compared with the type of the read it replaces,
/// so a <c>Now</c> on some other type is never rewritten into a <c>UtcNow</c> that does not exist, and a
/// rewrite that would hand back a different type is never offered.
/// </para>
/// <para>
/// <c>DateTimeOffset.Now.DateTime</c> becomes <c>UtcDateTime</c>, not <c>DateTime</c>. The two agree on the
/// ticks once the clock is <c>UtcNow</c> — its offset is zero — but they disagree on the
/// <c>DateTimeKind</c> they carry: <c>DateTime</c> hands back <c>Unspecified</c>, which is the very
/// ambiguity the rule exists to remove, and <c>UtcDateTime</c> hands back <c>Utc</c>, so every later
/// conversion knows what it is holding.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2011RecordInstantsInUtcCodeFixProvider))]
[Shared]
public sealed class Sst2011RecordInstantsInUtcCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.RecordInstantsInUtc.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryBuildReplacement(root, model, diagnostic, out var access, out var replacement))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Read the UTC clock",
                    _ => Task.FromResult(Apply(context.Document, root, access!, replacement!)),
                    equivalenceKey: nameof(Sst2011RecordInstantsInUtcCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryBuildReplacement(editor.OriginalRoot, editor.SemanticModel, diagnostic, out var access, out var replacement))
        {
            return;
        }

        editor.ReplaceNode(access!, replacement!);
    }

    /// <summary>Rewrites one reported clock read to the UTC clock.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="access">The reported member access.</param>
    /// <param name="replacement">The UTC member access built for the reported read.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(
        Document document,
        SyntaxNode root,
        MemberAccessExpressionSyntax access,
        MemberAccessExpressionSyntax replacement)
        => document.WithSyntaxRoot(root.ReplaceNode(access, replacement));

    /// <summary>Resolves the reported clock read and builds its UTC replacement, if the rewrite binds.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="access">The reported member access, when the shape still matches.</param>
    /// <param name="replacement">The UTC member access, when it binds.</param>
    /// <returns><see langword="true"/> when the fix can be offered.</returns>
    internal static bool TryBuildReplacement(
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic,
        out MemberAccessExpressionSyntax? access,
        out MemberAccessExpressionSyntax? replacement)
    {
        replacement = null;
        access = root.FindNode(diagnostic.Location.SourceSpan) as MemberAccessExpressionSyntax;
        if (access is null)
        {
            return false;
        }

        var shape = ClockPropertyAccess.MatchLocalInstantSpelling(access);
        if (shape == ClockPropertyAccess.LocalInstant.None)
        {
            return false;
        }

        var candidate = BuildUtcRead(access, shape);
        if (candidate is null || !PreservesTheRead(model, access, candidate))
        {
            return false;
        }

        replacement = candidate;
        return true;
    }

    /// <summary>Builds the UTC read that replaces one local-instant read.</summary>
    /// <param name="access">The reported member access.</param>
    /// <param name="shape">The local-instant shape it matched.</param>
    /// <returns>The UTC read, or <see langword="null"/> when the reported shape is not one this fix rewrites.</returns>
    private static MemberAccessExpressionSyntax? BuildUtcRead(MemberAccessExpressionSyntax access, ClockPropertyAccess.LocalInstant shape)
    {
        switch (shape)
        {
            case ClockPropertyAccess.LocalInstant.Now:
            {
                return WithName(access, ClockPropertyAccess.UtcNowName);
            }

            case ClockPropertyAccess.LocalInstant.Today:
            {
                // Local midnight becomes the UTC instant truncated to its date: DateTime.UtcNow.Date.
                var utcNow = access.WithName(SyntaxFactory.IdentifierName(ClockPropertyAccess.UtcNowName));
                return SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        utcNow,
                        SyntaxFactory.IdentifierName(ClockPropertyAccess.DatePropertyName))
                    .WithTriviaFrom(access);
            }

            case ClockPropertyAccess.LocalInstant.OffsetLocalDateTime:
            {
                // The clock moves to UtcNow and the projection moves with it: taking '.DateTime' off
                // 'UtcNow' would hand back the right ticks with DateTimeKind.Unspecified, which is the
                // ambiguity being fixed. '.UtcDateTime' carries DateTimeKind.Utc.
                if (access.Expression is not MemberAccessExpressionSyntax clock)
                {
                    return null;
                }

                return access
                    .WithExpression(WithName(clock, ClockPropertyAccess.UtcNowName))
                    .WithName(SyntaxFactory.IdentifierName(ClockPropertyAccess.UtcDateTimePropertyName).WithTriviaFrom(access.Name));
            }

            default:
            {
                return null;
            }
        }
    }

    /// <summary>Returns whether the rewritten read binds, and binds to the very type the read it replaces had.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="access">The reported member access.</param>
    /// <param name="candidate">The UTC read built for it.</param>
    /// <returns><see langword="true"/> when the rewrite compiles and changes nothing but the clock.</returns>
    /// <remarks>
    /// The type check is what makes the fix safe to offer: a rewrite that bound to a property of some other
    /// type would compile here and break at the next line that consumed it.
    /// </remarks>
    private static bool PreservesTheRead(SemanticModel model, MemberAccessExpressionSyntax access, MemberAccessExpressionSyntax candidate)
    {
        var speculative = model.GetSpeculativeSymbolInfo(access.SpanStart, candidate, SpeculativeBindingOption.BindAsExpression);
        if (speculative.Symbol is not IPropertySymbol)
        {
            return false;
        }

        var rewritten = model.GetSpeculativeTypeInfo(access.SpanStart, candidate, SpeculativeBindingOption.BindAsExpression).Type;
        return rewritten is not null
            && SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(access).Type, rewritten);
    }

    /// <summary>Renames the member a clock read names, keeping the receiver and the trivia as they were written.</summary>
    /// <param name="access">The member access to rename.</param>
    /// <param name="name">The member to read instead.</param>
    /// <returns>The renamed member access.</returns>
    private static MemberAccessExpressionSyntax WithName(MemberAccessExpressionSyntax access, string name)
        => access.WithName(SyntaxFactory.IdentifierName(name).WithTriviaFrom(access.Name));
}
