// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a write through a <c>readonly</c> field whose type is a type parameter with no reference-type
/// constraint (SST2421). When the type argument is a struct, reading the readonly field yields a defensive
/// copy, so a property assignment or a mutating-method call on it changes the copy and is silently lost.
/// </summary>
/// <remarks>
/// The clean path resolves nothing: it rejects every write whose target does not root at a plain field
/// reference before binding, then bails unless that field is a <c>readonly</c> field of an unconstrained (or
/// struct-constrained) type parameter.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2421ReadonlyGenericFieldWriteAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.ReadonlyGenericFieldWrite);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports a property or field assignment through a readonly generic field.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        var receiver = assignment.Left switch
        {
            MemberAccessExpressionSyntax member => member.Expression,
            ElementAccessExpressionSyntax element => element.Expression,
            _ => null,
        };

        if (receiver is null || !TryGetReadonlyGenericField(context, receiver, out var name))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.ReadonlyGenericFieldWrite, assignment.GetLocation(), name));
    }

    /// <summary>Reports a mutating-method call through a readonly generic field.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || !TryGetReadonlyGenericField(context, member.Expression, out var name))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method || !Mutates(method))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.ReadonlyGenericFieldWrite, invocation.GetLocation(), name));
    }

    /// <summary>Returns whether a method call could mutate the receiver.</summary>
    /// <param name="method">The resolved method.</param>
    /// <returns><see langword="true"/> for a non-readonly, non-static instance method that is not an object member.</returns>
    private static bool Mutates(IMethodSymbol method)
        => method is { IsStatic: false, IsReadOnly: false }
            && method.ContainingType?.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum);

    /// <summary>Returns whether a receiver is a readonly field of an unconstrained type parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="receiver">The receiver expression.</param>
    /// <param name="name">The field's name.</param>
    /// <returns><see langword="true"/> when a write through the receiver lands on a copy.</returns>
    private static bool TryGetReadonlyGenericField(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver, out string name)
    {
        name = string.Empty;
        if (receiver is not IdentifierNameSyntax and not MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax })
        {
            return false;
        }

        if (context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol is not IFieldSymbol { IsReadOnly: true } field
            || field.Type is not ITypeParameterSymbol { HasReferenceTypeConstraint: false })
        {
            return false;
        }

        name = field.Name;
        return true;
    }
}
