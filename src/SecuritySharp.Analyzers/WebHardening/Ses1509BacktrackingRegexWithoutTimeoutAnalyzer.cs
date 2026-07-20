// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a backtracking-prone regular expression that is compiled or run with no time bound (SES1509).
/// The rule reports the <c>pattern</c> argument of <c>new System.Text.RegularExpressions.Regex(pattern, ...)</c>,
/// of the static <c>Regex.IsMatch</c>, <c>Regex.Match</c>, <c>Regex.Matches</c>, <c>Regex.Replace</c>,
/// <c>Regex.Split</c>, and <c>Regex.Count(input, pattern, ...)</c> overloads, and of a
/// <c>[GeneratedRegex(pattern, ...)]</c> attribute, when all of the following hold: no match timeout is supplied
/// (a <c>System.TimeSpan</c> argument on the call, or the <c>matchTimeoutMilliseconds</c> argument on the
/// attribute); <c>RegexOptions.NonBacktracking</c> is not among the options; and the pattern is a compile-time
/// constant string that is ReDoS-prone -- an unbounded quantifier (<c>*</c>, <c>+</c>, or <c>{n,}</c>) applied to
/// a group whose body itself repeats or offers a top-level alternation, as in <c>(a+)+</c>, <c>(a*)*</c>,
/// <c>([a-z]+)*</c>, <c>(.*)*</c>, <c>(a|aa)+</c>, or <c>(\d+)*</c>. A non-constant pattern is a separate injection
/// concern and is not reported here, so the two rules never overlap. The <c>Regex</c> type is probed once per
/// compilation and, when absent, nothing is registered. There is no code fix: adding a timeout or switching to the
/// non-backtracking engine is a semantic choice, so the rewrite is left to the author.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1509BacktrackingRegexWithoutTimeoutAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the regular-expression type whose pattern argument is guarded.</summary>
    private const string RegexMetadataName = "System.Text.RegularExpressions.Regex";

    /// <summary>The metadata name of the options enum whose <c>NonBacktracking</c> flag disables backtracking.</summary>
    private const string RegexOptionsMetadataName = "System.Text.RegularExpressions.RegexOptions";

    /// <summary>The metadata name of the source-generator attribute whose pattern argument is guarded.</summary>
    private const string GeneratedRegexAttributeMetadataName = "System.Text.RegularExpressions.GeneratedRegexAttribute";

    /// <summary>The metadata name of the type whose argument on a call supplies a match timeout.</summary>
    private const string TimeSpanMetadataName = "System.TimeSpan";

    /// <summary>The simple type name used to prefilter object-creation nodes syntactically.</summary>
    private const string RegexTypeName = "Regex";

    /// <summary>The name of the pattern parameter on every guarded constructor, overload, and attribute.</summary>
    private const string PatternParameterName = "pattern";

    /// <summary>The name of the options parameter on the guarded members.</summary>
    private const string OptionsParameterName = "options";

    /// <summary>The name of the attribute parameter that supplies a match timeout in milliseconds.</summary>
    private const string TimeoutMillisecondsParameterName = "matchTimeoutMilliseconds";

    /// <summary>The short attribute-name spelling used by the syntactic prefilter.</summary>
    private const string GeneratedRegexShortName = "GeneratedRegex";

    /// <summary>The long attribute-name spelling used by the syntactic prefilter.</summary>
    private const string GeneratedRegexLongName = "GeneratedRegexAttribute";

    /// <summary>The message label used for a constructor call.</summary>
    private const string ConstructorSink = "new Regex";

    /// <summary>The message label used for a <c>[GeneratedRegex]</c> application.</summary>
    private const string GeneratedRegexSink = "[GeneratedRegex]";

    /// <summary>The numeric value of <c>RegexOptions.NonBacktracking</c>, which selects the linear-time engine.</summary>
    private const int NonBacktrackingOption = 1024;

    /// <summary>The length of a regex escape sequence -- the backslash and the single character it escapes.</summary>
    private const int EscapeSequenceLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.BacktrackingRegexWithoutTimeout);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var regexType = start.Compilation.GetTypeByMetadataName(RegexMetadataName);
            var optionsType = start.Compilation.GetTypeByMetadataName(RegexOptionsMetadataName);
            var timeSpanType = start.Compilation.GetTypeByMetadataName(TimeSpanMetadataName);
            if (regexType is null || optionsType is null || timeSpanType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeObjectCreation(nodeContext, regexType, timeSpanType, optionsType), SyntaxKind.ObjectCreationExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, regexType, timeSpanType, optionsType), SyntaxKind.InvocationExpression);

            var generatedRegexAttributeType = start.Compilation.GetTypeByMetadataName(GeneratedRegexAttributeMetadataName);
            if (generatedRegexAttributeType is not null)
            {
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAttribute(nodeContext, generatedRegexAttributeType, timeSpanType, optionsType), SyntaxKind.Attribute);
            }
        });
    }

    /// <summary>Reports SES1509 for a <c>new Regex(pattern, ...)</c> whose constant pattern is ReDoS-prone and unbounded.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="regexType">The gated <c>Regex</c> type resolved for the compilation.</param>
    /// <param name="timeSpanType">The gated <c>TimeSpan</c> type used to detect a match-timeout argument.</param>
    /// <param name="optionsType">The gated <c>RegexOptions</c> type used to locate the options argument.</param>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol regexType, INamedTypeSymbol timeSpanType, INamedTypeSymbol optionsType)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // Syntactic prefilter: a 'new <...>.Regex(...)' with at least one argument.
        if (objectCreation.ArgumentList is not { Arguments.Count: > 0 } argumentList
            || !IsRegexTypeName(objectCreation.Type))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(objectCreation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, regexType))
        {
            return;
        }

        ReportWhenVulnerable(
            context,
            constructor,
            timeSpanType,
            GetArgumentForParameter(argumentList.Arguments, GetPatternOrdinal(constructor), PatternParameterName),
            GetArgumentForParameter(argumentList.Arguments, GetOptionsOrdinal(constructor, optionsType), OptionsParameterName),
            ConstructorSink);
    }

    /// <summary>Reports SES1509 for a static <c>Regex</c> call whose constant pattern is ReDoS-prone and unbounded.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="regexType">The gated <c>Regex</c> type resolved for the compilation.</param>
    /// <param name="timeSpanType">The gated <c>TimeSpan</c> type used to detect a match-timeout argument.</param>
    /// <param name="optionsType">The gated <c>RegexOptions</c> type used to locate the options argument.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol regexType, INamedTypeSymbol timeSpanType, INamedTypeSymbol optionsType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member call to one of the guarded static methods carrying at least the
        // input and pattern arguments. The static overloads always take '(input, pattern, ...)'.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: { } methodName }
            || !IsGuardedStaticMethodName(methodName)
            || invocation.ArgumentList.Arguments.Count < 2)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || !IsGuardedStaticMethodName(method.Name)
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, regexType))
        {
            return;
        }

        ReportWhenVulnerable(
            context,
            method,
            timeSpanType,
            GetArgumentForParameter(invocation.ArgumentList.Arguments, GetPatternOrdinal(method), PatternParameterName),
            GetArgumentForParameter(invocation.ArgumentList.Arguments, GetOptionsOrdinal(method, optionsType), OptionsParameterName),
            "Regex." + method.Name);
    }

    /// <summary>Reports SES1509 for a <c>[GeneratedRegex(pattern, ...)]</c> whose constant pattern is ReDoS-prone.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attributeType">The gated <c>GeneratedRegexAttribute</c> type resolved for the compilation.</param>
    /// <param name="timeSpanType">The gated <c>TimeSpan</c> type used to detect a match-timeout argument.</param>
    /// <param name="optionsType">The gated <c>RegexOptions</c> type used to locate the options argument.</param>
    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, INamedTypeSymbol attributeType, INamedTypeSymbol timeSpanType, INamedTypeSymbol optionsType)
    {
        var attribute = (AttributeSyntax)context.Node;

        // Syntactic prefilter: the attribute is spelled 'GeneratedRegex' or 'GeneratedRegexAttribute' and carries a pattern.
        if (attribute.ArgumentList is not { Arguments.Count: > 0 } argumentList
            || GetAttributeSimpleName(attribute.Name) is not (GeneratedRegexShortName or GeneratedRegexLongName))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, attributeType))
        {
            return;
        }

        ReportWhenVulnerable(
            context,
            constructor,
            timeSpanType,
            GetAttributeArgumentForParameter(argumentList.Arguments, GetPatternOrdinal(constructor), PatternParameterName),
            GetAttributeArgumentForParameter(argumentList.Arguments, GetOptionsOrdinal(constructor, optionsType), OptionsParameterName),
            GeneratedRegexSink);
    }

    /// <summary>Reports SES1509 when the pattern is an unguarded, ReDoS-prone constant string.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="method">The bound constructor or static method.</param>
    /// <param name="timeSpanType">The gated <c>TimeSpan</c> type used to detect a match-timeout argument.</param>
    /// <param name="patternExpression">The pattern argument expression, or <see langword="null"/> when absent.</param>
    /// <param name="optionsExpression">The options argument expression, or <see langword="null"/> when absent.</param>
    /// <param name="sink">The message label identifying the call.</param>
    private static void ReportWhenVulnerable(
        SyntaxNodeAnalysisContext context,
        IMethodSymbol method,
        INamedTypeSymbol timeSpanType,
        ExpressionSyntax? patternExpression,
        ExpressionSyntax? optionsExpression,
        string sink)
    {
        // A supplied timeout or the non-backtracking engine bounds the cost, so the shape is safe. The pattern
        // scan runs last because it is the most expensive of the three checks.
        if (patternExpression is null
            || SpecifiesTimeout(method, timeSpanType)
            || !OptionsAllowBacktracking(context.SemanticModel, optionsExpression, context.CancellationToken)
            || !IsConstantBacktrackingPronePattern(context.SemanticModel, patternExpression, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.BacktrackingRegexWithoutTimeout,
            patternExpression.SyntaxTree,
            patternExpression.Span,
            sink));
    }

    /// <summary>Returns whether a bound member supplies a match timeout, bounding the match cost.</summary>
    /// <param name="method">The bound constructor or static method.</param>
    /// <param name="timeSpanType">The gated <c>TimeSpan</c> type resolved for the compilation.</param>
    /// <returns><see langword="true"/> when a timeout parameter is present on the bound overload.</returns>
    private static bool SpecifiesTimeout(IMethodSymbol method, INamedTypeSymbol timeSpanType)
    {
        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Name == TimeoutMillisecondsParameterName
                || SymbolEqualityComparer.Default.Equals(parameters[i].Type, timeSpanType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the options provably leave the backtracking engine in use.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="optionsExpression">The options argument expression, or <see langword="null"/> when absent.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when no options are supplied, or a constant options value lacks <c>NonBacktracking</c>.</returns>
    private static bool OptionsAllowBacktracking(SemanticModel model, ExpressionSyntax? optionsExpression, CancellationToken cancellationToken)
    {
        if (optionsExpression is null)
        {
            return true;
        }

        // A non-constant options value cannot be proven free of NonBacktracking, so stay silent to avoid a false positive.
        return model.GetConstantValue(optionsExpression, cancellationToken) is { HasValue: true, Value: int options }
            && (options & NonBacktrackingOption) == 0;
    }

    /// <summary>Returns whether a pattern argument is a compile-time-constant, ReDoS-prone string.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="patternExpression">The pattern argument expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the constant pattern nests or overlaps quantifiers.</returns>
    private static bool IsConstantBacktrackingPronePattern(SemanticModel model, ExpressionSyntax patternExpression, CancellationToken cancellationToken)
        => model.GetConstantValue(patternExpression, cancellationToken) is { HasValue: true, Value: string pattern }
            && HasNestedOrOverlappingQuantifier(pattern);

    /// <summary>Returns the argument expression bound to a parameter, honouring an explicit name.</summary>
    /// <param name="arguments">The call's argument list.</param>
    /// <param name="ordinal">The zero-based ordinal of the target parameter, or -1 when it is absent.</param>
    /// <param name="parameterName">The target parameter name.</param>
    /// <returns>The argument expression, or <see langword="null"/> when it cannot be identified.</returns>
    private static ExpressionSyntax? GetArgumentForParameter(SeparatedSyntaxList<ArgumentSyntax> arguments, int ordinal, string parameterName)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon?.Name.Identifier.ValueText == parameterName)
            {
                return arguments[i].Expression;
            }
        }

        return ordinal >= 0 && ordinal < arguments.Count && arguments[ordinal].NameColon is null
            ? arguments[ordinal].Expression
            : null;
    }

    /// <summary>Returns the attribute argument expression bound to a parameter, honouring an explicit name.</summary>
    /// <param name="arguments">The attribute's argument list.</param>
    /// <param name="ordinal">The zero-based ordinal of the target parameter, or -1 when it is absent.</param>
    /// <param name="parameterName">The target parameter name.</param>
    /// <returns>The argument expression, or <see langword="null"/> when it cannot be identified.</returns>
    private static ExpressionSyntax? GetAttributeArgumentForParameter(SeparatedSyntaxList<AttributeArgumentSyntax> arguments, int ordinal, string parameterName)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon?.Name.Identifier.ValueText == parameterName)
            {
                return arguments[i].Expression;
            }
        }

        return ordinal >= 0 && ordinal < arguments.Count && arguments[ordinal].NameColon is null && arguments[ordinal].NameEquals is null
            ? arguments[ordinal].Expression
            : null;
    }

    /// <summary>Returns the ordinal of a method's <c>pattern</c> parameter.</summary>
    /// <param name="method">The bound constructor or static method.</param>
    /// <returns>The zero-based ordinal, or -1 when there is no <c>pattern</c> parameter.</returns>
    private static int GetPatternOrdinal(IMethodSymbol method)
    {
        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Name == PatternParameterName)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns the ordinal of a method's <c>RegexOptions</c> parameter.</summary>
    /// <param name="method">The bound constructor or static method.</param>
    /// <param name="optionsType">The gated <c>RegexOptions</c> type resolved for the compilation.</param>
    /// <returns>The zero-based ordinal, or -1 when there is no options parameter.</returns>
    private static int GetOptionsOrdinal(IMethodSymbol method, INamedTypeSymbol optionsType)
    {
        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(parameters[i].Type, optionsType))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns whether an object-creation type names the <c>Regex</c> type.</summary>
    /// <param name="type">The created type syntax.</param>
    /// <returns><see langword="true"/> when the right-most name is <c>Regex</c>.</returns>
    private static bool IsRegexTypeName(TypeSyntax type)
        => type switch
        {
            IdentifierNameSyntax { Identifier.ValueText: RegexTypeName } => true,
            QualifiedNameSyntax { Right.Identifier.ValueText: RegexTypeName } => true,
            _ => false,
        };

    /// <summary>Returns the simple identifier text of an attribute name, ignoring any qualifier or alias.</summary>
    /// <param name="name">The attribute's name syntax.</param>
    /// <returns>The rightmost simple name, or <see langword="null"/> when it cannot be read syntactically.</returns>
    private static string? GetAttributeSimpleName(NameSyntax name)
        => name switch
        {
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns whether a name is one of the guarded static <c>Regex</c> methods.</summary>
    /// <param name="name">The candidate method name.</param>
    /// <returns><see langword="true"/> for <c>IsMatch</c>, <c>Match</c>, <c>Matches</c>, <c>Replace</c>, <c>Split</c>, or <c>Count</c>.</returns>
    private static bool IsGuardedStaticMethodName(string name)
        => name switch
        {
            "IsMatch" or "Match" or "Matches" or "Replace" or "Split" or "Count" => true,
            _ => false,
        };

    /// <summary>
    /// Returns whether a regex pattern nests or overlaps quantifiers: an unbounded quantifier
    /// (<c>*</c>, <c>+</c>, or <c>{n,}</c>) applied to a group whose body itself repeats or offers a
    /// top-level alternation. Every group start is examined, so a nested culprit is found too. Escapes
    /// and character classes are skipped so their literal metacharacters never register as structure.
    /// </summary>
    /// <param name="pattern">The constant pattern text.</param>
    /// <returns><see langword="true"/> when the pattern carries a nested or overlapping unbounded quantifier.</returns>
    private static bool HasNestedOrOverlappingQuantifier(string pattern)
    {
        var length = pattern.Length;
        var i = 0;
        while (i < length)
        {
            var c = pattern[i];
            if (c == '\\')
            {
                i += EscapeSequenceLength;
                continue;
            }

            if (c == '[')
            {
                i = SkipCharacterClass(pattern, i);
                continue;
            }

            if (c == '(')
            {
                var close = FindGroupClose(pattern, i);
                if (close < 0)
                {
                    return false; // unbalanced parentheses would fail to compile at runtime
                }

                if (IsUnboundedQuantifier(pattern, close + 1)
                    && BodyRepeatsOrAlternates(pattern, i + 1, close))
                {
                    return true;
                }

                i++; // step into the group so a nested group gets its own examination
                continue;
            }

            i++;
        }

        return false;
    }

    /// <summary>Returns the index of the closing parenthesis matching the group opened at <paramref name="open"/>.</summary>
    /// <param name="pattern">The pattern text.</param>
    /// <param name="open">The index of the group's opening parenthesis.</param>
    /// <returns>The index of the matching close, or -1 when the group is unbalanced.</returns>
    private static int FindGroupClose(string pattern, int open)
    {
        var length = pattern.Length;
        var depth = 0;
        var j = open;
        while (j < length)
        {
            var c = pattern[j];
            if (c == '\\')
            {
                j += EscapeSequenceLength;
                continue;
            }

            if (c == '[')
            {
                j = SkipCharacterClass(pattern, j);
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return j;
                }
            }

            j++;
        }

        return -1;
    }

    /// <summary>Returns the index just past a character class opened at <paramref name="open"/>.</summary>
    /// <param name="pattern">The pattern text.</param>
    /// <param name="open">The index of the class's opening bracket.</param>
    /// <returns>The index after the closing bracket, or the pattern length when the class is unterminated.</returns>
    private static int SkipCharacterClass(string pattern, int open)
    {
        var length = pattern.Length;
        var j = open + 1;
        if (j < length && pattern[j] == '^')
        {
            j++;
        }

        if (j < length && pattern[j] == ']')
        {
            j++; // a ']' immediately after '[' or '[^' is a literal member, not the terminator
        }

        while (j < length)
        {
            var c = pattern[j];
            if (c == '\\')
            {
                j += EscapeSequenceLength;
                continue;
            }

            if (c == ']')
            {
                return j + 1;
            }

            j++;
        }

        return length;
    }

    /// <summary>Returns whether the character at <paramref name="index"/> begins an unbounded quantifier.</summary>
    /// <param name="pattern">The pattern text.</param>
    /// <param name="index">The index immediately following a group's closing parenthesis.</param>
    /// <returns><see langword="true"/> for <c>*</c>, <c>+</c>, or an open-ended <c>{n,}</c>.</returns>
    private static bool IsUnboundedQuantifier(string pattern, int index)
    {
        if (index >= pattern.Length)
        {
            return false;
        }

        var c = pattern[index];
        return c == '*' || c == '+' || (c == '{' && IsOpenEndedBrace(pattern, index));
    }

    /// <summary>Returns whether a brace quantifier at <paramref name="index"/> has no upper bound (<c>{n,}</c>).</summary>
    /// <param name="pattern">The pattern text.</param>
    /// <param name="index">The index of the opening brace.</param>
    /// <returns><see langword="true"/> for <c>{n,}</c>; <see langword="false"/> for <c>{n}</c>, <c>{n,m}</c>, or a literal brace.</returns>
    private static bool IsOpenEndedBrace(string pattern, int index)
    {
        var length = pattern.Length;
        var j = index + 1;
        var digitStart = j;
        while (j < length && pattern[j] >= '0' && pattern[j] <= '9')
        {
            j++;
        }

        if (j == digitStart || j >= length || pattern[j] != ',')
        {
            return false; // '{n}' exact, or not a real quantifier
        }

        j++; // skip ','
        return j < length && pattern[j] == '}'; // '{n,}' unbounded; '{n,m}' is bounded
    }

    /// <summary>Returns whether a group body repeats an atom or offers a top-level alternation.</summary>
    /// <param name="pattern">The pattern text.</param>
    /// <param name="start">The inclusive start index of the group body.</param>
    /// <param name="end">The exclusive end index of the group body (the closing parenthesis).</param>
    /// <returns><see langword="true"/> when the body carries a quantifier or a depth-zero <c>|</c>.</returns>
    private static bool BodyRepeatsOrAlternates(string pattern, int start, int end)
    {
        var depth = 0;
        var j = start;
        while (j < end)
        {
            var c = pattern[j];
            if (c == '\\')
            {
                j += EscapeSequenceLength;
                continue;
            }

            if (c == '[')
            {
                j = SkipCharacterClass(pattern, j);
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth -= depth > 0 ? 1 : 0;
            }
            else if (RepeatsOrTopLevelAlternates(pattern, j, c, depth))
            {
                return true;
            }

            j++;
        }

        return false;
    }

    /// <summary>Returns whether a body character is a quantifier or a depth-zero alternation.</summary>
    /// <param name="pattern">The pattern text.</param>
    /// <param name="index">The index of the character under inspection.</param>
    /// <param name="c">The character under inspection.</param>
    /// <param name="depth">The parenthesis nesting depth at <paramref name="index"/>.</param>
    /// <returns><see langword="true"/> for <c>*</c>, <c>+</c>, an open-ended <c>{n,}</c>, or a top-level <c>|</c>.</returns>
    private static bool RepeatsOrTopLevelAlternates(string pattern, int index, char c, int depth)
        => c == '*'
            || c == '+'
            || (c == '{' && IsOpenEndedBrace(pattern, index))
            || (c == '|' && depth == 0);
}
