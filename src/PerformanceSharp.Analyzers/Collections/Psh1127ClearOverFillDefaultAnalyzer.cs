// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags an <c>Array.Fill</c> call whose fill value is the element type's default (PSH1127) —
/// <c>Fill(buffer, default)</c>, <c>Fill(buffer, 0)</c>, <c>Fill(buffer, null)</c>, and the
/// four-argument ranged form — because <c>Array.Clear</c> hands the block to the runtime, which
/// zeroes it with a single vectorizable memset instead of storing element by element.
/// <para>
/// The value's meaning is decided against the array's <em>element type</em>, never the literal
/// alone: <c>Fill(new object[4], 0)</c> stores a boxed zero, which is not <see langword="null"/>,
/// so it is never reported. A <c>0</c> or <c>false</c> literal qualifies only for a non-nullable
/// value element type, and a <c>null</c> literal only for a reference or nullable one, while
/// <c>default</c> always qualifies.
/// </para>
/// <para>
/// Both <c>Array.Fill</c> and <c>Array.Clear</c> are resolved on first demand per compilation,
/// after a candidate passes the syntax filter. <c>Fill</c> requires .NET Core 2.0+. The whole-array
/// <c>Clear(Array)</c> overload is .NET 6+; where it is missing the rule falls back to
/// <c>Clear(array, 0, array.Length)</c>, and then only when the array expression is a repeatable
/// name — an expression that could have side effects is never evaluated twice.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1127ClearOverFillDefaultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string FillMethodName = "Fill";

    /// <summary>The member name the code fix moves the call to.</summary>
    internal const string ClearMethodName = "Clear";

    /// <summary>The receiver type name the syntax gate requires.</summary>
    internal const string ArrayTypeName = "Array";

    /// <summary>The argument count of the whole-array <c>Fill(array, value)</c> overload.</summary>
    internal const int WholeArrayFillArgumentCount = 2;

    /// <summary>The argument count of the ranged <c>Fill(array, value, startIndex, count)</c> overload.</summary>
    internal const int RangedFillArgumentCount = 4;

    /// <summary>The parameter count of the whole-array <c>Clear(Array)</c> overload.</summary>
    private const int WholeArrayClearParameterCount = 1;

    /// <summary>The message argument naming the replacement call.</summary>
    private const string ClearMessageArg = "Array.Clear";

    /// <summary>The metadata name of the array type that hosts Fill and Clear.</summary>
    private const string ArrayMetadataName = "System.Array";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CollectionRules.ClearOverFillDefault);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyCompilationValue<ArrayMethods>(compilation, ResolveArrayMethods),
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Returns whether an invocation has the <c>Array.Fill(array, value, ...)</c> syntax shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the member name is Fill with a recognized argument count and a default-shaped value.</returns>
    internal static bool IsFillDefaultShape(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        return arguments.Count is WholeArrayFillArgumentCount or RangedFillArgumentCount
            && IsFillName(invocation.Expression)
            && IsDefaultValueShape(arguments[1].Expression);
    }

    /// <summary>Returns whether an expression is a literal that can denote an element type's default.</summary>
    /// <param name="expression">The fill value expression.</param>
    /// <returns><see langword="true"/> for <c>default</c>, <c>default(T)</c>, <c>null</c>, <c>false</c>, or the <c>0</c> literal.</returns>
    internal static bool IsDefaultValueShape(ExpressionSyntax expression)
    {
        if (expression is DefaultExpressionSyntax)
        {
            return true;
        }

        if (expression is not LiteralExpressionSyntax literal)
        {
            return false;
        }

        return literal.Kind() switch
        {
            SyntaxKind.DefaultLiteralExpression or SyntaxKind.NullLiteralExpression or SyntaxKind.FalseLiteralExpression => true,
            SyntaxKind.NumericLiteralExpression => literal.Token.ValueText == "0",
            _ => false,
        };
    }

    /// <summary>Returns whether an expression can be evaluated twice without changing behavior.</summary>
    /// <param name="expression">The array expression.</param>
    /// <returns><see langword="true"/> when the expression is a plain name chain with no calls or indexers.</returns>
    internal static bool IsRepeatableExpression(ExpressionSyntax expression)
    {
        var current = expression;
        while (true)
        {
            switch (current)
            {
                case IdentifierNameSyntax or ThisExpressionSyntax or PredefinedTypeSyntax:
                    return true;
                case MemberAccessExpressionSyntax access:
                {
                    current = access.Expression;
                    continue;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>Returns whether the array type exposes the whole-array <c>Clear(Array)</c> overload (.NET 6+).</summary>
    /// <param name="arrayType">The <c>System.Array</c> type in the current compilation.</param>
    /// <returns><see langword="true"/> when the single-parameter Clear exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasWholeArrayClear(INamedTypeSymbol arrayType) =>
        SymbolFacts.HasStaticMethod(arrayType, ClearMethodName, WholeArrayClearParameterCount);

    /// <summary>Reports PSH1127 for an <c>Array.Fill</c> call that writes the element type's default.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="methods">The deferred array method support for the compilation.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyCompilationValue<ArrayMethods> methods)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsFillDefaultShape(invocation))
        {
            return;
        }

        var resolved = methods.Get();
        if (resolved.ArrayType is not { } arrayType)
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == WholeArrayFillArgumentCount
            && !resolved.SupportsWholeArrayClear
            && !IsRepeatableExpression(arguments[0].Expression))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol { IsStatic: true, Name: FillMethodName } fill
            || !SymbolEqualityComparer.Default.Equals(fill.ContainingType, arrayType)
            || !FillsElementDefault(context.SemanticModel, arguments[0].Expression, arguments[1].Expression, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.ClearOverFillDefault,
            invocation.SyntaxTree,
            invocation.Span,
            ClearMessageArg));
    }

    /// <summary>Returns whether the fill value really is the array element type's default.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="arrayArgument">The array argument.</param>
    /// <param name="valueArgument">The fill value argument.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when clearing the array would store the same value.</returns>
    private static bool FillsElementDefault(
        SemanticModel model,
        ExpressionSyntax arrayArgument,
        ExpressionSyntax valueArgument,
        CancellationToken cancellationToken)
    {
        if (valueArgument is DefaultExpressionSyntax || valueArgument.IsKind(SyntaxKind.DefaultLiteralExpression))
        {
            return true;
        }

        if (model.GetTypeInfo(arrayArgument, cancellationToken).Type is not IArrayTypeSymbol { ElementType: { } elementType })
        {
            return false;
        }

        // A null literal is the default only for an element type whose default *is* null; a 0 or
        // false literal only for one whose default is a zeroed value. Getting this backwards is how
        // Fill(new object[4], 0) — a boxed zero, not null — would be silently turned into nulls.
        var defaultIsNull = elementType.IsReferenceType
            || elementType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        return valueArgument.IsKind(SyntaxKind.NullLiteralExpression) ? defaultIsNull : !defaultIsNull;
    }

    /// <summary>Returns whether an invocation's callee names <c>Fill</c> on an <c>Array</c> receiver.</summary>
    /// <param name="callee">The invoked expression.</param>
    /// <returns><see langword="true"/> when the syntax names Array.Fill or a using-static Fill.</returns>
    private static bool IsFillName(ExpressionSyntax callee) =>
        callee switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: FillMethodName } access => TypeNameReceiver.EndsWithTypeName(access.Expression, ArrayTypeName),
            IdentifierNameSyntax { Identifier.ValueText: FillMethodName } => true,
            _ => false,
        };

    /// <summary>Resolves the array type and its Fill and Clear overloads.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <returns>The supported array type and whether whole-array Clear exists.</returns>
    private static ArrayMethods ResolveArrayMethods(Compilation compilation)
    {
        var arrayType = compilation.GetTypeByMetadataName(ArrayMetadataName);
        return arrayType is not null
            && SymbolFacts.HasStaticMethod(arrayType, FillMethodName)
            && SymbolFacts.HasStaticMethod(arrayType, ClearMethodName)
            ? new(arrayType, HasWholeArrayClear(arrayType))
            : new(null, false);
    }

    /// <summary>The immutable array method support published by the deferred resolver.</summary>
    /// <param name="arrayType">The array type, or null when Fill or Clear is unavailable.</param>
    /// <param name="supportsWholeArrayClear">Whether the whole-array Clear overload exists.</param>
    private sealed class ArrayMethods(INamedTypeSymbol? arrayType, bool supportsWholeArrayClear)
    {
        /// <summary>Gets the array type when Fill and Clear are available.</summary>
        public INamedTypeSymbol? ArrayType { get; } = arrayType;

        /// <summary>Gets whether the whole-array Clear overload exists.</summary>
        public bool SupportsWholeArrayClear { get; } = supportsWholeArrayClear;
    }
}
