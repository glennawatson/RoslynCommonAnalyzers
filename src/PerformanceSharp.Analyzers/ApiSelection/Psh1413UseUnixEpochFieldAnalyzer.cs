// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports the Unix epoch constructed by hand (PSH1413) — <c>new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)</c>,
/// <c>new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)</c>, and the kindless <c>new DateTime(1970, 1, 1)</c>
/// that is far more dangerous than it looks. <c>DateTime.UnixEpoch</c> and
/// <c>DateTimeOffset.UnixEpoch</c> are the same value, already UTC, folded at compile time.
/// </summary>
/// <remarks>
/// <para>
/// <b>The kindless form is the bug this rule is really for.</b> <c>new DateTime(1970, 1, 1)</c> has
/// <see cref="DateTimeKind.Unspecified"/>, and an Unspecified <see cref="DateTime"/> is treated as local
/// the moment anything converts it — <c>ToUniversalTime</c>, a subtraction against a UTC value, a
/// serializer. The epoch then silently moves by the machine's offset, which is a bug that reproduces on
/// one developer's laptop and not another's. The rule reports it and the fix replaces it with the UTC
/// field, which is a deliberate correction, not a refactor.
/// </para>
/// <para>
/// <b>An explicitly non-UTC epoch is never reported.</b> <c>DateTimeKind.Local</c> names a different
/// instant and <c>DateTimeKind.Unspecified</c>, written out, is a choice — neither is
/// <c>DateTime.UnixEpoch</c>, so the rule leaves both alone rather than changing what the code
/// means. Only an explicit <c>Utc</c>, or a constructor with no kind parameter at all, matches.
/// </para>
/// <para>
/// <b>Gated on the field, never on a version number.</b> The two <c>UnixEpoch</c> fields arrived with
/// .NET Core 2.1 and .NET Standard 2.1, so each is probed for separately in the compilation; a target
/// framework that has neither registers no action, and one that has only one of them reports only that
/// one. Every component is read as a <em>constant</em>, so a named <c>const int EpochYear = 1970</c>
/// matches exactly as the literal does, and a computed value matches nothing.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1413UseUnixEpochFieldAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The replacement field.</summary>
    internal const string UnixEpochFieldName = "UnixEpoch";

    /// <summary>The metadata name of the date type.</summary>
    private const string DateTimeMetadataName = "System.DateTime";

    /// <summary>The metadata name of the offset date type.</summary>
    private const string DateTimeOffsetMetadataName = "System.DateTimeOffset";

    /// <summary>The simple name of the kind enum a date's last parameter may take.</summary>
    private const string DateTimeKindTypeName = "DateTimeKind";

    /// <summary>The kind that makes a hand-built epoch the real epoch.</summary>
    private const string UtcKindName = "Utc";

    /// <summary>The simple name of the offset type.</summary>
    private const string TimeSpanTypeName = "TimeSpan";

    /// <summary>The offset field that makes a hand-built epoch the real epoch.</summary>
    private const string ZeroOffsetName = "Zero";

    /// <summary>The fewest components an epoch is written with: year, month, day.</summary>
    private const int MinimumComponentCount = 3;

    /// <summary>The year component of the epoch.</summary>
    private const int EpochYear = 1970;

    /// <summary>The month and day components of the epoch, which are both the first of their unit.</summary>
    private const int EpochFirstDayOfMonth = 1;

    /// <summary>The position of the year component.</summary>
    private const int YearIndex = 0;

    /// <summary>The position of the month component.</summary>
    private const int MonthIndex = 1;

    /// <summary>The position of the day component.</summary>
    private const int DayIndex = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.UseUnixEpochField);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var dateTime = GetTypeWithUnixEpoch(start.Compilation, DateTimeMetadataName);
            var dateTimeOffset = GetTypeWithUnixEpoch(start.Compilation, DateTimeOffsetMetadataName);
            if (dateTime is null && dateTimeOffset is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeCreation(nodeContext, dateTime, dateTimeOffset),
                SyntaxKind.ObjectCreationExpression);
        });
    }

    /// <summary>Returns whether an allocation has the shape of a hand-written epoch, before any binding.</summary>
    /// <param name="creation">The allocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsEpochCreationShape(ObjectCreationExpressionSyntax creation)
        => creation.Initializer is null
            && creation.ArgumentList is { Arguments.Count: >= MinimumComponentCount }
            && creation.Type is NameSyntax;

    /// <summary>Reports PSH1413 for an epoch the framework already holds as a field.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="dateTime">The compilation's date type, when it has the field.</param>
    /// <param name="dateTimeOffset">The compilation's offset date type, when it has the field.</param>
    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol? dateTime, INamedTypeSymbol? dateTimeOffset)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (!IsEpochCreationShape(creation))
        {
            return;
        }

        var model = context.SemanticModel;
        if (model.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || GetEpochTypeName(constructor.ContainingType, dateTime, dateTimeOffset) is not { } typeName
            || !IsEpochArgumentList(model, constructor, creation.ArgumentList!, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.UseUnixEpochField,
            creation.SyntaxTree,
            creation.Span,
            typeName));
    }

    /// <summary>Names the constructed type when it is one that holds a <c>UnixEpoch</c> field.</summary>
    /// <param name="constructed">The constructor's containing type.</param>
    /// <param name="dateTime">The compilation's date type, when it has the field.</param>
    /// <param name="dateTimeOffset">The compilation's offset date type, when it has the field.</param>
    /// <returns>The type's name, or <see langword="null"/> when it is neither.</returns>
    private static string? GetEpochTypeName(INamedTypeSymbol constructed, INamedTypeSymbol? dateTime, INamedTypeSymbol? dateTimeOffset)
    {
        if (SymbolEqualityComparer.Default.Equals(constructed, dateTime))
        {
            return nameof(DateTime);
        }

        return SymbolEqualityComparer.Default.Equals(constructed, dateTimeOffset) ? nameof(DateTimeOffset) : null;
    }

    /// <summary>Returns whether every argument is the epoch's value for the component it fills.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="constructor">The bound constructor.</param>
    /// <param name="arguments">The written arguments.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the allocation spells out the Unix epoch.</returns>
    /// <remarks>
    /// Driven off the bound parameters rather than the argument count, which is what tells the seventh
    /// <see cref="int"/> (a millisecond) apart from a <see cref="DateTimeKind"/> in the same position, and
    /// what rejects every overload the rule cannot vouch for — a <c>Calendar</c>, a tick count, a
    /// <see cref="DateTime"/> — by simply not knowing what to expect there.
    /// </remarks>
    private static bool IsEpochArgumentList(SemanticModel model, IMethodSymbol constructor, ArgumentListSyntax arguments, CancellationToken cancellationToken)
    {
        var parameters = constructor.Parameters;
        if (parameters.Length != arguments.Arguments.Count)
        {
            return false;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            var argument = arguments.Arguments[i];
            if (argument.NameColon is not null || !IsEpochComponent(model, parameters[i].Type, argument.Expression, i, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether one argument is the epoch's value for the parameter it fills.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="parameterType">The parameter's type.</param>
    /// <param name="expression">The written argument.</param>
    /// <param name="index">The parameter's position.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the argument matches what the epoch has there.</returns>
    private static bool IsEpochComponent(
        SemanticModel model,
        ITypeSymbol parameterType,
        ExpressionSyntax expression,
        int index,
        CancellationToken cancellationToken)
    {
        if (parameterType.SpecialType == SpecialType.System_Int32)
        {
            return model.GetConstantValue(expression, cancellationToken).Value is int value && value == GetExpectedComponent(index);
        }

        if (IsNamedSystemType(parameterType, DateTimeKindTypeName))
        {
            return IsSystemMember(model, expression, DateTimeKindTypeName, UtcKindName, cancellationToken);
        }

        return IsNamedSystemType(parameterType, TimeSpanTypeName)
            && IsSystemMember(model, expression, TimeSpanTypeName, ZeroOffsetName, cancellationToken);
    }

    /// <summary>Gets the epoch's value for one numeric component.</summary>
    /// <param name="index">The component's position: year, month, day, then the time components.</param>
    /// <returns>The expected value.</returns>
    private static int GetExpectedComponent(int index) => index switch
    {
        YearIndex => EpochYear,
        MonthIndex or DayIndex => EpochFirstDayOfMonth,
        _ => 0,
    };

    /// <summary>Returns whether a type is a given type in the <c>System</c> namespace.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="name">The expected simple name.</param>
    /// <returns><see langword="true"/> when the type matches.</returns>
    private static bool IsNamedSystemType(ITypeSymbol type, string name)
        => type.Name == name
            && type.ContainingNamespace is { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true };

    /// <summary>Returns whether an argument binds to a named static field of a <c>System</c> type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The written argument.</param>
    /// <param name="typeName">The field's declaring type.</param>
    /// <param name="memberName">The field's name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the argument is that field.</returns>
    /// <remarks>
    /// Bound rather than matched on the written text, so <c>TimeSpan.Zero</c>, <c>System.TimeSpan.Zero</c>
    /// and an aliased spelling all count, and a local field that happens to be called <c>Zero</c> does not.
    /// </remarks>
    private static bool IsSystemMember(SemanticModel model, ExpressionSyntax expression, string typeName, string memberName, CancellationToken cancellationToken)
        => model.GetSymbolInfo(expression, cancellationToken).Symbol is IFieldSymbol { IsStatic: true } field
            && field.Name == memberName
            && IsNamedSystemType(field.ContainingType, typeName);

    /// <summary>Resolves a type only when the compilation's version of it holds the epoch field.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <param name="metadataName">The type's metadata name.</param>
    /// <returns>The type, or <see langword="null"/> when it is missing or has no <c>UnixEpoch</c>.</returns>
    private static INamedTypeSymbol? GetTypeWithUnixEpoch(Compilation compilation, string metadataName)
    {
        if (compilation.GetTypeByMetadataName(metadataName) is not { } type)
        {
            return null;
        }

        var members = type.GetMembers(UnixEpochFieldName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IFieldSymbol { IsStatic: true })
            {
                return type;
            }
        }

        return null;
    }
}
