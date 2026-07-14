// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Recognizes a read of the machine clock — <c>DateTime.Now</c>, <c>DateTime.UtcNow</c>,
/// <c>DateTimeOffset.Now</c>, <c>DateTimeOffset.UtcNow</c> — for the rules that care where the time comes
/// from (SST2010) and which clock it came from (SST2011), and the three ways a value can carry the
/// <em>local</em> instant that SST2011 reports: <c>Now</c>, <c>DateTime.Today</c>, and
/// <c>DateTimeOffset.Now.DateTime</c>.
/// </summary>
/// <remarks>
/// The syntactic gate runs first and rejects on a couple of string comparisons: the member has to be spelled
/// <c>Now</c>, <c>UtcNow</c>, <c>Today</c> or <c>DateTime</c>, and the receiver has to be spelled
/// <c>DateTime</c>, <c>DateTimeOffset</c>, or — for the last of those — a <c>DateTimeOffset.Now</c>. Only a
/// member access that survives the gate is bound, so a type of the user's own that happens to expose a
/// <c>Now</c> costs one bind and everything else costs nothing.
/// </remarks>
internal static class ClockPropertyAccess
{
    /// <summary>The local-clock property name.</summary>
    public const string NowName = "Now";

    /// <summary>The UTC-clock property name.</summary>
    public const string UtcNowName = "UtcNow";

    /// <summary>The local-midnight property name.</summary>
    public const string TodayName = "Today";

    /// <summary>The <c>DateTimeOffset</c> property that hands back the local <c>DateTime</c>, offset discarded.</summary>
    public const string DateTimePropertyName = "DateTime";

    /// <summary>The <c>DateTimeOffset</c> property that hands back the UTC <c>DateTime</c>, kind and all.</summary>
    public const string UtcDateTimePropertyName = "UtcDateTime";

    /// <summary>The <c>DateTime</c> property that truncates an instant to its date.</summary>
    public const string DatePropertyName = "Date";

    /// <summary>The <c>System.DateTime</c> metadata name.</summary>
    public const string DateTimeMetadataName = "System.DateTime";

    /// <summary>The <c>System.DateTimeOffset</c> metadata name.</summary>
    public const string DateTimeOffsetMetadataName = "System.DateTimeOffset";

    /// <summary>The <c>DateTime</c> type name as it is written.</summary>
    private const string DateTimeTypeName = "DateTime";

    /// <summary>The <c>DateTimeOffset</c> type name as it is written.</summary>
    private const string DateTimeOffsetTypeName = "DateTimeOffset";

    /// <summary>The ways a recorded value can carry the local instant rather than the UTC one.</summary>
    internal enum LocalInstant
    {
        /// <summary>Not a local-instant read.</summary>
        None,

        /// <summary><c>DateTime.Now</c> or <c>DateTimeOffset.Now</c>.</summary>
        Now,

        /// <summary><c>DateTime.Today</c> — local midnight, which discards the offset exactly as <c>Now</c> does.</summary>
        Today,

        /// <summary><c>DateTimeOffset.Now.DateTime</c> — the local <c>DateTime</c> taken out of an offset-carrying value, throwing the offset away.</summary>
        OffsetLocalDateTime,
    }

    /// <summary>Returns whether a member access is spelled like a clock read.</summary>
    /// <param name="access">The member access to inspect.</param>
    /// <param name="localOnly">Whether only the local clock (<c>Now</c>) counts.</param>
    /// <returns><see langword="true"/> when the spelling matches; the symbol is not yet bound.</returns>
    public static bool MatchesSpelling(MemberAccessExpressionSyntax access, bool localOnly)
    {
        var member = access.Name.Identifier.ValueText;
        if (member != NowName && (localOnly || member != UtcNowName))
        {
            return false;
        }

        var receiver = GetSimpleName(access.Expression);
        return receiver is DateTimeTypeName or DateTimeOffsetTypeName;
    }

    /// <summary>Classifies a member access as one of the local-instant shapes, by spelling alone.</summary>
    /// <param name="access">The member access to inspect.</param>
    /// <returns>The shape, or <see cref="LocalInstant.None"/>; the symbol is not yet bound.</returns>
    /// <remarks>
    /// <c>UtcNow</c>, <c>DateTimeOffset.Now.UtcDateTime</c> and <c>DateTimeOffset.UtcNow.DateTime</c> are all
    /// <see cref="LocalInstant.None"/>: each already carries the UTC instant, which is what the rule is
    /// asking for.
    /// </remarks>
    public static LocalInstant MatchLocalInstantSpelling(MemberAccessExpressionSyntax access)
    {
        switch (access.Name.Identifier.ValueText)
        {
            case NowName:
            {
                return GetSimpleName(access.Expression) is DateTimeTypeName or DateTimeOffsetTypeName
                    ? LocalInstant.Now
                    : LocalInstant.None;
            }

            case TodayName:
            {
                return GetSimpleName(access.Expression) == DateTimeTypeName ? LocalInstant.Today : LocalInstant.None;
            }

            case DateTimePropertyName:
            {
                return access.Expression is MemberAccessExpressionSyntax offsetClock
                    && offsetClock.Name.Identifier.ValueText == NowName
                    && GetSimpleName(offsetClock.Expression) == DateTimeOffsetTypeName
                        ? LocalInstant.OffsetLocalDateTime
                        : LocalInstant.None;
            }

            default:
            {
                return LocalInstant.None;
            }
        }
    }

