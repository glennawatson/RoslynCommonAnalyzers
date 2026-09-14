// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>TaskCompletionSource</c> creations that do not opt into
/// <c>TaskCreationOptions.RunContinuationsAsynchronously</c> (PSH1302). A creation is reported
/// when the bound constructor takes no <c>TaskCreationOptions</c> parameter at all, or when the
/// options argument is a compile-time constant that lacks the flag. Non-constant options
/// arguments are skipped — the flag may arrive at runtime — so the rule under-reports rather
/// than guessing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1302RunContinuationsAsynchronouslyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The simple type name creations are gated on before binding.</summary>
    internal const string TypeName = "TaskCompletionSource";

    /// <summary>The numeric value of <c>TaskCreationOptions.RunContinuationsAsynchronously</c>.</summary>
    internal const int RunContinuationsAsynchronouslyValue = 0x40;

    /// <summary>The metadata name of the non-generic completion-source type (.NET 5+).</summary>
    private const string NonGenericMetadataName = "System.Threading.Tasks.TaskCompletionSource";

    /// <summary>The metadata name of the generic completion-source type.</summary>
    private const string GenericMetadataName = "System.Threading.Tasks.TaskCompletionSource`1";

    /// <summary>The metadata name of the task creation options enum.</summary>
    private const string OptionsMetadataName = "System.Threading.Tasks.TaskCreationOptions";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ConcurrencyRules.RunContinuationsAsynchronously);

    /// <summary>The metadata names CompletionSourceTypes resolves, in slot order.</summary>
    private static readonly string[] CompletionSourceTypesMetadataNames =
    [
        GenericMetadataName,
        NonGenericMetadataName,
        OptionsMetadataName
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataTypes(compilation, CompletionSourceTypesMetadataNames),
            AnalyzeCreation,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    /// <summary>Returns the argument bound to a parameter ordinal, honoring named arguments.</summary>
    /// <param name="creation">The creation expression.</param>
    /// <param name="parameterName">The parameter's name, for named-argument matching.</param>
    /// <param name="ordinal">The parameter ordinal to resolve.</param>
    /// <returns>The matching argument, or <see langword="null"/> when it is not supplied.</returns>
    internal static ArgumentSyntax? FindArgumentForOrdinal(BaseObjectCreationExpressionSyntax creation, string parameterName, int ordinal)
    {
        if (creation.ArgumentList is not { } argumentList)
        {
            return null;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (argument.NameColon is { } nameColon)
            {
                if (nameColon.Name.Identifier.ValueText == parameterName)
                {
                    return argument;
                }

                continue;
            }

            if (i == ordinal)
            {
                return argument;
            }
        }

        return null;
    }

    /// <summary>Returns whether an explicit creation names the completion-source type, before any binding.</summary>
    /// <param name="creation">The creation expression.</param>
    /// <returns><see langword="true"/> for implicit creations and creations whose rightmost type name matches.</returns>
    private static bool MatchesTypeNameGate(BaseObjectCreationExpressionSyntax creation)
    {
        if (creation is not ObjectCreationExpressionSyntax explicitCreation)
        {
            return true;
        }

        var name = explicitCreation.Type;
        while (true)
        {
            switch (name)
            {
                case QualifiedNameSyntax qualified:
                {
                    name = qualified.Right;
                    continue;
                }

                case AliasQualifiedNameSyntax aliasQualified:
                {
                    name = aliasQualified.Name;
                    continue;
                }

                case GenericNameSyntax generic:
                    return generic.Identifier.ValueText == TypeName;
                case IdentifierNameSyntax identifier:
                    return identifier.Identifier.ValueText == TypeName;
                default:
                    return false;
            }
        }
    }

    /// <summary>Reports PSH1302 for a completion-source creation with provably missing continuation options.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation's lazily resolved completion-source and options types.</param>
    private static void AnalyzeCreation(
        in SyntaxNodeAnalysisContext context,
        LazyMetadataTypes types)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (!MatchesTypeNameGate(creation)
            || context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || constructor.ContainingType.Name != TypeName)
        {
            return;
        }

        var resolved = types.Get();
        if (resolved[0] is not { } genericType
            || resolved[2] is not { } optionsType
            || !IsCompletionSourceType(constructor.ContainingType, genericType, resolved[1]))
        {
            return;
        }

        if (!HasProvablyMissingFlag(context, creation, constructor, optionsType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.RunContinuationsAsynchronously,
            creation.GetLocation()));
    }

    /// <summary>Returns whether a constructed type is one of the completion-source types.</summary>
    /// <param name="containingType">The constructor's containing type.</param>
    /// <param name="genericType">The generic completion-source type.</param>
    /// <param name="nonGenericType">The non-generic completion-source type, when it exists.</param>
    /// <returns><see langword="true"/> when the type matches.</returns>
    private static bool IsCompletionSourceType(INamedTypeSymbol containingType, INamedTypeSymbol genericType, INamedTypeSymbol? nonGenericType) =>
        SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, genericType)
            || (nonGenericType is not null && SymbolEqualityComparer.Default.Equals(containingType, nonGenericType));

    /// <summary>
    /// Returns whether the creation provably omits the flag: either no constructor parameter is a
    /// <c>TaskCreationOptions</c>, or the options argument is a constant without the flag bit.
    /// </summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="creation">The creation expression.</param>
    /// <param name="constructor">The bound constructor.</param>
    /// <param name="optionsType">The task creation options enum type.</param>
    /// <returns><see langword="true"/> when the flag is provably absent.</returns>
    private static bool HasProvablyMissingFlag(
        in SyntaxNodeAnalysisContext context,
        BaseObjectCreationExpressionSyntax creation,
        IMethodSymbol constructor,
        INamedTypeSymbol optionsType)
    {
        var optionsOrdinal = -1;
        var parameters = constructor.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(parameters[i].Type, optionsType))
            {
                continue;
            }

            optionsOrdinal = i;
            break;
        }

        if (optionsOrdinal < 0)
        {
            return true;
        }

        if (FindArgumentForOrdinal(creation, parameters[optionsOrdinal].Name, optionsOrdinal) is not { } optionsArgument)
        {
            return false;
        }

        var constant = context.SemanticModel.GetConstantValue(optionsArgument.Expression, context.CancellationToken);
        return constant is { HasValue: true, Value: int value }
            && (value & RunContinuationsAsynchronouslyValue) == 0;
    }
}
