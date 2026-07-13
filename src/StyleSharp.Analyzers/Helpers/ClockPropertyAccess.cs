// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Recognizes a read of the machine clock — <c>DateTime.Now</c>, <c>DateTime.UtcNow</c>,
/// <c>DateTimeOffset.Now</c>, <c>DateTimeOffset.UtcNow</c> — for the rules that care where the time comes
/// from (SST2010) and which clock it came from (SST2011).
/// </summary>
/// <remarks>
/// The syntactic gate runs first and rejects on two string comparisons: the member has to be spelled
/// <c>Now</c> or <c>UtcNow</c>, and the receiver has to be spelled <c>DateTime</c> or <c>DateTimeOffset</c>.
/// Only a member access that survives both is bound, so a type of the user's own that happens to expose a
/// <c>Now</c> costs one bind and everything else costs nothing.
/// </remarks>
internal static class ClockPropertyAccess
{
    /// <summary>The local-clock property name.</summary>
    public const string NowName = "Now";

    /// <summary>The UTC-clock property name.</summary>
    public const string UtcNowName = "UtcNow";

    /// <summary>The <c>System.DateTime</c> metadata name.</summary>
    public const string DateTimeMetadataName = "System.DateTime";

    /// <summary>The <c>System.DateTimeOffset</c> metadata name.</summary>
    public const string DateTimeOffsetMetadataName = "System.DateTimeOffset";

    /// <summary>The <c>DateTime</c> type name as it is written.</summary>
    private const string DateTimeTypeName = "DateTime";

    /// <summary>The <c>DateTimeOffset</c> type name as it is written.</summary>
    private const string DateTimeOffsetTypeName = "DateTimeOffset";

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
    /// <returns>Text of the form <c>DateTime.UtcNow</c>.</returns>
    /// <remarks>Only reached once a diagnostic is being reported, so the concatenation never costs a clean file.</remarks>
    public static string Describe(MemberAccessExpressionSyntax access)
        => GetSimpleName(access.Expression) + "." + access.Name.Identifier.ValueText;

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
