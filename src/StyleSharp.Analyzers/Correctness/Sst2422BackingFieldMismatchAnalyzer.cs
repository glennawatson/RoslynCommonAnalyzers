// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a property whose getter returns a different instance field than its setter writes (SST2422), so a
/// value assigned through the property can never be read back through it.
/// </summary>
/// <remarks>
/// This is a proof, not a name heuristic: the getter's single field read and the setter's single
/// assignment-from-<c>value</c> are resolved to two field symbols, and reported only when they differ. The
/// setter may validate or raise a change notification; the getter may be any body that reduces to one field
/// read. The clean path never binds an auto-property, an expression-bodied read-only property, or an
/// interface property.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2422BackingFieldMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property naming the field the setter writes.</summary>
    internal const string SetterFieldKey = "SetterField";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.BackingFieldMismatch);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Reports one property whose accessors use different fields.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (property.AccessorList is not { } accessors
            || GetAccessor(accessors, SyntaxKind.GetAccessorDeclaration) is not { } getter
            || GetSetter(accessors) is not { } setter)
        {
            return;
        }

        if (GetterFieldRead(getter) is not { } getterRead || SetterFieldAssigned(setter) is not { } setterWrite)
        {
            return;
        }

        var getterField = ResolveInstanceField(context, getterRead);
        var setterField = ResolveInstanceField(context, setterWrite);
        if (getterField is null || setterField is null || SymbolEqualityComparer.Default.Equals(getterField, setterField))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(SetterFieldKey, setterField.Name);
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.BackingFieldMismatch,
            property.SyntaxTree,
            property.Identifier.Span,
            properties,
            getterField.Name,
            setterField.Name));
    }

    /// <summary>Finds an accessor of a given kind that has a body.</summary>
    /// <param name="accessors">The property's accessor list.</param>
    /// <param name="kind">The accessor kind.</param>
    /// <returns>The accessor, or <see langword="null"/>.</returns>
    private static AccessorDeclarationSyntax? GetAccessor(AccessorListSyntax accessors, SyntaxKind kind)
    {
        var list = accessors.Accessors;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].IsKind(kind) && HasBody(list[i]))
            {
                return list[i];
            }
        }

        return null;
    }

    /// <summary>Finds a <c>set</c> or <c>init</c> accessor that has a body.</summary>
    /// <param name="accessors">The property's accessor list.</param>
    /// <returns>The accessor, or <see langword="null"/>.</returns>
    private static AccessorDeclarationSyntax? GetSetter(AccessorListSyntax accessors)
        => GetAccessor(accessors, SyntaxKind.SetAccessorDeclaration) ?? GetAccessor(accessors, SyntaxKind.InitAccessorDeclaration);

    /// <summary>Returns whether an accessor has a block or expression body.</summary>
    /// <param name="accessor">The accessor.</param>
    /// <returns><see langword="true"/> when the accessor is not an auto-accessor.</returns>
    private static bool HasBody(AccessorDeclarationSyntax accessor) => accessor.Body is not null || accessor.ExpressionBody is not null;

    /// <summary>Gets the single field a getter reads, when its body reduces to one.</summary>
    /// <param name="getter">The get accessor.</param>
    /// <returns>The field-read expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? GetterFieldRead(AccessorDeclarationSyntax getter)
    {
        if (getter.ExpressionBody is { Expression: { } expression })
        {
            return AsFieldReference(expression);
        }

        return getter.Body is { Statements: [ReturnStatementSyntax { Expression: { } returned }] } ? AsFieldReference(returned) : null;
    }

    /// <summary>Gets the single field a setter assigns from <c>value</c>, when there is exactly one.</summary>
    /// <param name="setter">The set accessor.</param>
    /// <returns>The assigned field expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? SetterFieldAssigned(AccessorDeclarationSyntax setter)
    {
        if (setter.ExpressionBody is { Expression: AssignmentExpressionSyntax single })
        {
            return ValueAssignmentTarget(single);
        }

        if (setter.Body is not { } body)
        {
            return null;
        }

        ExpressionSyntax? found = null;
        var count = 0;
        var statements = body.Statements;
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i] is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
                && ValueAssignmentTarget(assignment) is { } target)
            {
                count++;
                found = target;
            }
        }

        return count == 1 ? found : null;
    }

    /// <summary>Gets the field an assignment writes <c>value</c> to, when that is its shape.</summary>
    /// <param name="assignment">The assignment.</param>
    /// <returns>The assigned field expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? ValueAssignmentTarget(AssignmentExpressionSyntax assignment)
        => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && assignment.Right is IdentifierNameSyntax { Identifier.ValueText: "value" }
                ? AsFieldReference(assignment.Left)
                : null;

    /// <summary>Reduces an expression to a plain field reference, if it is one.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The field-reference expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? AsFieldReference(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } member => member,
        _ => null,
    };

    /// <summary>Resolves a field reference to an instance field symbol.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="expression">The field-reference expression.</param>
    /// <returns>The instance field symbol, or <see langword="null"/>.</returns>
    private static IFieldSymbol? ResolveInstanceField(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        => context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol is IFieldSymbol { IsStatic: false } field ? field : null;
}
