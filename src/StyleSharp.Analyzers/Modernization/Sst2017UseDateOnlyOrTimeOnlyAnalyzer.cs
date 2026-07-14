// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the two places where the code itself proves a <c>DateTime</c> was only ever a date or only ever a
/// time of day (SST2017): <c>value.Date</c>, which should be <c>DateOnly.FromDateTime(value)</c>, and
/// <c>value.TimeOfDay</c>, which should be <c>TimeOnly.FromDateTime(value)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Only these two property reads are reported. The rule never guesses from a member's name — an
/// <c>ExpiryDate</c> may well want a time, and a rule that read intent out of an identifier would be a
/// false-positive machine.
/// </para>
/// <para>
/// A read whose receiver is the machine clock itself (<c>DateTime.Now.Date</c>) is left alone: that
/// expression belongs to SST2010, which asks the caller to take a <c>TimeProvider</c>, and one line should
/// not carry two rules arguing about it.
/// </para>
/// <para>
/// <c>DateOnly</c> and <c>TimeOnly</c> arrived in .NET 6 and are resolved independently once per compilation:
/// a framework with neither gets no registration at all, and a framework with only one of them is only told
/// about the half it can act on.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2017UseDateOnlyOrTimeOnlyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the date-only type this rule asks for.</summary>
    private const string DateOnlyMetadataName = "System.DateOnly";

    /// <summary>The metadata name of the time-only type this rule asks for.</summary>
    private const string TimeOnlyMetadataName = "System.TimeOnly";

    /// <summary>The <c>DateTime.Date</c> property name.</summary>
    private const string DateMemberName = "Date";

    /// <summary>The <c>DateTime.TimeOfDay</c> property name.</summary>
    private const string TimeOfDayMemberName = "TimeOfDay";

    /// <summary>The type suggested for a date-only value.</summary>
    private const string DateOnlyTypeName = "DateOnly";

    /// <summary>The type suggested for a time-only value.</summary>
    private const string TimeOnlyTypeName = "TimeOnly";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.UseDateOnlyOrTimeOnly);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var suggestions = SplitTypes.Resolve(start.Compilation);
            if (!suggestions.Any)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, suggestions),
                SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    /// <summary>Reports one <c>.Date</c> or <c>.TimeOfDay</c> read on a <c>DateTime</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="suggestions">The split types resolved for this compilation.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, in SplitTypes suggestions)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        var suggested = SuggestedTypeFor(access, suggestions);
        if (suggested is null || IsClockRead(access.Expression))
        {
            return;
        }

        if (!ReadsDateTimeProperty(context.SemanticModel, access, suggestions.DateTime, context.CancellationToken))
        {
            return;
        }

        var diagnostic = DiagnosticHelper.Create(
            ModernizationRules.UseDateOnlyOrTimeOnly,
            access.GetLocation(),
            suggested,
            access.Name.Identifier.ValueText);
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>Returns the type this member read should have produced, by spelling alone.</summary>
    /// <param name="access">The member access to inspect.</param>
    /// <param name="suggestions">The split types resolved for this compilation.</param>
    /// <returns><c>DateOnly</c>, <c>TimeOnly</c>, or <see langword="null"/> when the read is neither, or when the type it would name is absent.</returns>
    private static string? SuggestedTypeFor(MemberAccessExpressionSyntax access, in SplitTypes suggestions)
        => access.Name.Identifier.ValueText switch
        {
            DateMemberName when suggestions.DateOnly is not null => DateOnlyTypeName,
            TimeOfDayMemberName when suggestions.TimeOnly is not null => TimeOnlyTypeName,
            _ => null,
        };

    /// <summary>Returns whether the receiver is a direct read of the machine clock, which SST2010 owns.</summary>
    /// <param name="receiver">The expression the member is read from.</param>
    /// <returns><see langword="true"/> when the receiver is spelled like a clock property.</returns>
    private static bool IsClockRead(ExpressionSyntax receiver)
        => receiver is MemberAccessExpressionSyntax inner && ClockPropertyAccess.MatchesSpelling(inner, localOnly: false);

    /// <summary>Returns whether a member access really reads an instance property of <c>System.DateTime</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="access">The member access, already past the spelling gate.</param>
    /// <param name="dateTime">The resolved <c>System.DateTime</c> symbol.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the receiver is a <c>DateTime</c>.</returns>
    /// <remarks>
    /// <c>DateTimeOffset</c> exposes both members too, and is not reported: the value there still needs its
    /// offset applied before a date can be named, which is a decision the rule has no business making.
    /// </remarks>
    private static bool ReadsDateTimeProperty(
        SemanticModel model,
        MemberAccessExpressionSyntax access,
        INamedTypeSymbol? dateTime,
        CancellationToken cancellationToken)
    {
        if (dateTime is null || model.GetSymbolInfo(access, cancellationToken).Symbol is not IPropertySymbol { IsStatic: false } property)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(property.ContainingType, dateTime);
    }

    /// <summary>The types this rule can suggest, resolved once per compilation.</summary>
    /// <param name="DateTime">The <c>System.DateTime</c> symbol, or <see langword="null"/>.</param>
    /// <param name="DateOnly">The <c>System.DateOnly</c> symbol, or <see langword="null"/> on a framework without it.</param>
    /// <param name="TimeOnly">The <c>System.TimeOnly</c> symbol, or <see langword="null"/> on a framework without it.</param>
    private readonly record struct SplitTypes(INamedTypeSymbol? DateTime, INamedTypeSymbol? DateOnly, INamedTypeSymbol? TimeOnly)
    {
        /// <summary>Gets a value indicating whether there is anything at all this rule could suggest here.</summary>
        public bool Any => DateTime is not null && (DateOnly is not null || TimeOnly is not null);

        /// <summary>Resolves the split types for one compilation.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The resolved types.</returns>
        public static SplitTypes Resolve(Compilation compilation) => new(
            compilation.GetTypeByMetadataName(ClockPropertyAccess.DateTimeMetadataName),
            compilation.GetTypeByMetadataName(DateOnlyMetadataName),
            compilation.GetTypeByMetadataName(TimeOnlyMetadataName));
    }
}
