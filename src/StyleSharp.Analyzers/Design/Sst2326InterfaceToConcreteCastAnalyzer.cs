// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports narrowing a value whose static type is an interface down to a concrete class that implements it
/// (SST2326). The three forms covered are an explicit cast <c>(Concrete)value</c>, an <c>value as Concrete</c>,
/// and an <c>value is Concrete</c> type test — with or without a declared variable. Each reaches past the
/// abstraction to a named implementation, so substituting a different implementation of the interface makes the
/// cast throw or the type test silently pick the wrong branch.
/// </summary>
/// <remarks>
/// <para>
/// Precision is kept high by binding only after a cheap syntactic dispatch on the node kind, then requiring the
/// operand's static type to be a genuine interface (<see cref="INamedTypeSymbol"/> with
/// <see cref="TypeKind.Interface"/>) and the target to be a non-abstract class that actually appears in the
/// operand interface's implementers — the target's <see cref="ITypeSymbol.AllInterfaces"/> must contain the
/// operand interface. A type parameter, <c>object</c>, or <c>dynamic</c> operand is not a named interface and is
/// skipped; an interface, <c>object</c>, struct, or abstract-class target is skipped; and a target that does not
/// implement the interface — an unrelated narrowing the compiler already rejects or leaves for a subclass — is
/// left alone.
/// </para>
/// <para>
/// The clean path allocates nothing: the syntactic dispatch rejects every node that is not one of the four
/// narrowing shapes (an <c>is</c> pattern that is not a declaration pattern binds nothing), and display strings
/// for the message are built only once a violation is confirmed.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2326InterfaceToConcreteCastAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.InterfaceToConcreteCast);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.CastExpression,
            SyntaxKind.AsExpression,
            SyntaxKind.IsExpression,
            SyntaxKind.IsPatternExpression);
    }

    /// <summary>Reports one narrowing of an interface reference to a concrete implementation type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!TryGetOperandAndTarget(context.Node, out var operand, out var targetType))
        {
            return;
        }

        var semanticModel = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        // The operand's declared static type must be a genuine interface. A type parameter, object, or dynamic
        // is not an INamedTypeSymbol interface and drops out here, so those shapes never reach the target check.
        if (semanticModel.GetTypeInfo(operand, cancellationToken).Type is not INamedTypeSymbol { TypeKind: TypeKind.Interface } interfaceType)
        {
            return;
        }

        // The target must be a concrete (non-abstract) class: an interface, struct, enum, object, or abstract
        // base is not a specific implementation to couple to.
        if (semanticModel.GetTypeInfo(targetType, cancellationToken).Type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } concreteType)
        {
            return;
        }

        // Only report when the concrete type genuinely implements the interface. An unrelated narrowing either
        // fails to compile or is left open for a subclass — not this rule's business, and reporting it would be noise.
        if (!ImplementsInterface(concreteType, interfaceType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            DesignRules.InterfaceToConcreteCast,
            targetType.GetLocation(),
            interfaceType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            concreteType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    /// <summary>Splits a narrowing node into the operand being narrowed and the target type syntax, on syntax alone.</summary>
    /// <param name="node">The cast, <c>as</c>, <c>is</c>, or <c>is</c>-pattern node.</param>
    /// <param name="operand">The expression whose static type is inspected.</param>
    /// <param name="targetType">The target type syntax the operand is narrowed to.</param>
    /// <returns><see langword="true"/> when the node is a narrowing shape this rule inspects.</returns>
    /// <remarks>
    /// A bare <c>value is Concrete</c> parses as an <see cref="BinaryExpressionSyntax"/> (<c>IsExpression</c>),
    /// not an <c>is</c> pattern; the pattern form is reached only by <c>value is Concrete name</c>, a declaration
    /// pattern. Any other pattern — <c>is not</c>, a constant, a recursive pattern — carries no single target type
    /// to narrow to and is rejected here without binding.
    /// </remarks>
    private static bool TryGetOperandAndTarget(SyntaxNode node, out ExpressionSyntax operand, out TypeSyntax targetType)
    {
        switch (node)
        {
            case CastExpressionSyntax cast:
            {
                operand = cast.Expression;
                targetType = cast.Type;
                return true;
            }

            case BinaryExpressionSyntax binary:
            {
                operand = binary.Left;
                targetType = (TypeSyntax)binary.Right;
                return true;
            }

            case IsPatternExpressionSyntax { Pattern: DeclarationPatternSyntax declaration } isPattern:
            {
                operand = isPattern.Expression;
                targetType = declaration.Type;
                return true;
            }

            default:
            {
                operand = null!;
                targetType = null!;
                return false;
            }
        }
    }

    /// <summary>Returns whether a concrete type implements a specific interface.</summary>
    /// <param name="concreteType">The concrete class.</param>
    /// <param name="interfaceType">The interface the operand is statically typed as.</param>
    /// <returns><see langword="true"/> when the interface is among the concrete type's implemented interfaces.</returns>
    private static bool ImplementsInterface(INamedTypeSymbol concreteType, INamedTypeSymbol interfaceType)
    {
        var interfaces = concreteType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], interfaceType))
            {
                return true;
            }
        }

        return false;
    }
}
