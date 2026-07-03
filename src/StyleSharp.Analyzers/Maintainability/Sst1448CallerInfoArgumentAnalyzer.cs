// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Flags arguments passed explicitly to caller-info parameters (SST1448): parameters marked
/// <c>[CallerMemberName]</c>, <c>[CallerFilePath]</c>, or <c>[CallerLineNumber]</c> exist so the
/// compiler injects the real call site, and supplying a value defeats that and usually reports
/// the wrong caller. Forwarding your own caller-info parameter onward is the intended pattern and
/// is never reported. The rule binds only invocations and creations that pass at least one
/// argument to a method with optional parameters, and the whole analyzer is gated on the
/// attributes existing in the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1448CallerInfoArgumentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the caller-member-name attribute.</summary>
    private const string CallerMemberNameMetadataName = "System.Runtime.CompilerServices.CallerMemberNameAttribute";

    /// <summary>The metadata name of the caller-file-path attribute.</summary>
    private const string CallerFilePathMetadataName = "System.Runtime.CompilerServices.CallerFilePathAttribute";

    /// <summary>The metadata name of the caller-line-number attribute.</summary>
    private const string CallerLineNumberMetadataName = "System.Runtime.CompilerServices.CallerLineNumberAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.CallerInfoArgument);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var memberName = start.Compilation.GetTypeByMetadataName(CallerMemberNameMetadataName);
            if (memberName is null)
            {
                return;
            }

            var attributes = new CallerInfoAttributes(
                memberName,
                start.Compilation.GetTypeByMetadataName(CallerFilePathMetadataName),
                start.Compilation.GetTypeByMetadataName(CallerLineNumberMetadataName));

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeArguments(nodeContext, attributes),
                SyntaxKind.InvocationExpression,
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Reports explicit arguments bound to caller-info parameters.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attributes">The compilation's caller-info attribute symbols.</param>
    private static void AnalyzeArguments(SyntaxNodeAnalysisContext context, CallerInfoAttributes attributes)
    {
        var argumentList = GetArgumentList(context.Node);
        if (argumentList is null || argumentList.Arguments.Count == 0)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!HasOptionalParameter(method))
        {
            return;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (FindParameter(method, arguments, i) is not { IsOptional: true } parameter)
            {
                continue;
            }

            if (attributes.Classify(parameter) is not { } description)
            {
                continue;
            }

            if (IsCallerInfoForwarding(argument.Expression, attributes, context))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.CallerInfoArgument,
                argument.SyntaxTree,
                argument.Span,
                description));
        }
    }

    /// <summary>Returns the argument list of an invocation or object creation.</summary>
    /// <param name="node">The reported node.</param>
    /// <returns>The argument list, or <see langword="null"/>.</returns>
    private static ArgumentListSyntax? GetArgumentList(SyntaxNode node)
        => node switch
        {
            InvocationExpressionSyntax invocation => invocation.ArgumentList,
            BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
            _ => null,
        };

    /// <summary>Returns whether a method declares any optional parameter (caller-info parameters must).</summary>
    /// <param name="method">The bound method.</param>
    /// <returns><see langword="true"/> when an optional parameter exists.</returns>
    private static bool HasOptionalParameter(IMethodSymbol method)
    {
        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].IsOptional)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Maps one argument to the parameter it supplies.</summary>
    /// <param name="method">The bound method.</param>
    /// <param name="arguments">The argument list.</param>
    /// <param name="index">The argument index.</param>
    /// <returns>The matched parameter, or <see langword="null"/>.</returns>
    private static IParameterSymbol? FindParameter(IMethodSymbol method, SeparatedSyntaxList<ArgumentSyntax> arguments, int index)
    {
        var parameters = method.Parameters;
        if (arguments[index].NameColon is { Name.Identifier.ValueText: var argumentName })
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Name == argumentName)
                {
                    return parameters[i];
                }
            }

            return null;
        }

        return index < parameters.Length ? parameters[index] : null;
    }

    /// <summary>
    /// Returns whether an argument merely forwards the enclosing member's own caller-info
    /// parameter, which is the intended way to propagate the original call site.
    /// </summary>
    /// <param name="expression">The argument expression.</param>
    /// <param name="attributes">The compilation's caller-info attribute symbols.</param>
    /// <param name="context">The syntax node analysis context.</param>
    /// <returns><see langword="true"/> when the argument forwards a caller-info parameter.</returns>
    private static bool IsCallerInfoForwarding(ExpressionSyntax expression, CallerInfoAttributes attributes, SyntaxNodeAnalysisContext context)
        => expression is IdentifierNameSyntax
            && context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol is IParameterSymbol forwarded
            && attributes.Classify(forwarded) is not null;

    /// <summary>The compilation's caller-info attribute symbols.</summary>
    private sealed class CallerInfoAttributes
    {
        /// <summary>The caller-member-name attribute symbol.</summary>
        private readonly INamedTypeSymbol _memberName;

        /// <summary>The caller-file-path attribute symbol.</summary>
        private readonly INamedTypeSymbol? _filePath;

        /// <summary>The caller-line-number attribute symbol.</summary>
        private readonly INamedTypeSymbol? _lineNumber;

        /// <summary>Initializes a new instance of the <see cref="CallerInfoAttributes"/> class.</summary>
        /// <param name="memberName">The caller-member-name attribute symbol.</param>
        /// <param name="filePath">The caller-file-path attribute symbol.</param>
        /// <param name="lineNumber">The caller-line-number attribute symbol.</param>
        public CallerInfoAttributes(INamedTypeSymbol memberName, INamedTypeSymbol? filePath, INamedTypeSymbol? lineNumber)
        {
            _memberName = memberName;
            _filePath = filePath;
            _lineNumber = lineNumber;
        }

        /// <summary>Describes the caller-info attribute a parameter carries, if any.</summary>
        /// <param name="parameter">The parameter to classify.</param>
        /// <returns>The message description, or <see langword="null"/> when not caller-info.</returns>
        public string? Classify(IParameterSymbol parameter)
        {
            var parameterAttributes = parameter.GetAttributes();
            for (var i = 0; i < parameterAttributes.Length; i++)
            {
                var attributeClass = parameterAttributes[i].AttributeClass;
                if (SymbolEqualityComparer.Default.Equals(attributeClass, _memberName))
                {
                    return "member name";
                }

                if (SymbolEqualityComparer.Default.Equals(attributeClass, _filePath))
                {
                    return "file path";
                }

                if (SymbolEqualityComparer.Default.Equals(attributeClass, _lineNumber))
                {
                    return "line number";
                }
            }

            return null;
        }
    }
}
