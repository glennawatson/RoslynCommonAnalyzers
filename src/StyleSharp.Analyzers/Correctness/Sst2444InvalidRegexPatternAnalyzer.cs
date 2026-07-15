// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.CodeAnalysis.Operations;

using RegexEngine = System.Text.RegularExpressions.Regex;
using RegexEngineOptions = System.Text.RegularExpressions.RegexOptions;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a constant regular-expression pattern that does not parse (SST2444). Rather than reimplement the
/// grammar, the analyzer constructs the real engine with the real options the call passes and reports the
/// engine's own error, so a pattern that is legal only under a particular option is judged correctly and a
/// pattern that is illegal is reported with an exact message.
/// </summary>
/// <remarks>
/// <para>
/// Two option bits are handled before construction: the compile bit is stripped, because it does not change
/// whether the pattern parses and constructing with it would spin up code generation on the keystroke path;
/// the non-backtracking bit is kept, because it legitimately rejects backreferences and lookarounds, so
/// dropping it would let those patterns pass. The engine never runs a match, so there is no catastrophic
/// backtracking to trigger, and every result is cached per compilation against the pattern and its effective
/// options.
/// </para>
/// <para>
/// The clean path is a token compare and a scan for a literal argument; nothing binds until a call names the
/// engine or one of its static query methods and passes a string literal. The whole rule is gated at
/// compilation start on the engine type resolving.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2444InvalidRegexPatternAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the pattern parameter across the engine's constructors and static methods.</summary>
    private const string PatternParameterName = "pattern";

    /// <summary>The simple name of the engine type in a construction.</summary>
    private const string RegexTypeName = "Regex";

    /// <summary>The metadata name of the engine type.</summary>
    private const string RegexMetadataName = "System.Text.RegularExpressions.Regex";

    /// <summary>The metadata name of the options enum.</summary>
    private const string RegexOptionsMetadataName = "System.Text.RegularExpressions.RegexOptions";

    /// <summary>The compile option bit, stripped before validating.</summary>
    private const int CompiledOption = 0x0008;

    /// <summary>The non-backtracking option bit, kept when the host engine supports it.</summary>
    private const int NonBacktrackingOption = 0x0400;

    /// <summary>A finite construction timeout; matching is never run, so the value is never consulted.</summary>
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Whether the host engine accepts the non-backtracking option.</summary>
    private static readonly bool HostSupportsNonBacktracking = ComputeNonBacktrackingSupport();

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.InvalidRegexPattern);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(RegexMetadataName) is not { } regexType)
            {
                return;
            }

            var optionsType = start.Compilation.GetTypeByMetadataName(RegexOptionsMetadataName);
            var cache = new ConcurrentDictionary<(string Pattern, int Options), string?>();
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, regexType, optionsType, cache),
                SyntaxKind.InvocationExpression,
                SyntaxKind.ObjectCreationExpression);
        });
    }

    /// <summary>Returns whether a call's callee names the engine or one of its query methods, before binding.</summary>
    /// <param name="node">The invocation or construction.</param>
    /// <returns><see langword="true"/> when the callee is worth binding.</returns>
    internal static bool NamesRegexApi(SyntaxNode node) => node switch
    {
        ObjectCreationExpressionSyntax creation => GetSimpleName(creation.Type) == RegexTypeName,
        InvocationExpressionSyntax invocation => IsQueryMethod(GetInvokedName(invocation)),
        _ => false,
    };

    /// <summary>Analyzes one regex call for an unparseable constant pattern.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="regexType">The engine type.</param>
    /// <param name="optionsType">The options enum, when it resolves.</param>
    /// <param name="cache">The per-compilation validation cache.</param>
    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol regexType,
        INamedTypeSymbol? optionsType,
        ConcurrentDictionary<(string Pattern, int Options), string?> cache)
    {
        if (!NamesRegexApi(context.Node) || GetArgumentList(context.Node) is not { } arguments || !HasStringLiteral(arguments))
        {
            return;
        }

        if (!TryReadPatternAndOptions(context, arguments, regexType, optionsType, out var pattern, out var options, out var patternLocation))
        {
            return;
        }

        var message = GetError(pattern!, options, cache);
        if (message is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.InvalidRegexPattern, patternLocation!, message));
    }

    /// <summary>Reads the constant pattern and options a regex call binds, when both are constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="arguments">The call's argument list.</param>
    /// <param name="regexType">The engine type.</param>
    /// <param name="optionsType">The options enum, when it resolves.</param>
    /// <param name="pattern">The bound constant pattern.</param>
    /// <param name="options">The bound constant options, or zero when the call passes none.</param>
    /// <param name="patternLocation">The location of the pattern expression.</param>
    /// <returns><see langword="true"/> when the call binds to the engine with a constant pattern.</returns>
    private static bool TryReadPatternAndOptions(
        SyntaxNodeAnalysisContext context,
        ArgumentListSyntax arguments,
        INamedTypeSymbol regexType,
        INamedTypeSymbol? optionsType,
        out string? pattern,
        out int options,
        out Location? patternLocation)
    {
        pattern = null;
        options = 0;
        patternLocation = null;
        var boundToRegex = false;

        foreach (var argument in arguments.Arguments)
        {
            if (context.SemanticModel.GetOperation(argument, context.CancellationToken) is not IArgumentOperation { Parameter: { } parameter })
            {
                continue;
            }

            if (IsPatternParameter(parameter))
            {
                if (!TryGetConstantString(context, argument.Expression, out var patternValue))
                {
                    return false;
                }

                pattern = patternValue;
                patternLocation = argument.Expression.GetLocation();
                boundToRegex = SymbolEqualityComparer.Default.Equals(parameter.ContainingType, regexType);
            }
            else if (IsOptionsParameter(parameter, optionsType) && !TryReadOptions(context, argument.Expression, out options))
            {
                return false;
            }
        }

        return boundToRegex && pattern is not null;
    }

    /// <summary>Returns whether a parameter is the engine's string pattern parameter.</summary>
    /// <param name="parameter">The bound parameter.</param>
    /// <returns><see langword="true"/> when it is the pattern parameter.</returns>
    private static bool IsPatternParameter(IParameterSymbol parameter)
        => parameter.Name == PatternParameterName && parameter.Type.SpecialType == SpecialType.System_String;

    /// <summary>Returns whether a parameter is the engine's options parameter.</summary>
    /// <param name="parameter">The bound parameter.</param>
    /// <param name="optionsType">The options enum, when it resolves.</param>
    /// <returns><see langword="true"/> when it is the options parameter.</returns>
    private static bool IsOptionsParameter(IParameterSymbol parameter, INamedTypeSymbol? optionsType)
        => optionsType is not null && SymbolEqualityComparer.Default.Equals(parameter.Type, optionsType);

    /// <summary>Reads a constant string from an expression.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The argument expression.</param>
    /// <param name="value">The constant string when present.</param>
    /// <returns><see langword="true"/> when the expression is a constant string.</returns>
    private static bool TryGetConstantString(SyntaxNodeAnalysisContext context, ExpressionSyntax? expression, out string value)
    {
        if (expression is not null
            && context.SemanticModel.GetConstantValue(expression, context.CancellationToken) is { HasValue: true, Value: string constant })
        {
            value = constant;
            return true;
        }

        value = string.Empty;
        return false;
    }

    /// <summary>Reads a constant options value from an expression, failing when it is not constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The argument expression.</param>
    /// <param name="options">The constant options when present.</param>
    /// <returns><see langword="true"/> when the expression is a constant integer.</returns>
    private static bool TryReadOptions(SyntaxNodeAnalysisContext context, ExpressionSyntax? expression, out int options)
    {
        if (expression is not null
            && context.SemanticModel.GetConstantValue(expression, context.CancellationToken) is { HasValue: true, Value: int constant })
        {
            options = constant;
            return true;
        }

        options = 0;
        return false;
    }

    /// <summary>Validates a pattern by constructing the real engine, caching the result per compilation.</summary>
    /// <param name="pattern">The constant pattern.</param>
    /// <param name="options">The constant options.</param>
    /// <param name="cache">The per-compilation validation cache.</param>
    /// <returns>The engine's error message, or <see langword="null"/> when the pattern parses.</returns>
    private static string? GetError(string pattern, int options, ConcurrentDictionary<(string Pattern, int Options), string?> cache)
    {
        var effective = options & ~CompiledOption;
        if (!HostSupportsNonBacktracking)
        {
            effective &= ~NonBacktrackingOption;
        }

        return cache.GetOrAdd((pattern, effective), static key => Validate(key.Pattern, key.Options));
    }

    /// <summary>Constructs the engine once to learn whether a pattern parses.</summary>
    /// <param name="pattern">The constant pattern.</param>
    /// <param name="options">The effective options.</param>
    /// <returns>The engine's error message, or <see langword="null"/> when the pattern parses.</returns>
    private static string? Validate(string pattern, int options)
    {
        try
        {
            _ = new RegexEngine(pattern, (RegexEngineOptions)options, ValidationTimeout);
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            // The options combination is not one this host engine accepts; that is not a pattern defect.
            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
        catch (NotSupportedException ex)
        {
            // The non-backtracking engine rejects the pattern's constructs (a backreference or lookaround).
            return ex.Message;
        }
    }

    /// <summary>Probes whether the host engine accepts the non-backtracking option.</summary>
    /// <returns><see langword="true"/> when the option is accepted.</returns>
    private static bool ComputeNonBacktrackingSupport()
    {
        try
        {
            _ = new RegexEngine("a", (RegexEngineOptions)NonBacktrackingOption, ValidationTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Returns whether an invoked name is one of the engine's constant-pattern query methods.</summary>
    /// <param name="name">The invoked simple name.</param>
    /// <returns><see langword="true"/> when the name takes a pattern.</returns>
    private static bool IsQueryMethod(string? name) => name switch
    {
        "IsMatch" or "Match" or "Matches" or "Replace" or "Split" or "Count" or "EnumerateMatches" or "EnumerateSplits" => true,
        _ => false,
    };

    /// <summary>Returns whether an argument list carries a string literal worth binding for.</summary>
    /// <param name="arguments">The call's argument list.</param>
    /// <returns><see langword="true"/> when at least one argument is a string literal.</returns>
    private static bool HasStringLiteral(ArgumentListSyntax arguments)
    {
        foreach (var argument in arguments.Arguments)
        {
            if (argument.Expression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the invoked member's simple name text for the supported call shapes.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The invoked name, or <see langword="null"/> for unsupported expression shapes.</returns>
    private static string? GetInvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Gets the argument list of a regex call.</summary>
    /// <param name="node">The invocation or construction.</param>
    /// <returns>The argument list, or <see langword="null"/> when the call has none.</returns>
    private static ArgumentListSyntax? GetArgumentList(SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax invocation => invocation.ArgumentList,
        ObjectCreationExpressionSyntax creation => creation.ArgumentList,
        _ => null,
    };

    /// <summary>Returns the rightmost identifier of a written type name.</summary>
    /// <param name="type">The written type syntax.</param>
    /// <returns>The simple name, or <see langword="null"/> when the syntax names no simple type.</returns>
    private static string? GetSimpleName(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => GetSimpleName(alias.Name),
        _ => null,
    };
}
