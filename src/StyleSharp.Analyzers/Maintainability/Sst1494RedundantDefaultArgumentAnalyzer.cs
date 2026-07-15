// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a trailing argument whose value is already the parameter's declared default (SST1494), so the
/// argument can be dropped without changing what the call does.
/// </summary>
/// <remarks>
/// <para>
/// Only a trailing run is reported. An earlier argument cannot be dropped without naming every argument
/// after it, so the walk starts at the last argument and stops at the first one that is not redundant —
/// which makes every reported argument safe to delete along with everything to its right.
/// </para>
/// <para>
/// Values are compared as constants, not as syntax: <c>0</c>, <c>0x0</c> and a <c>const Zero = 0</c> all
/// repeat a default of <c>0</c>. A caller-info parameter is skipped because the compiler — not the caller —
/// is meant to fill it, and SST1448 already owns that call site. A call inside an expression tree is left
/// alone entirely: the language forbids omitting optional arguments there, so the "fix" would not compile.
/// </para>
/// <para>
/// Nothing is reported until the shortened call has been bound and proved to still mean the same thing.
/// Dropping an argument can move a call to a different overload — <c>M(int a, int b = 0)</c> beside
/// <c>M(int a)</c> — and a rule that quietly re-targets a call is worse than one that says nothing. A
/// target-typed <c>new(...)</c> is therefore not analyzed at all: without its target type there is no way to
/// bind the shortened form, and so no way to prove the omission safe.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1494RedundantDefaultArgumentAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.RedundantDefaultArgument);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.InvocationExpression,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.BaseConstructorInitializer,
            SyntaxKind.ThisConstructorInitializer);
    }

    /// <summary>Builds the argument list with the reported argument, and everything after it, removed.</summary>
    /// <param name="list">The argument list.</param>
    /// <param name="index">The first argument index to drop.</param>
    /// <returns>The shortened argument list.</returns>
    internal static ArgumentListSyntax TruncateAt(ArgumentListSyntax list, int index)
    {
        var arguments = list.Arguments;
        while (index >= 0 && arguments.Count > index)
        {
            arguments = arguments.RemoveAt(arguments.Count - 1);
        }

        return list.WithArguments(arguments);
    }

    /// <summary>Returns whether the shortened call still binds to the method the full call binds to.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="call">The invocation, object creation, or constructor initializer.</param>
    /// <param name="index">The first argument index to drop.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when omitting the arguments changes nothing about what is called.</returns>
    /// <remarks>
    /// The shortened call is bound speculatively rather than reasoned about. Overload resolution is the only
    /// thing that knows whether <c>M(1)</c> and <c>M(1, 0)</c> reach the same method, and an argument that
    /// looks droppable but moves the call elsewhere must never be reported — the diagnostic would be a bug
    /// report against working code, and the fix would silently change behavior.
    /// </remarks>
    internal static bool OmissionKeepsTheSameTarget(SemanticModel model, SyntaxNode call, int index, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(call, cancellationToken).Symbol is not IMethodSymbol method
            || ArgumentBinding.GetArgumentList(call) is not { } list)
        {
            return false;
        }

        var shortened = TruncateAt(list, index);
        var speculative = call switch
        {
            InvocationExpressionSyntax invocation => model.GetSpeculativeSymbolInfo(
                call.SpanStart,
                invocation.WithArgumentList(shortened),
                SpeculativeBindingOption.BindAsExpression),
            ObjectCreationExpressionSyntax creation => model.GetSpeculativeSymbolInfo(
                call.SpanStart,
                creation.WithArgumentList(shortened),
                SpeculativeBindingOption.BindAsExpression),
            ConstructorInitializerSyntax initializer => model.GetSpeculativeSymbolInfo(
                call.SpanStart,
                initializer.WithArgumentList(shortened)),
            _ => default,
        };

        return SymbolEqualityComparer.Default.Equals(speculative.Symbol, method);
    }

    /// <summary>Reports the trailing run of arguments that repeat their parameter's default.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (ArgumentBinding.GetArgumentList(context.Node) is not { } argumentList || argumentList.Arguments.Count == 0)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is not IMethodSymbol method
            || !ArgumentBinding.HasOptionalParameter(method))
        {
            return;
        }

        var arguments = argumentList.Arguments;
        var firstRedundant = FindFirstRedundantArgument(method, arguments, context);
        if (firstRedundant == arguments.Count
            || IsInsideExpressionTree(context)
            || !OmissionKeepsTheSameTarget(context.SemanticModel, context.Node, firstRedundant, context.CancellationToken))
        {
            return;
        }

        for (var i = firstRedundant; i < arguments.Count; i++)
        {
            var parameter = ArgumentBinding.FindParameter(method, arguments, i);
            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.RedundantDefaultArgument,
                arguments[i].GetLocation(),
                parameter!.Name));
        }
    }

    /// <summary>Walks back from the last argument to find where the redundant trailing run begins.</summary>
    /// <param name="method">The bound method.</param>
    /// <param name="arguments">The argument list.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns>The index the run starts at, or the argument count when there is no run.</returns>
    private static int FindFirstRedundantArgument(
        IMethodSymbol method,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        SyntaxNodeAnalysisContext context)
    {
        var firstRedundant = arguments.Count;
        for (var i = arguments.Count - 1; i >= 0; i--)
        {
            if (!IsRedundant(method, arguments, i, context))
            {
                break;
            }

            firstRedundant = i;
        }

        return firstRedundant;
    }

    /// <summary>Returns whether one argument states exactly what its parameter already defaults to.</summary>
    /// <param name="method">The bound method.</param>
    /// <param name="arguments">The argument list.</param>
    /// <param name="index">The argument index.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when the argument can be dropped.</returns>
    /// <remarks>
    /// A <c>params</c> parameter is never optional, so it fails the first test and stops the walk. A
    /// caller-info parameter is optional but belongs to the compiler, so it stops the walk too rather than
    /// being reported here.
    /// </remarks>
    private static bool IsRedundant(
        IMethodSymbol method,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        int index,
        SyntaxNodeAnalysisContext context)
    {
        if (ArgumentBinding.FindParameter(method, arguments, index) is not { IsOptional: true, HasExplicitDefaultValue: true } parameter
            || IsCallerInfoParameter(parameter))
        {
            return false;
        }

        var constant = context.SemanticModel.GetConstantValue(arguments[index].Expression, context.CancellationToken);
        return constant.HasValue && ConstantsMatch(constant.Value, parameter.ExplicitDefaultValue);
    }

    /// <summary>Returns whether the compiler, rather than the caller, is meant to supply the parameter.</summary>
    /// <param name="parameter">The matched parameter.</param>
    /// <returns><see langword="true"/> for a caller-info parameter, which SST1448 reports instead.</returns>
    private static bool IsCallerInfoParameter(IParameterSymbol parameter)
    {
        var attributes = parameter.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (attributes[i].AttributeClass?.Name is "CallerMemberNameAttribute"
                or "CallerFilePathAttribute"
                or "CallerLineNumberAttribute"
                or "CallerArgumentExpressionAttribute")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the call sits inside an expression tree, where optional arguments must be written out.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when dropping the argument would not compile.</returns>
    /// <remarks>Runs only after a redundant argument is found, so a clean call never pays for the lambda bind.</remarks>
    private static bool IsInsideExpressionTree(SyntaxNodeAnalysisContext context)
    {
        for (var node = context.Node.Parent; node is not null; node = node.Parent)
        {
            if (node is AnonymousFunctionExpressionSyntax function
                && IsExpressionTreeType(context.SemanticModel.GetTypeInfo(function, context.CancellationToken).ConvertedType))
            {
                return true;
            }

            if (node is MemberDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Returns whether a converted lambda type is an expression tree.</summary>
    /// <param name="type">The lambda's converted type.</param>
    /// <returns><see langword="true"/> when the lambda becomes data rather than code.</returns>
    private static bool IsExpressionTreeType(ITypeSymbol? type)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (current.Name == "Expression"
                && current.ContainingNamespace is
                {
                    Name: "Expressions",
                    ContainingNamespace: { Name: "Linq", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } },
                })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Compares an argument's constant value with the parameter's declared default.</summary>
    /// <param name="argumentValue">The argument's constant value.</param>
    /// <param name="defaultValue">The parameter's declared default value.</param>
    /// <returns><see langword="true"/> when the two denote the same value.</returns>
    /// <remarks>
    /// Boxed constants of different widths are not <see cref="object.Equals(object, object)"/>-equal — an
    /// <c>int</c> literal <c>0</c> passed to a <c>long</c> parameter defaulting to <c>0</c> arrives as a
    /// boxed <see cref="int"/> against a boxed <see cref="long"/> — so numeric values are compared by value.
    /// </remarks>
    private static bool ConstantsMatch(object? argumentValue, object? defaultValue)
    {
        if (argumentValue is null || defaultValue is null)
        {
            return argumentValue is null && defaultValue is null;
        }

        return argumentValue.Equals(defaultValue)
            || (IsNumeric(argumentValue) && IsNumeric(defaultValue) && NumbersMatch(argumentValue, defaultValue));
    }

    /// <summary>Returns whether a boxed constant is one of the numeric primitives.</summary>
    /// <param name="value">The boxed constant.</param>
    /// <returns><see langword="true"/> when the value can be compared numerically.</returns>
    private static bool IsNumeric(object value) => IsIntegral(value) || IsReal(value);

    /// <summary>Returns whether a boxed constant is an integral primitive.</summary>
    /// <param name="value">The boxed constant.</param>
    /// <returns><see langword="true"/> for the signed and unsigned integer types.</returns>
    private static bool IsIntegral(object value) => value is sbyte or byte or short or ushort or int or uint or long or ulong;

    /// <summary>Returns whether a boxed constant is a fractional primitive.</summary>
    /// <param name="value">The boxed constant.</param>
    /// <returns><see langword="true"/> for the floating-point and decimal types.</returns>
    private static bool IsReal(object value) => value is float or double or decimal;

    /// <summary>Compares two boxed numeric constants by value rather than by boxed type.</summary>
    /// <param name="left">The first boxed constant.</param>
    /// <param name="right">The second boxed constant.</param>
    /// <returns><see langword="true"/> when both denote the same number.</returns>
    private static bool NumbersMatch(object left, object right)
    {
        if (left is float or double || right is float or double)
        {
            return Convert.ToDouble(left, CultureInfo.InvariantCulture).Equals(Convert.ToDouble(right, CultureInfo.InvariantCulture));
        }

        return Convert.ToDecimal(left, CultureInfo.InvariantCulture) == Convert.ToDecimal(right, CultureInfo.InvariantCulture);
    }
}
