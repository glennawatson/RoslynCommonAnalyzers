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
/// argument to a method with optional parameters, and resolves the attribute symbols only
/// when an explicit argument targets an optional parameter.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1448CallerInfoArgumentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The message description for a caller-member-name parameter.</summary>
    private const string MemberNameDescription = "member name";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(MaintainabilityRules.CallerInfoArgument);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyCompilationValue<CallerInfoAttributes?>(compilation, ResolveCallerInfoAttributes, runOnce: true),
            AnalyzeArguments,
            SyntaxKind.InvocationExpression,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    /// <summary>Reports explicit arguments bound to caller-info parameters.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attributeTypes">The caller-info attribute type cache for this compilation.</param>
    private static void AnalyzeArguments(in SyntaxNodeAnalysisContext context, LazyCompilationValue<CallerInfoAttributes?> attributeTypes)
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

            if (Classify(parameter, attributeTypes) is not { } description)
            {
                continue;
            }

            if (!IsRedundant(argument.Expression, description, attributeTypes, context))
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
        LazyCompilationValue<CallerInfoAttributes?> attributes,
        in SyntaxNodeAnalysisContext context) =>
        !IsCallerInfoForwarding(expression, attributes, context)
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
    private static bool SuppliesTheSameMemberName(ExpressionSyntax expression, in SyntaxNodeAnalysisContext context) =>
        context.SemanticModel.GetConstantValue(expression, context.CancellationToken) is { HasValue: true, Value: string stated }
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
    private static string? GetEnclosingCallerMemberName(in SyntaxNodeAnalysisContext context)
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
    private static bool IsCallerInfoForwarding(ExpressionSyntax expression, LazyCompilationValue<CallerInfoAttributes?> attributes, in SyntaxNodeAnalysisContext context) =>
        expression is IdentifierNameSyntax
            && context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol is IParameterSymbol forwarded
            && Classify(forwarded, attributes) is not null;

    /// <summary>Describes the caller-info attribute a parameter carries, if any.</summary>
    /// <param name="parameter">The parameter to classify.</param>
    /// <param name="attributeTypes">The caller-info attribute symbols, resolved on first demand within one compilation.</param>
    /// <returns>The message description, or <see langword="null"/> when not caller-info.</returns>
    private static string? Classify(IParameterSymbol parameter, LazyCompilationValue<CallerInfoAttributes?> attributeTypes)
    {
        var parameterAttributes = parameter.GetAttributes();
        if (parameterAttributes.IsEmpty || attributeTypes.Get() is not { } types)
        {
            return null;
        }

        for (var i = 0; i < parameterAttributes.Length; i++)
        {
            var attributeClass = parameterAttributes[i].AttributeClass;
            if (SymbolEqualityComparer.Default.Equals(attributeClass, types.MemberName))
            {
                return MemberNameDescription;
            }

            if (SymbolEqualityComparer.Default.Equals(attributeClass, types.FilePath))
            {
                return "file path";
            }

            if (SymbolEqualityComparer.Default.Equals(attributeClass, types.LineNumber))
            {
                return "line number";
            }
        }

        return null;
    }

    /// <summary>Resolves the caller-info attribute symbols once, when the required caller-member-name attribute exists.</summary>
    /// <param name="compilation">The compilation whose references are searched.</param>
    /// <returns>The attribute symbols, or <see langword="null"/> when caller-member-name is absent.</returns>
    private static CallerInfoAttributes? ResolveCallerInfoAttributes(Compilation compilation) =>
        compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.CallerMemberNameAttribute") is { } memberName
            ? new CallerInfoAttributes(
                memberName,
                compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.CallerFilePathAttribute"),
                compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.CallerLineNumberAttribute"))
            : null;

    /// <summary>The caller-info attribute symbols resolved once per compilation.</summary>
    /// <param name="MemberName">The caller-member-name attribute.</param>
    /// <param name="FilePath">The caller-file-path attribute, or null when absent.</param>
    /// <param name="LineNumber">The caller-line-number attribute, or null when absent.</param>
    private sealed record CallerInfoAttributes(INamedTypeSymbol MemberName, INamedTypeSymbol? FilePath, INamedTypeSymbol? LineNumber);
}
