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
    /// <summary>The message description for a caller-member-name parameter.</summary>
    private const string MemberNameDescription = "member name";

    /// <summary>The metadata name of the caller-member-name attribute.</summary>
    private const string CallerMemberNameMetadataName = "System.Runtime.CompilerServices.CallerMemberNameAttribute";

    /// <summary>The metadata name of the caller-file-path attribute.</summary>
    private const string CallerFilePathMetadataName = "System.Runtime.CompilerServices.CallerFilePathAttribute";

    /// <summary>The metadata name of the caller-line-number attribute.</summary>
    private const string CallerLineNumberMetadataName = "System.Runtime.CompilerServices.CallerLineNumberAttribute";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(MaintainabilityRules.CallerInfoArgument);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

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
        var argumentList = ArgumentBinding.GetArgumentList(context.Node);
        if (argumentList is null || argumentList.Arguments.Count == 0)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!ArgumentBinding.HasOptionalParameter(method))
        {
            return;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (ArgumentBinding.FindParameter(method, arguments, i) is not { IsOptional: true } parameter)
            {
                continue;
            }

            if (attributes.Classify(parameter) is not { } description)
            {
                continue;
            }

            if (!IsRedundant(argument.Expression, description, attributes, context))
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

    /// <summary>Returns whether removing the argument would leave the call meaning the same thing.</summary>
    /// <param name="expression">The argument expression.</param>
    /// <param name="description">The caller-info kind the parameter carries.</param>
    /// <param name="attributes">The compilation's caller-info attribute symbols.</param>
    /// <param name="context">The syntax node analysis context.</param>
    /// <returns><see langword="true"/> when the argument states what the compiler would supply anyway.</returns>
    private static bool IsRedundant(
        ExpressionSyntax expression,
        string description,
        CallerInfoAttributes attributes,
        SyntaxNodeAnalysisContext context)
        => !IsCallerInfoForwarding(expression, attributes, context)
        && (description != MemberNameDescription || SuppliesTheSameMemberName(expression, context));

    /// <summary>Returns whether the compiler would supply exactly the text the argument states.</summary>
    /// <param name="expression">The argument expression.</param>
    /// <param name="context">The syntax node analysis context.</param>
    /// <returns><see langword="true"/> only when removing the argument would preserve the value.</returns>
    /// <remarks>
    /// The rule's premise is that the argument is redundant, and that holds only where the enclosing
    /// member is the name being passed. A call inside a constructor is handed <c>.ctor</c>, so
    /// <c>Register(nameof(Width))</c> there is not redundant at all — dropping it collapses every such
    /// call onto one name. The same gap opens for an accessor calling a helper about another member,
    /// and for any argument that simply states something else.
    /// </remarks>
    private static bool SuppliesTheSameMemberName(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
        => context.SemanticModel.GetConstantValue(expression, context.CancellationToken) is { HasValue: true, Value: string stated }
        && GetEnclosingCallerMemberName(context) is { } supplied
        && string.Equals(stated, supplied, StringComparison.Ordinal);

    /// <summary>Returns the text <c>[CallerMemberName]</c> receives at this position.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <returns>The supplied name, or <see langword="null"/> where it cannot be determined.</returns>
    /// <remarks>
    /// A lambda and a local function do not carry a name of their own — the value comes from the
    /// member containing them — so the walk continues through both. Anything this does not recognise
    /// yields <see langword="null"/> and the argument is left alone, since guessing wrong here is what
    /// produces the defect.
    /// </remarks>
    private static string? GetEnclosingCallerMemberName(SyntaxNodeAnalysisContext context)
    {
        var symbol = context.SemanticModel.GetEnclosingSymbol(context.Node.SpanStart, context.CancellationToken);
        for (; symbol is not null; symbol = symbol.ContainingSymbol)
        {
            switch (symbol)
            {
                case IMethodSymbol { MethodKind: MethodKind.LocalFunction or MethodKind.AnonymousFunction }:
                    continue;

                case IMethodSymbol { MethodKind: MethodKind.Constructor }:
                    return ".ctor";

                case IMethodSymbol { MethodKind: MethodKind.StaticConstructor }:
                    return ".cctor";

                case IMethodSymbol { MethodKind: MethodKind.Destructor }:
                    return "Finalize";

                // An accessor reports the property or event it belongs to; an indexer's is 'Item'.
                case IMethodSymbol { AssociatedSymbol: { } associated }:
                    return associated.MetadataName;

                case IMethodSymbol method:
                    return method.MetadataName;

                default:
                    return null;
            }
        }

        return null;
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
                    return MemberNameDescription;
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
