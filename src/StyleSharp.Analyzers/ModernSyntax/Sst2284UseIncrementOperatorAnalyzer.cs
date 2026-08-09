// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a standalone statement that steps a value by one through a compound assignment (SST2284):
/// <c>i += 1;</c> and <c>i -= 1;</c> become <c>i++;</c> and <c>i--;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Only an <see cref="ExpressionStatementSyntax"/> is considered, so the assignment's value is discarded
/// and the prefix/postfix distinction cannot be observed.
/// </para>
/// <para>
/// The clean path is syntactic: a compound assignment whose right operand is not the literal <c>1</c>, or
/// whose target is not a side-effect-free name, is dropped before the semantic model is consulted. The type
/// is asked for its increment operator only for a candidate that already looks rewritable, because a type
/// may overload <c>+</c> without overloading <c>++</c> and the shorter form would then not compile.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2284UseIncrementOperatorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the user-defined increment operator.</summary>
    private const string IncrementOperatorName = "op_Increment";

    /// <summary>The metadata name of the user-defined decrement operator.</summary>
    private const string DecrementOperatorName = "op_Decrement";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(ModernSyntaxRules.UseIncrementOperator);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AddAssignmentExpression, SyntaxKind.SubtractAssignmentExpression);
    }

    /// <summary>Returns whether a type has the stepping operator the rewrite would use.</summary>
    /// <param name="type">The assignment target's type, or <see langword="null"/> when it did not bind.</param>
    /// <param name="increment"><see langword="true"/> for <c>++</c>, <see langword="false"/> for <c>--</c>.</param>
    /// <returns><see langword="true"/> when <c>++</c> or <c>--</c> is defined for the type.</returns>
    /// <remarks>
    /// The built-in numeric types, <c>char</c>, every enum, and every pointer have both operators. A nullable
    /// value type lifts whichever its underlying type has. Anything else must declare the operator itself.
    /// </remarks>
    internal static bool SupportsStepping(ITypeSymbol? type, bool increment)
    {
        if (type is null || type.TypeKind == TypeKind.Dynamic)
        {
            return false;
        }

        if (type is IPointerTypeSymbol)
        {
            return true;
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        return type.TypeKind == TypeKind.Enum
            || IsSteppableSpecialType(type.SpecialType)
            || DeclaresSteppingOperator(type, increment);
    }

    /// <summary>Reports one compound assignment that steps its target by one.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (assignment.Parent is not ExpressionStatementSyntax
            || assignment.Right is not LiteralExpressionSyntax { Token.Value: 1 }
            || !CompoundAssignmentOperators.IsSideEffectFreeTarget(assignment.Left))
        {
            return;
        }

        var increment = assignment.IsKind(SyntaxKind.AddAssignmentExpression);
        var type = context.SemanticModel.GetTypeInfo(assignment.Left, context.CancellationToken).Type;
        if (!SupportsStepping(type, increment))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.UseIncrementOperator,
            assignment.SyntaxTree,
            assignment.Span,
            increment ? "++" : "--",
            increment ? "+=" : "-="));
    }

    /// <summary>Returns whether a special type is one of the built-ins that has <c>++</c> and <c>--</c>.</summary>
    /// <param name="specialType">The candidate special type.</param>
    /// <returns><see langword="true"/> for <c>char</c> and the built-in numeric types.</returns>
    /// <remarks>
    /// <see cref="SpecialType"/> lists <c>char</c> through <c>double</c> contiguously — char, the eight
    /// integer types, decimal, float, double — which is exactly the set with built-in stepping operators, so
    /// the test is one range comparison rather than a twelve-arm switch. <c>bool</c> sits just below the
    /// range and <c>string</c> just above it, so neither can slip in. <c>IntPtr</c> and <c>UIntPtr</c> are
    /// deliberately outside it: they gained <c>++</c> only when they became the <c>nint</c>/<c>nuint</c>
    /// types, so they fall through to the operator lookup that asks the referenced framework.
    /// </remarks>
    private static bool IsSteppableSpecialType(SpecialType specialType)
        => specialType is >= SpecialType.System_Char and <= SpecialType.System_Double;

    /// <summary>Returns whether a type declares the user-defined stepping operator the rewrite would use.</summary>
    /// <param name="type">The assignment target's type.</param>
    /// <param name="increment"><see langword="true"/> for <c>++</c>, <see langword="false"/> for <c>--</c>.</param>
    /// <returns><see langword="true"/> when the operator is declared on the type.</returns>
    private static bool DeclaresSteppingOperator(ITypeSymbol type, bool increment)
    {
        var members = type.GetMembers(increment ? IncrementOperatorName : DecrementOperatorName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator })
            {
                return true;
            }
        }

        return false;
    }
}
