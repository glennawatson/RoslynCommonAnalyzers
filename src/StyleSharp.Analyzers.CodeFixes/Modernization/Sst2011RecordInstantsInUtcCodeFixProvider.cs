// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
public sealed class Sst2011RecordInstantsInUtcCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.RecordInstantsInUtc.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Read the UTC clock",
            nameof(Sst2011RecordInstantsInUtcCodeFixProvider),
            CanRewrite,
            TryRewrite);

    /// <summary>Resolves the reported clock read and builds its UTC replacement, if the rewrite binds.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The reported read and its UTC replacement, or <see langword="null"/> when the fix cannot be offered.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not MemberAccessExpressionSyntax access)
        {
            return null;
        }

        var shape = ClockPropertyAccess.MatchLocalInstantSpelling(access);
        if (shape == ClockPropertyAccess.LocalInstant.None)
        {
            return null;
        }

        var candidate = BuildUtcRead(access, shape);
        return candidate is not null && PreservesTheRead(model, access, candidate)
            ? new NodeReplacement(access, candidate)
            : null;
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
                    SyntaxFactory.Token(default, SyntaxKind.DotToken, default),
                    SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                        default,
                        ClockPropertyAccess.DatePropertyName,
                        access.GetTrailingTrivia())));
            }

            case ClockPropertyAccess.LocalInstant.OffsetLocalDateTime:
            {
                // The clock moves to UtcNow and the projection moves with it: taking '.DateTime' off
                // 'UtcNow' would hand back the right ticks with DateTimeKind.Unspecified, which is the
                // ambiguity being fixed. '.UtcDateTime' carries DateTimeKind.Utc.
                return access.Expression is not MemberAccessExpressionSyntax clock
                    ? null
                    : access.Update(
                        WithName(clock, ClockPropertyAccess.UtcNowName),
                        access.OperatorToken,
                        SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                            access.Name.GetLeadingTrivia(),
                            ClockPropertyAccess.UtcDateTimePropertyName,
                            access.Name.GetTrailingTrivia())));
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MemberAccessExpressionSyntax WithName(MemberAccessExpressionSyntax access, string name) =>
        access.WithName(SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(access.Name.GetLeadingTrivia(), name, access.Name.GetTrailingTrivia())));

    /// <summary>Checks framework clock properties by symbol, retaining binding for lookalike receivers.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The document's semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the UTC read preserves the original type.</returns>
    private static bool CanRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        var shape = ClockPropertyAccess.MatchLocalInstantSpelling(access);
        if (shape == ClockPropertyAccess.LocalInstant.None)
        {
            return false;
        }

        var clock = shape == ClockPropertyAccess.LocalInstant.OffsetLocalDateTime
            ? (MemberAccessExpressionSyntax)access.Expression
            : access;
        if (model.GetSymbolInfo(clock.Expression).Symbol is INamedTypeSymbol { DeclaringSyntaxReferences.Length: 0 } type
            && (type.SpecialType == SpecialType.System_DateTime
                || SymbolEqualityComparer.Default.Equals(type, model.Compilation.GetTypeByMetadataName(ClockPropertyAccess.DateTimeOffsetMetadataName)))
            && GetClockPropertyType(type, ClockPropertyAccess.UtcNowName, isStatic: true) is { } utcType)
        {
            var rewrittenType = shape switch
            {
                ClockPropertyAccess.LocalInstant.Now => utcType,
                ClockPropertyAccess.LocalInstant.Today => GetClockPropertyType(utcType, ClockPropertyAccess.DatePropertyName, isStatic: false),
                ClockPropertyAccess.LocalInstant.OffsetLocalDateTime => GetClockPropertyType(utcType, ClockPropertyAccess.UtcDateTimePropertyName, isStatic: false),
                _ => null
            };
            return rewrittenType is not null
                && SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(access).Type, rewrittenType);
        }

        // A lookalike may hide members or bind its receiver differently after the rename.
        return TryRewrite(root, model, diagnostic) is not null;
    }

    /// <summary>Gets the type of an unambiguous public property on a framework clock type.</summary>
    /// <param name="type">The clock or projected value type.</param>
    /// <param name="name">The property name.</param>
    /// <param name="isStatic">The required receiver form.</param>
    /// <returns>The property type, or null when the member does not match.</returns>
    private static ITypeSymbol? GetClockPropertyType(ITypeSymbol type, string name, bool isStatic)
    {
        var members = type.GetMembers(name);
        return members.Length == 1
            && members[0] is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, Parameters.Length: 0 } property
            && property.IsStatic == isStatic
            ? property.Type
            : null;
    }
}
