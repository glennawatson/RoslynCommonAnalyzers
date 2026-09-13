// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a call to the third-party TimeZoneConverter package (PSH1419) — <c>TZConvert.GetTimeZoneInfo</c>,
/// <c>TZConvert.IanaToWindows</c>, and <c>TZConvert.WindowsToIana</c> — where the built-in
/// <see cref="System.TimeZoneInfo"/> now does the same work. Since .NET 6
/// <c>TimeZoneInfo.FindSystemTimeZoneById</c> resolves both IANA and Windows ids on every platform, and
/// <c>TimeZoneInfo.TryConvertIanaIdToWindowsId</c> / <c>TryConvertWindowsIdToIanaId</c> convert between the
/// two id styles, so the package is an avoidable dependency.
/// </summary>
/// <remarks>
/// <para>
/// The rule resolves <c>TimeZoneConverter.TZConvert</c> on first demand after a candidate method name
/// passes the syntax check, caching missing types too. It is gated a second
/// time on the replacement existing — <see cref="System.TimeZoneInfo"/> with a
/// <c>FindSystemTimeZoneById</c> method — so a framework that has no built-in equivalent is never handed a
/// suggestion it cannot take. The conversion helpers arrived with .NET 6 and are each probed separately;
/// the id-conversion calls are reported only where the matching helper resolves, while
/// <c>GetTimeZoneInfo</c> maps to <c>FindSystemTimeZoneById</c>, which exists wherever
/// <see cref="System.TimeZoneInfo"/> does.
/// </para>
/// <para>
/// The clean path costs one switch over the invoked simple name; only a call that spells one of the three
/// method names — and whose replacement is available — is bound, and only then is its receiver confirmed to
/// be <c>TZConvert</c>, so an unrelated <c>GetTimeZoneInfo</c> of your own is never confused with it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1419PreferBuiltInTimeZoneAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The converter method that returns a time zone from a Windows or IANA id.</summary>
    internal const string GetTimeZoneInfoMethodName = "GetTimeZoneInfo";

    /// <summary>The converter method that maps an IANA id to a Windows id.</summary>
    private const string IanaToWindowsMethodName = "IanaToWindows";

    /// <summary>The converter method that maps a Windows id to an IANA id.</summary>
    private const string WindowsToIanaMethodName = "WindowsToIana";

    /// <summary>The built-in replacement for <see cref="GetTimeZoneInfoMethodName"/>.</summary>
    private const string FindSystemTimeZoneByIdReplacement = "TimeZoneInfo.FindSystemTimeZoneById";

    /// <summary>The built-in replacement for <see cref="IanaToWindowsMethodName"/>.</summary>
    private const string TryConvertIanaIdToWindowsIdReplacement = "TimeZoneInfo.TryConvertIanaIdToWindowsId";

    /// <summary>The built-in replacement for <see cref="WindowsToIanaMethodName"/>.</summary>
    private const string TryConvertWindowsIdToIanaIdReplacement = "TimeZoneInfo.TryConvertWindowsIdToIanaId";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ApiSelectionRules.PreferBuiltInTimeZone);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var types = new TimeZoneTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, types),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation is a single-argument <c>GetTimeZoneInfo</c> call, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the code fix can rewrite it one-to-one.</returns>
    internal static bool IsGetTimeZoneInfoInvocation(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Count == 1
            && GetInvokedSimpleName(invocation.Expression) == GetTimeZoneInfoMethodName;

    /// <summary>Reports PSH1419 for a converter call whose built-in replacement is available.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The lazily resolved converter and replacement availability.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, TimeZoneTypes types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var methodName = GetInvokedSimpleName(invocation.Expression);
        if (methodName is not (GetTimeZoneInfoMethodName or IanaToWindowsMethodName or WindowsToIanaMethodName))
        {
            return;
        }

        var resolved = types.Get();
        if (resolved.ConverterType is not { } converterType
            || GetReplacement(methodName, resolved.HasIanaToWindows, resolved.HasWindowsToIana) is not { } replacement)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, converterType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.PreferBuiltInTimeZone,
            invocation.SyntaxTree,
            invocation.Span,
            replacement));
    }

    /// <summary>Maps a converter method name to its available built-in replacement, without binding.</summary>
    /// <param name="methodName">The invoked simple name.</param>
    /// <param name="hasIanaToWindows">Whether the IANA-to-Windows conversion helper resolves.</param>
    /// <param name="hasWindowsToIana">Whether the Windows-to-IANA conversion helper resolves.</param>
    /// <returns>The replacement display name, or <see langword="null"/> when there is no available replacement.</returns>
    private static string? GetReplacement(string methodName, bool hasIanaToWindows, bool hasWindowsToIana) => methodName switch
    {
        GetTimeZoneInfoMethodName => FindSystemTimeZoneByIdReplacement,
        IanaToWindowsMethodName => hasIanaToWindows ? TryConvertIanaIdToWindowsIdReplacement : null,
        WindowsToIanaMethodName => hasWindowsToIana ? TryConvertWindowsIdToIanaIdReplacement : null,
        _ => null,
    };

    /// <summary>Returns the simple name an invocation calls, without binding it.</summary>
    /// <param name="expression">The invoked expression.</param>
    /// <returns>The rightmost identifier, or <see langword="null"/> when the expression names none.</returns>
    private static string? GetInvokedSimpleName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        _ => null,
    };

    /// <summary>The converter type and the available built-in conversion helpers.</summary>
    /// <param name="ConverterType">The converter type, or null when a required type or replacement is absent.</param>
    /// <param name="HasIanaToWindows">Whether the IANA-to-Windows helper is available.</param>
    /// <param name="HasWindowsToIana">Whether the Windows-to-IANA helper is available.</param>
    private readonly record struct TimeZoneGate(INamedTypeSymbol? ConverterType, bool HasIanaToWindows, bool HasWindowsToIana);

    /// <summary>Resolves converter and replacement availability only for candidate calls.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    private sealed class TimeZoneTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the converter package's static entry-point type.</summary>
        private const string TimeZoneConverterTypeMetadataName = "TimeZoneConverter.TZConvert";

        /// <summary>The metadata name of the built-in time-zone type.</summary>
        private const string TimeZoneInfoMetadataName = "System.TimeZoneInfo";

        /// <summary>The built-in method whose presence gates <see cref="GetTimeZoneInfoMethodName"/>.</summary>
        private const string FindSystemTimeZoneByIdMethodName = "FindSystemTimeZoneById";

        /// <summary>The built-in method whose presence gates <see cref="IanaToWindowsMethodName"/> (.NET 6+).</summary>
        private const string TryConvertIanaIdToWindowsIdMethodName = "TryConvertIanaIdToWindowsId";

        /// <summary>The built-in method whose presence gates <see cref="WindowsToIanaMethodName"/> (.NET 6+).</summary>
        private const string TryConvertWindowsIdToIanaIdMethodName = "TryConvertWindowsIdToIanaId";

        /// <summary>The cached availability, including missing types, or null before the first candidate.</summary>
        private TimeZoneGate[]? _resolved;

        /// <summary>Gets the converter and replacement availability on first demand.</summary>
        /// <returns>The resolved converter and replacement availability.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeZoneGate Get() => (_resolved ??= [Resolve(compilation)])[0];

        /// <summary>Resolves the converter type and probes each built-in replacement once.</summary>
        /// <param name="compilation">The compilation being analyzed.</param>
        /// <returns>The converter and replacement availability, or an empty gate when unavailable.</returns>
        private static TimeZoneGate Resolve(Compilation compilation) =>
            compilation.GetTypeByMetadataName(TimeZoneConverterTypeMetadataName) is not { } converterType
                || compilation.GetTypeByMetadataName(TimeZoneInfoMetadataName) is not { } timeZoneInfoType
                || timeZoneInfoType.GetMembers(FindSystemTimeZoneByIdMethodName).IsEmpty
                ? default
                : new(
                    converterType,
                    !timeZoneInfoType.GetMembers(TryConvertIanaIdToWindowsIdMethodName).IsEmpty,
                    !timeZoneInfoType.GetMembers(TryConvertWindowsIdToIanaIdMethodName).IsEmpty);
    }
}
