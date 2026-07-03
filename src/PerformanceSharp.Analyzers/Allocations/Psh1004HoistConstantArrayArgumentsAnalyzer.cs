// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests hoisting a constant inline array argument into a <c>static readonly</c> field
/// (PSH1004). A creation qualifies when it is an explicit or implicit array creation passed
/// (through an argument) to an invocation or object creation, its initializer contains at
/// least one expression, and every element is a compile-time constant. Zero-element
/// initializers are PSH1001's territory, and attribute arguments are skipped because they are
/// already compile-time constants. The clean path is syntax-only: elements are prefiltered by
/// shape (literals, prefix-unary literals, identifiers, and member accesses) before any
/// semantic constant binding happens.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1004HoistConstantArrayArgumentsAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.HoistConstantArrayArguments);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeCreation, SyntaxKind.ArrayCreationExpression, SyntaxKind.ImplicitArrayCreationExpression);
    }

    /// <summary>Extracts the initializer of a syntax-level constant array argument candidate.</summary>
    /// <param name="creation">The explicit or implicit array creation to inspect.</param>
    /// <param name="initializer">The creation's initializer when the candidate shape matches.</param>
    /// <returns><see langword="true"/> when the creation is an argument whose elements all look constant.</returns>
    internal static bool TryGetCandidateInitializer(ExpressionSyntax creation, out InitializerExpressionSyntax? initializer)
    {
        initializer = GetInitializer(creation);
        if (initializer is { Expressions.Count: > 0 }
            && IsArgumentToCall(creation)
            && AllElementsLookConstant(initializer)
            && !IsInsideAttributeArgument(creation))
        {
            return true;
        }

        initializer = null;
        return false;
    }

    /// <summary>Returns whether every initializer element binds to a compile-time constant.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="initializer">The initializer whose elements to verify.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when every element has a constant value.</returns>
    internal static bool AreAllElementsConstant(SemanticModel model, InitializerExpressionSyntax initializer, CancellationToken cancellationToken)
    {
        var expressions = initializer.Expressions;
        for (var i = 0; i < expressions.Count; i++)
        {
            if (!model.GetConstantValue(expressions[i], cancellationToken).HasValue)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reports PSH1004 for a constant inline array argument that is reallocated on every call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ExpressionSyntax)context.Node;
        if (!TryGetCandidateInitializer(creation, out var initializer)
            || !AreAllElementsConstant(context.SemanticModel, initializer!, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.HoistConstantArrayArguments,
            creation.SyntaxTree,
            creation.Span));
    }

    /// <summary>Gets the initializer of an explicit or implicit array creation.</summary>
    /// <param name="creation">The creation expression.</param>
    /// <returns>The initializer, or <see langword="null"/> when the creation has none.</returns>
    private static InitializerExpressionSyntax? GetInitializer(ExpressionSyntax creation)
        => creation switch
        {
            ArrayCreationExpressionSyntax array => array.Initializer,
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer,
            _ => null
        };

    /// <summary>Returns whether every initializer element could be a constant by shape alone.</summary>
    /// <param name="initializer">The initializer whose elements to prefilter.</param>
    /// <returns><see langword="true"/> when every element passes the cheap syntax prefilter.</returns>
    private static bool AllElementsLookConstant(InitializerExpressionSyntax initializer)
    {
        var expressions = initializer.Expressions;
        for (var i = 0; i < expressions.Count; i++)
        {
            if (!CouldBeConstantElement(expressions[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an element's syntax shape can denote a compile-time constant.</summary>
    /// <param name="expression">The initializer element to inspect.</param>
    /// <returns><see langword="true"/> for literals, prefix-unary literals, identifiers, and member accesses.</returns>
    private static bool CouldBeConstantElement(ExpressionSyntax expression)
        => expression switch
        {
            LiteralExpressionSyntax => true,
            PrefixUnaryExpressionSyntax prefix => prefix.Operand is LiteralExpressionSyntax,
            IdentifierNameSyntax => true,
            MemberAccessExpressionSyntax => true,
            _ => false
        };

    /// <summary>Returns whether the creation is an argument to an invocation or object creation.</summary>
    /// <param name="creation">The creation expression.</param>
    /// <returns><see langword="true"/> when the creation sits in an argument list of a call.</returns>
    private static bool IsArgumentToCall(ExpressionSyntax creation)
        => creation.Parent is ArgumentSyntax
        {
            Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax }
        };

    /// <summary>Returns whether a node sits inside an attribute argument.</summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns><see langword="true"/> when an <see cref="AttributeArgumentSyntax"/> ancestor is found before any statement or member.</returns>
    private static bool IsInsideAttributeArgument(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is AttributeArgumentSyntax)
            {
                return true;
            }

            if (current is StatementSyntax or MemberDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }
}