    /// <summary>Returns whether a member access that is spelled like a local-instant read really is one.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="access">The member access, already past <see cref="MatchLocalInstantSpelling"/>.</param>
    /// <param name="shape">The shape the spelling matched.</param>
    /// <param name="clockTypes">The resolved clock types.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the access reads a local instant off the framework's own clock types.</returns>
    public static bool BindsToLocalInstant(
        SemanticModel model,
        MemberAccessExpressionSyntax access,
        LocalInstant shape,
        in ClockTypes clockTypes,
        CancellationToken cancellationToken)
        => shape switch
        {
            LocalInstant.Now or LocalInstant.Today => BindsToClock(model, access, clockTypes, cancellationToken),
            LocalInstant.OffsetLocalDateTime => BindsToOffsetLocalDateTime(model, access, clockTypes, cancellationToken),
            _ => false,
        };

    /// <summary>Returns whether a member access really binds to a static clock property of the framework type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="access">The member access, already past <see cref="MatchesSpelling"/>.</param>
    /// <param name="clockTypes">The resolved clock types.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the access reads the framework clock.</returns>
    public static bool BindsToClock(
        SemanticModel model,
        MemberAccessExpressionSyntax access,
        in ClockTypes clockTypes,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(access, cancellationToken).Symbol is not IPropertySymbol { IsStatic: true } property)
        {
            return false;
        }

        var containing = property.ContainingType;
        return SymbolEqualityComparer.Default.Equals(containing, clockTypes.DateTime)
            || SymbolEqualityComparer.Default.Equals(containing, clockTypes.DateTimeOffset);
    }

    /// <summary>Builds the message text for a clock read, as the source spells it.</summary>
    /// <param name="access">The reported member access.</param>
    /// <returns>Text of the form <c>DateTime.UtcNow</c>, or <c>DateTimeOffset.Now.DateTime</c> for a read taken off the clock.</returns>
    /// <remarks>Only reached once a diagnostic is being reported, so the concatenation never costs a clean file.</remarks>
    public static string Describe(MemberAccessExpressionSyntax access)
    {
        // A projection off the clock keeps the clock in the text: 'Now.DateTime' would name nothing.
        var receiver = access.Expression is MemberAccessExpressionSyntax clock && clock.Name.Identifier.ValueText == NowName
            ? Describe(clock)
            : GetSimpleName(access.Expression);

        return receiver + "." + access.Name.Identifier.ValueText;
    }

    /// <summary>Returns whether a <c>.DateTime</c> read really takes the local time out of the framework's <c>DateTimeOffset.Now</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="access">The <c>.DateTime</c> member access.</param>
    /// <param name="clockTypes">The resolved clock types.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when both halves bind to <c>System.DateTimeOffset</c>.</returns>
    private static bool BindsToOffsetLocalDateTime(
        SemanticModel model,
        MemberAccessExpressionSyntax access,
        in ClockTypes clockTypes,
        CancellationToken cancellationToken)
    {
        if (clockTypes.DateTimeOffset is null
            || model.GetSymbolInfo(access, cancellationToken).Symbol is not IPropertySymbol { IsStatic: false } property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, clockTypes.DateTimeOffset))
        {
            return false;
        }

        return access.Expression is MemberAccessExpressionSyntax clock
            && BindsToClock(model, clock, clockTypes, cancellationToken);
    }

    /// <summary>Gets the rightmost identifier of a receiver expression.</summary>
    /// <param name="expression">The receiver of the member access.</param>
    /// <returns>The simple name, or an empty string when the receiver is not a name.</returns>
    private static string GetSimpleName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax qualified => qualified.Name.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>The clock types resolved once per compilation.</summary>
    /// <param name="DateTime">The <c>System.DateTime</c> symbol, or <see langword="null"/>.</param>
    /// <param name="DateTimeOffset">The <c>System.DateTimeOffset</c> symbol, or <see langword="null"/>.</param>
    internal readonly record struct ClockTypes(INamedTypeSymbol? DateTime, INamedTypeSymbol? DateTimeOffset)
    {
        /// <summary>Gets a value indicating whether either clock type is present in the compilation.</summary>
        public bool Any => DateTime is not null || DateTimeOffset is not null;

        /// <summary>Resolves the clock types for one compilation.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The resolved clock types.</returns>
        public static ClockTypes Resolve(Compilation compilation) => new(
            compilation.GetTypeByMetadataName(DateTimeMetadataName),
            compilation.GetTypeByMetadataName(DateTimeOffsetMetadataName));
    }
}
