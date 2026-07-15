// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports structured-logging call defects that share one bound call, one parsed template, and one set of
/// arguments, so the template is read once per call rather than once per rule:
/// <list type="bullet">
/// <item>SST2438 — an error or critical log in a catch that throws the caught exception away;</item>
/// <item>SST2439 — an exception passed as a message value instead of the exception argument;</item>
/// <item>SST2440 — two message values ordered against the placeholders they are named after;</item>
/// <item>SST2441 — a placeholder with no valid property name;</item>
/// <item>SST2442 — a placeholder name repeated within one template.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// The whole family is gated on the logging extension type resolving in the compilation, so a project that
/// does not log registers no syntax action and pays a single type lookup. Each call is filtered syntactically
/// before it is bound — the invoked member must start with <c>Log</c> or be <c>BeginScope</c>, and one of its
/// arguments must be a string literal — so a call with a non-constant template, which is a separate concern,
/// is never bound here.
/// </para>
/// <para>
/// The template is parsed in one index scan with no allocation on the clean path; the placeholder array is
/// sized to the count and filled only when the template holds at least one placeholder.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LoggerCallAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The property carrying the caught exception's name for the SST2438 fix.</summary>
    internal const string ExceptionNameKey = "ExceptionName";

    /// <summary>The property carrying the position the exception argument is inserted at for the SST2438 fix.</summary>
    internal const string InsertIndexKey = "InsertIndex";

    /// <summary>The property carrying the degraded projection's argument index for the SST2438 fix, or -1.</summary>
    internal const string DegradedArgumentKey = "DegradedArgument";

    /// <summary>The property carrying the first tail argument position for the SST2438 fix.</summary>
    internal const string TailStartKey = "TailStart";

    /// <summary>The property carrying the swap partner's position for the SST2440 fix.</summary>
    internal const string SwapWithKey = "SwapWith";

    /// <summary>The metadata name of the logging abstraction that gates the whole family.</summary>
    private const string LoggerMetadataName = "Microsoft.Extensions.Logging.ILogger";

    /// <summary>The metadata name of the logging extension type whose members these calls resolve to.</summary>
    private const string LoggerExtensionsMetadataName = "Microsoft.Extensions.Logging.LoggerExtensions";

    /// <summary>The metadata name of the exception base type.</summary>
    private const string ExceptionMetadataName = "System.Exception";

    /// <summary>The fewest tail values a transposition needs.</summary>
    private const int MinimumTransposableValues = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        CorrectnessRules.ExceptionDiscardedInCatch,
        CorrectnessRules.ExceptionAsTemplateArgument,
        CorrectnessRules.TransposedTemplateArguments,
        CorrectnessRules.MalformedPlaceholder,
        CorrectnessRules.DuplicatePlaceholder);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Resolves the logging types once and, only when they are present, registers the call action.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var logger = context.Compilation.GetTypeByMetadataName(LoggerMetadataName);
        var loggerExtensions = context.Compilation.GetTypeByMetadataName(LoggerExtensionsMetadataName);
        var exceptionType = context.Compilation.GetTypeByMetadataName(ExceptionMetadataName);
        if (logger is null || loggerExtensions is null || exceptionType is null)
        {
            return;
        }

        var state = new LoggingState(loggerExtensions, exceptionType);
        var floors = new ConcurrentDictionary<SyntaxTree, LogLevelFloorOptions>();
        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, state, floors), SyntaxKind.InvocationExpression);
    }

    /// <summary>Analyzes one call, if it is a logging call with a literal template.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="state">The resolved logging symbols.</param>
    /// <param name="floors">The per-tree SST2438 level floor cache.</param>
    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        LoggingState state,
        ConcurrentDictionary<SyntaxTree, LogLevelFloorOptions> floors)
    {
        if (!TryBind(context, state, out var call))
        {
            return;
        }

        ReportMalformedPlaceholders(context, call);
        ReportDuplicatePlaceholders(context, call);
        ReportExceptionAsValue(context, call, state);
        ReportTransposedArguments(context, call);
        ReportDiscardedException(context, call, state, floors);
    }

    /// <summary>Filters a call syntactically, then binds it and describes it as a logging call.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="state">The resolved logging symbols.</param>
    /// <param name="call">The described call, when the node is a logging call with a literal template.</param>
    /// <returns><see langword="true"/> when the node is a logging call worth analyzing.</returns>
    private static bool TryBind(SyntaxNodeAnalysisContext context, LoggingState state, out LogCall call)
    {
        call = null!;
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (GetInvokedName(invocation) is not { } nameToken || !IsLoggingName(nameToken.ValueText))
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (!HasStringLiteralArgument(arguments))
        {
            return false;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, state.LoggerExtensions)
            || !TryResolveTemplate(method, arguments, out var templateIndex, out var paramsIndex, out var literal))
        {
            return false;
        }

        var text = literal.Token.ValueText;
        call = new LogCall
        {
            Invocation = invocation,
            NameToken = nameToken,
            MethodName = method.Name,
            Arguments = arguments,
            TemplateIndex = templateIndex,
            ParamsIndex = paramsIndex,
            ExceptionIndex = FindExceptionParameter(method.Parameters, templateIndex, state.ExceptionType),
            Literal = literal,
            Text = text,
            Placeholders = LogMessageTemplate.Parse(text),
        };
        return true;
    }

    /// <summary>Locates the string-literal template argument and the params tail of a logging method.</summary>
    /// <param name="method">The bound logging method.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="templateIndex">The template argument's position.</param>
    /// <param name="paramsIndex">The first tail argument's position.</param>
    /// <param name="literal">The template literal.</param>
    /// <returns><see langword="true"/> when the method has a literal template before a params tail.</returns>
    private static bool TryResolveTemplate(
        IMethodSymbol method,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        out int templateIndex,
        out int paramsIndex,
        out LiteralExpressionSyntax literal)
    {
        templateIndex = -1;
        paramsIndex = -1;
        literal = null!;
        var parameters = method.Parameters;
        if (parameters.Length == 0 || !parameters[parameters.Length - 1].IsParams)
        {
            return false;
        }

        paramsIndex = parameters.Length - 1;
        templateIndex = paramsIndex - 1;
        if (templateIndex < 0
            || parameters[templateIndex].Type.SpecialType != SpecialType.System_String
            || templateIndex >= arguments.Count
            || arguments[templateIndex].Expression is not LiteralExpressionSyntax candidate
            || !candidate.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return false;
        }

        literal = candidate;
        return true;
    }

    /// <summary>Reports each placeholder that carries no usable property name (SST2441).</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="call">The described call.</param>
    private static void ReportMalformedPlaceholders(SyntaxNodeAnalysisContext context, LogCall call)
    {
        var placeholders = call.Placeholders;
        for (var i = 0; i < placeholders.Length; i++)
        {
            if (placeholders[i].Kind != LogPlaceholderKind.Malformed)
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.MalformedPlaceholder,
                PlaceholderLocation(call.Literal, placeholders[i]),
                LogMessageTemplate.GetText(call.Text, placeholders[i])));
        }
    }

    /// <summary>Reports each placeholder name that repeats an earlier one (SST2442).</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="call">The described call.</param>
    private static void ReportDuplicatePlaceholders(SyntaxNodeAnalysisContext context, LogCall call)
    {
        var placeholders = call.Placeholders;
        for (var i = 0; i < placeholders.Length; i++)
        {
            if (placeholders[i].Kind != LogPlaceholderKind.Named || !HasEarlierMatch(call.Text, placeholders, i))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.DuplicatePlaceholder,
                PlaceholderLocation(call.Literal, placeholders[i]),
                LogMessageTemplate.GetName(call.Text, placeholders[i])));
        }
    }

    /// <summary>Reports a tail argument whose type is an exception (SST2439).</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="call">The described call.</param>
    /// <param name="state">The resolved logging symbols.</param>
    private static void ReportExceptionAsValue(SyntaxNodeAnalysisContext context, LogCall call, LoggingState state)
    {
        if (!state.HasExceptionOverload(call.MethodName))
        {
            return;
        }

        var arguments = call.Arguments;
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(InsertIndexKey, Format(call.TemplateIndex))
            .Add(TailStartKey, Format(call.ParamsIndex));
        for (var i = call.ParamsIndex; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (!DerivesFromException(context.SemanticModel.GetTypeInfo(argument.Expression, context.CancellationToken).Type, state.ExceptionType))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.ExceptionAsTemplateArgument,
                argument.SyntaxTree,
                argument.Span,
                properties,
                argument.ToString()));
        }
    }

    /// <summary>Reports a two-way swap between tail arguments named after the placeholders (SST2440).</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="call">The described call.</param>
    private static void ReportTransposedArguments(SyntaxNodeAnalysisContext context, LogCall call)
    {
        var arguments = call.Arguments;
        var placeholders = call.Placeholders;
        var tailCount = arguments.Count - call.ParamsIndex;
        if (tailCount < MinimumTransposableValues || placeholders.Length != tailCount || !AllNamedAndDistinct(call.Text, placeholders))
        {
            return;
        }

        for (var i = 0; i < tailCount; i++)
        {
            var partner = FindSwapPartner(call, i);
            if (partner <= i)
            {
                continue;
            }

            var argument = arguments[call.ParamsIndex + i];
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add(SwapWithKey, (call.ParamsIndex + partner).ToString(System.Globalization.CultureInfo.InvariantCulture));
            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.TransposedTemplateArguments,
                argument.SyntaxTree,
                argument.Span,
                properties,
                argument.ToString(),
                LogMessageTemplate.GetName(call.Text, placeholders[i])));
        }
    }

    /// <summary>Finds the tail position whose value and placeholder mirror this one's, forming a two-cycle.</summary>
    /// <param name="call">The described call.</param>
    /// <param name="index">The tail position being examined.</param>
    /// <returns>The partner position, or <c>-1</c> when this value is in the right slot.</returns>
    private static int FindSwapPartner(LogCall call, int index)
    {
        var placeholders = call.Placeholders;
        if (GetValueName(call.Arguments[call.ParamsIndex + index].Expression) is not { } name
            || LogMessageTemplate.NameEquals(call.Text, placeholders[index], name))
        {
            return -1;
        }

        var partner = IndexOfPlaceholder(call.Text, placeholders, name);
        if (partner < 0 || partner == index)
        {
            return -1;
        }

        return GetValueName(call.Arguments[call.ParamsIndex + partner].Expression) is { } partnerName
            && LogMessageTemplate.NameEquals(call.Text, placeholders[index], partnerName)
                ? partner
                : -1;
    }

    /// <summary>Reports a caught exception a catch's error log discards (SST2438).</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="call">The described call.</param>
    /// <param name="state">The resolved logging symbols.</param>
    /// <param name="floors">The per-tree level floor cache.</param>
    private static void ReportDiscardedException(
        SyntaxNodeAnalysisContext context,
        LogCall call,
        LoggingState state,
        ConcurrentDictionary<SyntaxTree, LogLevelFloorOptions> floors)
    {
        var level = LevelOf(context, call.MethodName, call.Arguments);
        if (level < 0 || !GetFloor(context, floors).Includes(level) || !state.HasExceptionOverload(call.MethodName))
        {
            return;
        }

        if (!TryGetNamedCatch(context, call.Invocation, out var local, out var catchClause)
            || !IsExceptionDiscarded(context, call, local, catchClause, out var degradedArgument))
        {
            return;
        }

        var nameToken = call.NameToken;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.ExceptionDiscardedInCatch,
            nameToken.SyntaxTree!,
            nameToken.Span,
            BuildDiscardProperties(call, local.Name, degradedArgument),
            local.Name));
    }

    /// <summary>Finds the caught local of a catch a call sits inside, when the catch names it and does not rethrow.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="invocation">The logging call.</param>
    /// <param name="local">The caught exception local.</param>
    /// <param name="catchClause">The enclosing catch.</param>
    /// <returns><see langword="true"/> when a named, non-rethrowing catch encloses the call.</returns>
    private static bool TryGetNamedCatch(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        out ISymbol local,
        out CatchClauseSyntax catchClause)
    {
        local = null!;
        catchClause = null!;
        var found = FindEnclosingCatch(invocation);
        if (found?.Declaration is not { Identifier.ValueText.Length: > 0 } declaration
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } symbol
            || ContainsThrow(found.Block))
        {
            return false;
        }

        local = symbol;
        catchClause = found;
        return true;
    }

    /// <summary>Returns whether a catch's log call discards the caught exception rather than passing it.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="call">The described call.</param>
    /// <param name="local">The caught exception local.</param>
    /// <param name="catchClause">The enclosing catch.</param>
    /// <param name="degradedArgument">The degraded projection's argument position, or -1.</param>
    /// <returns><see langword="true"/> when the exception is discarded and should be reported.</returns>
    private static bool IsExceptionDiscarded(
        SyntaxNodeAnalysisContext context,
        LogCall call,
        ISymbol local,
        CatchClauseSyntax catchClause,
        out int degradedArgument)
    {
        degradedArgument = -1;
        if (call.ExceptionIndex >= 0 && call.ExceptionIndex < call.Arguments.Count
            && ExpressionReferencesLocal(context, call.Arguments[call.ExceptionIndex].Expression, local))
        {
            return false;
        }

        return TryClassifyDiscard(context, call, local, catchClause, out degradedArgument);
    }

    /// <summary>Classifies how a catch's log call relates to the caught exception, deciding whether it is discarded.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="call">The described call.</param>
    /// <param name="local">The caught exception local.</param>
    /// <param name="catchClause">The enclosing catch.</param>
    /// <param name="degradedArgument">The degraded projection's argument position, or -1.</param>
    /// <returns><see langword="true"/> when the exception is discarded and should be reported.</returns>
    private static bool TryClassifyDiscard(
        SyntaxNodeAnalysisContext context,
        LogCall call,
        ISymbol local,
        CatchClauseSyntax catchClause,
        out int degradedArgument)
    {
        degradedArgument = -1;
        var arguments = call.Arguments;
        for (var i = call.ParamsIndex; i < arguments.Count; i++)
        {
            var expression = arguments[i].Expression;
            if (expression is IdentifierNameSyntax bare && ReferencesLocal(context, bare, local))
            {
                return false;
            }

            if (degradedArgument < 0 && ExpressionReferencesLocal(context, expression, local))
            {
                degradedArgument = i;
            }
        }

        return degradedArgument >= 0 || !ReferencedInBlock(context, catchClause, local);
    }

    /// <summary>Builds the property bag the SST2438 fix reads.</summary>
    /// <param name="call">The described call.</param>
    /// <param name="exceptionName">The caught exception's name.</param>
    /// <param name="degradedArgument">The degraded projection's argument position, or -1.</param>
    /// <returns>The diagnostic properties.</returns>
    private static ImmutableDictionary<string, string?> BuildDiscardProperties(LogCall call, string exceptionName, int degradedArgument)
    {
        var placeholderIndex = degradedArgument < 0 ? -1 : degradedArgument - call.ParamsIndex;
        var degradedForFix = degradedArgument >= 0 && placeholderIndex >= 0 && placeholderIndex < call.Placeholders.Length
            ? degradedArgument
            : -1;
        return ImmutableDictionary<string, string?>.Empty
            .Add(ExceptionNameKey, exceptionName)
            .Add(InsertIndexKey, Format(call.TemplateIndex))
            .Add(TailStartKey, Format(call.ParamsIndex))
            .Add(DegradedArgumentKey, Format(degradedForFix));
    }

    /// <summary>Formats an integer for a diagnostic property.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The invariant text.</returns>
    private static string Format(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Returns the level a call logs at, or -1 when it is not a levelled call or the level is not constant.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="methodName">The logging method's name.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <returns>The level ordinal, or -1.</returns>
    private static int LevelOf(SyntaxNodeAnalysisContext context, string methodName, SeparatedSyntaxList<ArgumentSyntax> arguments)
        => methodName switch
        {
            "LogTrace" => LogLevelFloorOptions.Trace,
            "LogDebug" => LogLevelFloorOptions.Debug,
            "LogInformation" => LogLevelFloorOptions.Information,
            "LogWarning" => LogLevelFloorOptions.Warning,
            "LogError" => LogLevelFloorOptions.Error,
            "LogCritical" => LogLevelFloorOptions.Critical,
            "Log" => ConstantLevel(context, arguments),
            _ => -1,
        };

    /// <summary>Reads the constant level argument of a general <c>Log</c> call.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <returns>The level ordinal, or -1 when no argument is a constant level.</returns>
    private static int ConstantLevel(SyntaxNodeAnalysisContext context, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (context.SemanticModel.GetConstantValue(arguments[i].Expression, context.CancellationToken) is { HasValue: true, Value: int value })
            {
                return value;
            }
        }

        return -1;
    }

    /// <summary>Reads the SST2438 level floor for a call's tree, parsing each tree at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="floors">The per-tree level floor cache.</param>
    /// <returns>The resolved floor.</returns>
    private static LogLevelFloorOptions GetFloor(SyntaxNodeAnalysisContext context, ConcurrentDictionary<SyntaxTree, LogLevelFloorOptions> floors)
    {
        var tree = context.Node.SyntaxTree;
        if (floors.TryGetValue(tree, out var floor))
        {
            return floor;
        }

        floor = LogLevelFloorOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        floors.TryAdd(tree, floor);
        return floor;
    }

    /// <summary>Finds the catch clause a call sits directly inside, stopping at a function boundary.</summary>
    /// <param name="invocation">The logging call.</param>
    /// <returns>The enclosing catch clause, or <see langword="null"/>.</returns>
    private static CatchClauseSyntax? FindEnclosingCatch(InvocationExpressionSyntax invocation)
    {
        for (var node = invocation.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case CatchClauseSyntax catchClause:
                    return catchClause;
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case MemberDeclarationSyntax:
                    return null;
                default:
                    continue;
            }
        }

        return null;
    }

    /// <summary>Returns whether a block throws anywhere within it.</summary>
    /// <param name="block">The catch block.</param>
    /// <returns><see langword="true"/> when a throw is present.</returns>
    private static bool ContainsThrow(BlockSyntax? block)
    {
        if (block is null)
        {
            return false;
        }

        var found = false;
        DescendantTraversalHelper.VisitDescendantTokens(block, ref found, VisitThrow);
        return found;
    }

    /// <summary>Records whether a token is a throw keyword.</summary>
    /// <param name="token">The token.</param>
    /// <param name="found">Whether a throw has been seen.</param>
    /// <returns><see langword="false"/> to stop once a throw is found.</returns>
    private static bool VisitThrow(in SyntaxToken token, ref bool found)
    {
        if (!token.IsKind(SyntaxKind.ThrowKeyword))
        {
            return true;
        }

        found = true;
        return false;
    }

    /// <summary>Returns whether a caught local is referenced anywhere in the catch block.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="catchClause">The enclosing catch.</param>
    /// <param name="local">The caught exception local.</param>
    /// <returns><see langword="true"/> when the local is referenced.</returns>
    private static bool ReferencedInBlock(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause, ISymbol local)
        => catchClause.Block is { } block && ExpressionReferencesLocal(context, block, local);

    /// <summary>Returns whether any identifier in a node binds to a local.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="node">The node to scan.</param>
    /// <param name="local">The local symbol.</param>
    /// <returns><see langword="true"/> when the local is referenced.</returns>
    private static bool ExpressionReferencesLocal(SyntaxNodeAnalysisContext context, SyntaxNode node, ISymbol local)
    {
        if (node is IdentifierNameSyntax identifier)
        {
            return ReferencesLocal(context, identifier, local);
        }

        var scan = new ReferenceScan(context.SemanticModel, local, local.Name, context.CancellationToken);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, ReferenceScan>(node, ref scan, VisitIdentifier);
        return scan.Found;
    }

    /// <summary>Visits one identifier, recording whether it binds to the scanned local.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/> to stop once the local is found.</returns>
    private static bool VisitIdentifier(IdentifierNameSyntax identifier, ref ReferenceScan scan)
    {
        if (identifier.Identifier.ValueText != scan.Name
            || !SymbolEqualityComparer.Default.Equals(scan.Model.GetSymbolInfo(identifier, scan.CancellationToken).Symbol, scan.Local))
        {
            return true;
        }

        scan.Found = true;
        return false;
    }

    /// <summary>Returns whether an identifier binds to a local.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="identifier">The identifier.</param>
    /// <param name="local">The local symbol.</param>
    /// <returns><see langword="true"/> when the identifier is the local.</returns>
    private static bool ReferencesLocal(SyntaxNodeAnalysisContext context, IdentifierNameSyntax identifier, ISymbol local)
        => identifier.Identifier.ValueText == local.Name
            && SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol, local);

    /// <summary>Returns whether every placeholder is named and no name repeats.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="placeholders">The parsed placeholders.</param>
    /// <returns><see langword="true"/> when the placeholders are all named and distinct.</returns>
    private static bool AllNamedAndDistinct(string text, LogPlaceholder[] placeholders)
    {
        for (var i = 0; i < placeholders.Length; i++)
        {
            if (placeholders[i].Kind != LogPlaceholderKind.Named || HasEarlierMatch(text, placeholders, i))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a named placeholder repeats one that comes before it.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="placeholders">The parsed placeholders.</param>
    /// <param name="index">The placeholder position being examined.</param>
    /// <returns><see langword="true"/> when an earlier named placeholder shares this name.</returns>
    private static bool HasEarlierMatch(string text, LogPlaceholder[] placeholders, int index)
    {
        for (var j = 0; j < index; j++)
        {
            if (placeholders[j].Kind == LogPlaceholderKind.Named && LogMessageTemplate.NamesEqual(text, placeholders[j], placeholders[index]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the placeholder position a value name belongs in.</summary>
    /// <param name="text">The template's value text.</param>
    /// <param name="placeholders">The parsed placeholders.</param>
    /// <param name="name">The value name.</param>
    /// <returns>The placeholder position, or <c>-1</c> when no placeholder has the name.</returns>
    private static int IndexOfPlaceholder(string text, LogPlaceholder[] placeholders, string name)
    {
        for (var i = 0; i < placeholders.Length; i++)
        {
            if (LogMessageTemplate.NameEquals(text, placeholders[i], name))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns the name a value argument reads, when it is a bare identifier or a simple member access.</summary>
    /// <param name="expression">The argument expression.</param>
    /// <returns>The name, or <see langword="null"/> when the value is neither shape.</returns>
    private static string? GetValueName(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => name.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns the position of a parameter, before the template, whose type is an exception.</summary>
    /// <param name="parameters">The method's parameters.</param>
    /// <param name="templateIndex">The template argument's position.</param>
    /// <param name="exceptionType">The exception base type.</param>
    /// <returns>The exception parameter's position, or -1.</returns>
    private static int FindExceptionParameter(ImmutableArray<IParameterSymbol> parameters, int templateIndex, INamedTypeSymbol exceptionType)
    {
        for (var i = 0; i < templateIndex; i++)
        {
            if (DerivesFromException(parameters[i].Type, exceptionType))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns whether a type is the exception base type or derives from it.</summary>
    /// <param name="type">The type, if resolved.</param>
    /// <param name="exceptionType">The exception base type.</param>
    /// <returns><see langword="true"/> when the type is an exception.</returns>
    private static bool DerivesFromException(ITypeSymbol? type, INamedTypeSymbol exceptionType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, exceptionType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an argument list carries a string literal in any position.</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <returns><see langword="true"/> when a string literal is present.</returns>
    private static bool HasStringLiteralArgument(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an invoked member's name is a logging name.</summary>
    /// <param name="name">The invoked member's name.</param>
    /// <returns><see langword="true"/> when the name starts with <c>Log</c> or is <c>BeginScope</c>.</returns>
    private static bool IsLoggingName(string name)
        => name.StartsWith("Log", System.StringComparison.Ordinal) || name == "BeginScope";

    /// <summary>Returns the invoked member's name token.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>The name token, or <see langword="null"/> when the callee is not a named member.</returns>
    private static SyntaxToken? GetInvokedName(InvocationExpressionSyntax invocation)
        => invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => name.Identifier,
            MemberBindingExpressionSyntax { Name: IdentifierNameSyntax bound } => bound.Identifier,
            IdentifierNameSyntax identifier => identifier.Identifier,
            _ => null,
        };

    /// <summary>Builds the source location for a placeholder inside a literal, or the whole literal when it cannot be mapped.</summary>
    /// <param name="literal">The template literal.</param>
    /// <param name="placeholder">The placeholder.</param>
    /// <returns>The location.</returns>
    private static Location PlaceholderLocation(LiteralExpressionSyntax literal, in LogPlaceholder placeholder)
        => StringLiteralSpanMapper.TryMap(literal, placeholder.ValueStart, placeholder.ValueEnd - placeholder.ValueStart, out var span)
            ? Location.Create(literal.SyntaxTree, span)
            : literal.GetLocation();

    /// <summary>One logging call, described once so every rule reads the same bound facts.</summary>
    private sealed class LogCall
    {
        /// <summary>Gets the invocation.</summary>
        public InvocationExpressionSyntax Invocation { get; init; } = null!;

        /// <summary>Gets the invoked member's name token.</summary>
        public SyntaxToken NameToken { get; init; }

        /// <summary>Gets the logging method's name.</summary>
        public string MethodName { get; init; } = string.Empty;

        /// <summary>Gets the call's arguments.</summary>
        public SeparatedSyntaxList<ArgumentSyntax> Arguments { get; init; }

        /// <summary>Gets the template argument's position.</summary>
        public int TemplateIndex { get; init; }

        /// <summary>Gets the first tail argument's position.</summary>
        public int ParamsIndex { get; init; }

        /// <summary>Gets the exception argument's position, or -1.</summary>
        public int ExceptionIndex { get; init; }

        /// <summary>Gets the template literal.</summary>
        public LiteralExpressionSyntax Literal { get; init; } = null!;

        /// <summary>Gets the template's value text.</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>Gets the parsed placeholders.</summary>
        public LogPlaceholder[] Placeholders { get; init; } = [];
    }

    /// <summary>The scan state threaded through a local-reference descendant walk.</summary>
    private sealed class ReferenceScan
    {
        /// <summary>Initializes a new instance of the <see cref="ReferenceScan"/> class.</summary>
        /// <param name="model">The semantic model.</param>
        /// <param name="local">The local symbol.</param>
        /// <param name="name">The local's name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public ReferenceScan(SemanticModel model, ISymbol local, string name, CancellationToken cancellationToken)
        {
            Model = model;
            Local = local;
            Name = name;
            CancellationToken = cancellationToken;
        }

        /// <summary>Gets the semantic model.</summary>
        public SemanticModel Model { get; }

        /// <summary>Gets the local symbol.</summary>
        public ISymbol Local { get; }

        /// <summary>Gets the local's name.</summary>
        public string Name { get; }

        /// <summary>Gets the cancellation token.</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>Gets or sets a value indicating whether the local was found.</summary>
        public bool Found { get; set; }
    }

    /// <summary>The resolved logging symbols shared across a compilation's calls.</summary>
    private sealed class LoggingState
    {
        /// <summary>The logging method names that offer an exception-first overload.</summary>
        private readonly HashSet<string> _namesWithExceptionOverload;

        /// <summary>Initializes a new instance of the <see cref="LoggingState"/> class.</summary>
        /// <param name="loggerExtensions">The logging extension type.</param>
        /// <param name="exceptionType">The exception base type.</param>
        public LoggingState(INamedTypeSymbol loggerExtensions, INamedTypeSymbol exceptionType)
        {
            LoggerExtensions = loggerExtensions;
            ExceptionType = exceptionType;
            _namesWithExceptionOverload = BuildExceptionOverloadNames(loggerExtensions, exceptionType);
        }

        /// <summary>Gets the logging extension type.</summary>
        public INamedTypeSymbol LoggerExtensions { get; }

        /// <summary>Gets the exception base type.</summary>
        public INamedTypeSymbol ExceptionType { get; }

        /// <summary>Returns whether a logging method has an overload that takes the exception directly.</summary>
        /// <param name="methodName">The logging method's name.</param>
        /// <returns><see langword="true"/> when an exception overload exists.</returns>
        public bool HasExceptionOverload(string methodName) => _namesWithExceptionOverload.Contains(methodName);

        /// <summary>Collects the logging method names whose overloads include an exception parameter.</summary>
        /// <param name="loggerExtensions">The logging extension type.</param>
        /// <param name="exceptionType">The exception base type.</param>
        /// <returns>The set of names.</returns>
        private static HashSet<string> BuildExceptionOverloadNames(INamedTypeSymbol loggerExtensions, INamedTypeSymbol exceptionType)
        {
            var names = new HashSet<string>(System.StringComparer.Ordinal);
            var members = loggerExtensions.GetMembers();
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is IMethodSymbol method && HasExceptionParameter(method, exceptionType))
                {
                    names.Add(method.Name);
                }
            }

            return names;
        }

        /// <summary>Returns whether a method declares a parameter whose type is an exception.</summary>
        /// <param name="method">The method.</param>
        /// <param name="exceptionType">The exception base type.</param>
        /// <returns><see langword="true"/> when an exception parameter exists.</returns>
        private static bool HasExceptionParameter(IMethodSymbol method, INamedTypeSymbol exceptionType)
        {
            var parameters = method.Parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (DerivesFromException(parameters[i].Type, exceptionType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
