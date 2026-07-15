// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a custom date/time format that leans on the culture's separators (SST2445): an unquoted <c>/</c>
/// (the date separator) or <c>:</c> (the time separator) in a custom format, formatted with a culture-sensitive
/// provider. Two shapes carry the defect — an explicit current-culture provider on a call such as
/// <c>ToString(format, provider)</c> or <c>ParseExact(...)</c>, and the implicit current culture of a plain
/// interpolated string — so a wire-format value silently changes shape when the process runs under another
/// culture.
/// </summary>
/// <remarks>
/// <para>
/// A call that passes the invariant culture is correct and is never reported, and a call that passes no
/// provider at all is out of scope. A separator that is already quoted or escaped is not a defect, and a
/// standard specifier (which carries no <c>/</c> or <c>:</c> in the format text) is culture-invariant by
/// definition. The receiver or the target of the call must be a date/time type, so the separators mean what
/// the rule assumes.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on the date/time types and the provider types resolving.
/// The clean path is a token compare and a character scan of the literal for a separator; nothing binds until
/// a candidate call or interpolation carries one.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2445CultureSensitiveDateFormatAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property naming which shape produced the report.</summary>
    internal const string ShapeKey = "SST2445.Shape";

    /// <summary>The shape value for a method-call report.</summary>
    internal const string InvocationShape = "Invocation";

    /// <summary>The shape value for an interpolated-string report.</summary>
    internal const string InterpolationShape = "Interpolation";

    /// <summary>The diagnostic property carrying the provider argument's span, as <c>start:length</c>.</summary>
    internal const string ProviderSpanKey = "SST2445.ProviderSpan";

    /// <summary>The parameter name of the format argument across the date/time methods.</summary>
    private const string FormatParameterName = "format";

    /// <summary>The metadata names of the date/time types whose separators the rule understands.</summary>
    private static readonly string[] DateTimeMetadataNames =
    [
        "System.DateTime",
        "System.DateTimeOffset",
        "System.DateOnly",
        "System.TimeOnly",
        "System.TimeSpan",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.CultureSensitiveDateFormat);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (DateFormatContext.TryCreate(start.Compilation) is not { } formatContext)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, formatContext), SyntaxKind.InvocationExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInterpolation(nodeContext, formatContext), SyntaxKind.InterpolatedStringExpression);
        });
    }

    /// <summary>Returns whether an invocation's callee is one of the date/time formatting or parsing methods.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the callee is a candidate.</returns>
    internal static bool IsCandidateName(InvocationExpressionSyntax invocation) => GetInvokedName(invocation) switch
    {
        "ToString" or "ParseExact" or "TryParseExact" => true,
        _ => false,
    };

    /// <summary>Analyzes one method call for a culture-sensitive custom date/time format.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="formatContext">The resolved date/time types.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, DateFormatContext formatContext)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList is not { } arguments || !IsCandidateName(invocation) || !HasSeparatorLiteral(arguments))
        {
            return;
        }

        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation
            || !formatContext.IsDateTimeType(operation.TargetMethod.ContainingType))
        {
            return;
        }

        var read = ReadFormatCall(context.SemanticModel, operation, formatContext);
        if (read.Format is null || read.ProviderExpression is null || !DateFormatText.HasUnquotedSeparator(read.Format))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(ShapeKey, InvocationShape)
            .Add(ProviderSpanKey, FormatSpan(read.ProviderExpression.Span));
        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.CultureSensitiveDateFormat, read.FormatLocation!, properties));
    }

    /// <summary>Analyzes one interpolated string for a culture-sensitive custom date/time format.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="formatContext">The resolved date/time types.</param>
    private static void AnalyzeInterpolation(SyntaxNodeAnalysisContext context, DateFormatContext formatContext)
    {
        var interpolated = (InterpolatedStringExpressionSyntax)context.Node;
        if (!HasSeparatorFormatClause(interpolated)
            || context.SemanticModel.GetTypeInfo(interpolated, context.CancellationToken).ConvertedType is not { SpecialType: SpecialType.System_String })
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(ShapeKey, InterpolationShape);
        foreach (var content in interpolated.Contents)
        {
            if (content is not InterpolationSyntax { FormatClause: { } clause } interpolation
                || !DateFormatText.HasUnquotedSeparator(clause.FormatStringToken.ValueText)
                || !formatContext.IsDateTimeType(context.SemanticModel.GetTypeInfo(interpolation.Expression, context.CancellationToken).Type))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.CultureSensitiveDateFormat,
                clause.FormatStringToken.GetLocation(),
                properties));
        }
    }

    /// <summary>Reads the constant format and the current-culture provider a formatting call passes.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="operation">The bound invocation.</param>
    /// <param name="formatContext">The resolved date/time types.</param>
    /// <returns>The format string, its location, and the culture-sensitive provider expression when all are present.</returns>
    private static FormatCall ReadFormatCall(SemanticModel model, IInvocationOperation operation, DateFormatContext formatContext)
    {
        string? format = null;
        Location? formatLocation = null;
        ExpressionSyntax? provider = null;
        foreach (var argument in operation.Arguments)
        {
            var parameter = argument.Parameter;
            if (parameter is null)
            {
                continue;
            }

            if (IsFormatParameter(parameter) && argument.Value.ConstantValue is { HasValue: true, Value: string constant })
            {
                format = constant;
                formatLocation = argument.Value.Syntax.GetLocation();
            }
            else if (formatContext.IsProviderParameter(parameter) && argument.Value.Syntax is ExpressionSyntax expression && formatContext.IsCurrentCultureProvider(model, expression))
            {
                provider = expression;
            }
        }

        return new FormatCall(format, formatLocation, provider);
    }

    /// <summary>Returns whether a parameter is the string format parameter.</summary>
    /// <param name="parameter">The bound parameter.</param>
    /// <returns><see langword="true"/> when it is the format parameter.</returns>
    private static bool IsFormatParameter(IParameterSymbol parameter)
        => parameter.Name == FormatParameterName && parameter.Type.SpecialType == SpecialType.System_String;

    /// <summary>Returns whether any argument is a string literal carrying a date or time separator.</summary>
    /// <param name="arguments">The call's argument list.</param>
    /// <returns><see langword="true"/> when a candidate literal is present.</returns>
    private static bool HasSeparatorLiteral(ArgumentListSyntax arguments)
    {
        foreach (var argument in arguments.Arguments)
        {
            if (argument.Expression is LiteralExpressionSyntax { Token.Value: string text } && ContainsSeparator(text))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an interpolated string has a format clause carrying a separator.</summary>
    /// <param name="interpolated">The interpolated string.</param>
    /// <returns><see langword="true"/> when a candidate format clause is present.</returns>
    private static bool HasSeparatorFormatClause(InterpolatedStringExpressionSyntax interpolated)
    {
        foreach (var content in interpolated.Contents)
        {
            if (content is InterpolationSyntax { FormatClause: { } clause } && ContainsSeparator(clause.FormatStringToken.ValueText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a string carries a date or time separator character.</summary>
    /// <param name="text">The text to scan.</param>
    /// <returns><see langword="true"/> when a <c>/</c> or <c>:</c> is present.</returns>
    private static bool ContainsSeparator(string text)
        => text.IndexOf('/') >= 0 || text.IndexOf(':') >= 0;

    /// <summary>Formats a span as <c>start:length</c> for a diagnostic property.</summary>
    /// <param name="span">The span to format.</param>
    /// <returns>The formatted span.</returns>
    private static string FormatSpan(Microsoft.CodeAnalysis.Text.TextSpan span)
        => span.Start.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":"
            + span.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Returns the invoked member's simple name text for the supported call shapes.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The invoked name, or <see langword="null"/> for unsupported expression shapes.</returns>
    private static string? GetInvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => null,
    };

    /// <summary>The pieces of a formatting call the rule reports on.</summary>
    /// <param name="Format">The constant format string, when present.</param>
    /// <param name="FormatLocation">The location of the format expression, when present.</param>
    /// <param name="ProviderExpression">The current-culture provider expression, when present.</param>
    private readonly record struct FormatCall(string? Format, Location? FormatLocation, ExpressionSyntax? ProviderExpression);

    /// <summary>The date/time and provider types resolved once per compilation.</summary>
    private sealed class DateFormatContext
    {
        /// <summary>The resolved date/time types.</summary>
        private readonly ImmutableArray<INamedTypeSymbol> _dateTimeTypes;

        /// <summary>The resolved format-provider interface.</summary>
        private readonly INamedTypeSymbol _formatProvider;

        /// <summary>The resolved culture type.</summary>
        private readonly INamedTypeSymbol _cultureInfo;

        /// <summary>Initializes a new instance of the <see cref="DateFormatContext"/> class.</summary>
        /// <param name="dateTimeTypes">The resolved date/time types.</param>
        /// <param name="formatProvider">The resolved format-provider interface.</param>
        /// <param name="cultureInfo">The resolved culture type.</param>
        private DateFormatContext(ImmutableArray<INamedTypeSymbol> dateTimeTypes, INamedTypeSymbol formatProvider, INamedTypeSymbol cultureInfo)
        {
            _dateTimeTypes = dateTimeTypes;
            _formatProvider = formatProvider;
            _cultureInfo = cultureInfo;
        }

        /// <summary>Resolves the types the rule needs, or returns <see langword="null"/> when they are absent.</summary>
        /// <param name="compilation">The compilation to resolve against.</param>
        /// <returns>The resolved context, or <see langword="null"/>.</returns>
        public static DateFormatContext? TryCreate(Compilation compilation)
        {
            var formatProvider = compilation.GetTypeByMetadataName("System.IFormatProvider");
            var cultureInfo = compilation.GetTypeByMetadataName("System.Globalization.CultureInfo");
            if (formatProvider is null || cultureInfo is null)
            {
                return null;
            }

            var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(DateTimeMetadataNames.Length);
            foreach (var name in DateTimeMetadataNames)
            {
                if (compilation.GetTypeByMetadataName(name) is { } type)
                {
                    builder.Add(type);
                }
            }

            return builder.Count == 0 ? null : new DateFormatContext(builder.ToImmutable(), formatProvider, cultureInfo);
        }

        /// <summary>Returns whether a type is one of the date/time types, unwrapping a nullable value type.</summary>
        /// <param name="type">The type to test.</param>
        /// <returns><see langword="true"/> when it is a date/time type.</returns>
        public bool IsDateTimeType(ITypeSymbol? type)
        {
            if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: [var underlying] })
            {
                type = underlying;
            }

            foreach (var candidate in _dateTimeTypes)
            {
                if (SymbolEqualityComparer.Default.Equals(type, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether a parameter is the format-provider parameter.</summary>
        /// <param name="parameter">The bound parameter.</param>
        /// <returns><see langword="true"/> when it is the provider parameter.</returns>
        public bool IsProviderParameter(IParameterSymbol parameter)
            => SymbolEqualityComparer.Default.Equals(parameter.Type, _formatProvider);

        /// <summary>Returns whether an expression is the current culture or current UI culture.</summary>
        /// <param name="model">The semantic model.</param>
        /// <param name="expression">The provider expression.</param>
        /// <returns><see langword="true"/> when the provider is culture-sensitive.</returns>
        public bool IsCurrentCultureProvider(SemanticModel model, ExpressionSyntax expression)
            => model.GetSymbolInfo(expression).Symbol is IPropertySymbol { Name: "CurrentCulture" or "CurrentUICulture" } property
                && SymbolEqualityComparer.Default.Equals(property.ContainingType, _cultureInfo);
    }
}
