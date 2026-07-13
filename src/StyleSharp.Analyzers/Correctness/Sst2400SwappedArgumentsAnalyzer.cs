// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a call whose argument names match the parameter names but sit in a different order (SST2400):
/// <c>Copy(target, source)</c> against <c>Copy(string source, string target)</c>.
/// </summary>
/// <remarks>
/// <para>
/// A false positive here is severe — the fix reorders a working call — so the rule only reports a genuine
/// transposition, and defines that as narrowly as it can:
/// </para>
/// <list type="bullet">
/// <item>every argument is positional (one named argument and the order is already explicit);</item>
/// <item>the argument is a bare identifier, so its name is the only thing being read;</item>
/// <item>the identifier names a parameter the call really has, at a different position;</item>
/// <item>the argument in <em>that</em> position names <em>this</em> parameter — a two-cycle, not a chain;</item>
/// <item>the two parameters have the same type and ref kind, so the reordered call binds to the same
/// overload and still compiles.</item>
/// </list>
/// <para>
/// The last condition is what makes the rule safe to act on. If the two parameters had different types, the
/// swapped call would not have compiled in the first place, so a same-type pair is the only shape that
/// silently does the wrong thing — and it is the only shape a fix can reorder without changing which method
/// is called.
/// </para>
/// <para>
/// The clean path is a scan of the argument list for identifiers and named arguments. Nothing binds until a
/// call has at least two bare-identifier arguments, which is a small fraction of call sites.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2400SwappedArgumentsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property holding the position the reported argument belongs in.</summary>
    internal const string SwapWithKey = "SwapWith";

    /// <summary>The fewest arguments a transposition needs.</summary>
    private const int MinimumIdentifierArguments = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.SwappedArguments);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.InvocationExpression,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    /// <summary>Analyzes one call for transposed arguments.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (GetArgumentList(context.Node) is not { } argumentList || !HasTransposableShape(argumentList.Arguments))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        var parameters = method.Parameters;
        var arguments = argumentList.Arguments;
        if (method.IsVararg || parameters.Length != arguments.Count || TakesParameterArray(parameters))
        {
            return;
        }

        ReportTranspositions(context, arguments, parameters);
    }

    /// <summary>Reports every argument pair that names the other's parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="parameters">The resolved parameters.</param>
    private static void ReportTranspositions(
        SyntaxNodeAnalysisContext context,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        ImmutableArray<IParameterSymbol> parameters)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            var partner = FindTransposedPartner(arguments, parameters, i);
            if (partner <= i)
            {
                continue;
            }

            var argument = arguments[i];
            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.SwappedArguments,
                argument.SyntaxTree,
                argument.Span,
                BuildProperties(partner),
                GetIdentifierName(argument)!,
                parameters[i].Name));
        }
    }

    /// <summary>Finds the position whose argument and parameter name mirror this one's.</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="parameters">The resolved parameters.</param>
    /// <param name="index">The argument position being examined.</param>
    /// <returns>The transposed partner's position, or <c>-1</c> when this argument is in the right place.</returns>
    /// <remarks>
    /// Only a two-cycle counts. A longer rotation — <c>M(b, c, a)</c> against <c>(a, b, c)</c> — has no
    /// unambiguous repair, and reading a rotation as a mistake is a guess this rule declines to make.
    /// </remarks>
    private static int FindTransposedPartner(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        ImmutableArray<IParameterSymbol> parameters,
        int index)
    {
        if (GetIdentifierName(arguments[index]) is not { } name)
        {
            return -1;
        }

        var partner = IndexOfParameter(parameters, name);
        if (partner < 0 || partner == index)
        {
            return -1;
        }

        if (!NameMatches(GetIdentifierName(arguments[partner]), parameters[index].Name)
            || !IsInterchangeable(parameters[index], parameters[partner]))
        {
            return -1;
        }

        return partner;
    }

    /// <summary>Returns whether two parameters can trade places without changing what the call means.</summary>
    /// <param name="first">The first parameter.</param>
    /// <param name="second">The second parameter.</param>
    /// <returns><see langword="true"/> when the reordered call binds identically.</returns>
    private static bool IsInterchangeable(IParameterSymbol first, IParameterSymbol second)
        => first.RefKind == second.RefKind
            && SymbolEqualityComparer.Default.Equals(first.Type, second.Type);

    /// <summary>Returns the position of the parameter with the given name.</summary>
    /// <param name="parameters">The resolved parameters.</param>
    /// <param name="name">The name to look for.</param>
    /// <returns>The parameter's position, or <c>-1</c> when the call has no such parameter.</returns>
    private static int IndexOfParameter(ImmutableArray<IParameterSymbol> parameters, string name)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (NameMatches(name, parameters[i].Name))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Compares an argument identifier with a parameter name.</summary>
    /// <param name="argumentName">The argument's identifier, when it has one.</param>
    /// <param name="parameterName">The parameter's name.</param>
    /// <returns><see langword="true"/> when the two name the same thing.</returns>
    /// <remarks>
    /// Case is ignored so a <c>Source</c> property or a <c>Target</c> local still reads as the parameter it
    /// is named after; a transposition is a naming mistake, and casing does not make it less of one.
    /// </remarks>
    private static bool NameMatches(string? argumentName, string parameterName)
        => argumentName is not null && string.Equals(argumentName, parameterName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds the property bag telling the fix where the reported argument belongs.</summary>
    /// <param name="partner">The transposed partner's position.</param>
    /// <returns>The diagnostic properties.</returns>
    private static ImmutableDictionary<string, string?> BuildProperties(int partner)
        => ImmutableDictionary<string, string?>.Empty.Add(
            SwapWithKey,
            partner.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Returns whether an argument list is worth binding for.</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <returns><see langword="true"/> when two or more positional arguments are bare identifiers.</returns>
    /// <remarks>
    /// A named argument settles the order at the call site, so one anywhere in the list ends the analysis.
    /// </remarks>
    private static bool HasTransposableShape(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        var identifiers = 0;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is not null)
            {
                return false;
            }

            if (arguments[i].Expression is IdentifierNameSyntax)
            {
                identifiers++;
            }
        }

        return identifiers >= MinimumIdentifierArguments;
    }

    /// <summary>Returns whether the last parameter absorbs a variable number of arguments.</summary>
    /// <param name="parameters">The resolved parameters.</param>
    /// <returns><see langword="true"/> when the call may be in expanded form.</returns>
    /// <remarks>
    /// With a <c>params</c> tail an argument's position no longer maps to a parameter's position, so a name
    /// landing in the "wrong" slot proves nothing.
    /// </remarks>
    private static bool TakesParameterArray(ImmutableArray<IParameterSymbol> parameters)
        => parameters.Length > 0 && parameters[parameters.Length - 1].IsParams;

    /// <summary>Gets an argument's identifier text.</summary>
    /// <param name="argument">The argument.</param>
    /// <returns>The identifier, or <see langword="null"/> when the argument is not a bare identifier.</returns>
    private static string? GetIdentifierName(ArgumentSyntax argument)
        => argument.Expression is IdentifierNameSyntax identifier ? identifier.Identifier.ValueText : null;

    /// <summary>Gets the argument list of a call.</summary>
    /// <param name="node">The invocation or object creation.</param>
    /// <returns>The argument list, or <see langword="null"/> when the call has none.</returns>
    private static ArgumentListSyntax? GetArgumentList(SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax invocation => invocation.ArgumentList,
        BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
        _ => null,
    };
}
