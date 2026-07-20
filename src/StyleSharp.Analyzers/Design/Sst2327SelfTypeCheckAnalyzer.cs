// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports code where an instance tests its own runtime type against a specific named class (SST2327):
/// <c>this is Derived</c>, <c>this is Derived name</c>, <c>this as Derived</c>, or
/// <c>this.GetType() == typeof(Derived)</c> (and the <c>!=</c> / reversed forms). A base type that branches on
/// which of its own subtypes it really is defeats polymorphism — the behaviour that varies by type belongs in a
/// virtual member the base declares and each subtype overrides.
/// </summary>
/// <remarks>
/// <para>
/// The clean path is purely syntactic. A type-test (<c>is</c>/<c>as</c>) or a declaration pattern is examined
/// only when its operand is literally <c>this</c>; an equality is examined only when one side is a
/// <c>typeof(...)</c> and the other is a zero-argument <c>this.GetType()</c> call. The semantic model is
/// consulted once, and only after that syntactic shape matches, to bind the tested type.
/// </para>
/// <para>
/// The tested type is reported only when it binds to a named class. A type parameter (<c>this is T</c>), an
/// interface capability check (<c>this is IDisposable</c>), a struct, and an unresolved name are all left alone.
/// A test of some other value (<c>other is Derived</c>), a negated or property pattern (<c>this is not Derived</c>,
/// <c>this is { }</c>), and a bare <c>GetType()</c> call without an explicit <c>this</c> receiver are not reported.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2327SelfTypeCheckAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the runtime type-of-instance accessor whose result the equality form compares.</summary>
    private const string GetTypeMethodName = "GetType";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.SelfTypeCheck);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeIsPattern, SyntaxKind.IsPatternExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeBinary,
            SyntaxKind.IsExpression,
            SyntaxKind.AsExpression,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression);
    }

    /// <summary>Analyzes a declaration pattern (<c>this is Derived name</c>) for a self type-check.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeIsPattern(SyntaxNodeAnalysisContext context)
    {
        var pattern = (IsPatternExpressionSyntax)context.Node;
        if (pattern.Expression is not ThisExpressionSyntax || pattern.Pattern is not DeclarationPatternSyntax declaration)
        {
            return;
        }

        ReportIfNamedClass(context, declaration.Type);
    }

    /// <summary>Analyzes an <c>is</c>/<c>as</c> test or a <c>GetType()</c>/<c>typeof</c> equality for a self type-check.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeBinary(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        var testedType = binary.Kind() switch
        {
            SyntaxKind.IsExpression or SyntaxKind.AsExpression => SelfTestOperandType(binary),
            _ => SelfGetTypeComparisonType(binary),
        };

        if (testedType is null)
        {
            return;
        }

        ReportIfNamedClass(context, testedType);
    }

    /// <summary>Returns the tested type of an <c>is</c>/<c>as</c> whose operand is <c>this</c>, or <see langword="null"/>.</summary>
    /// <param name="binary">The <c>is</c> or <c>as</c> expression.</param>
    /// <returns>The right-hand type when the left operand is <c>this</c>; otherwise <see langword="null"/>.</returns>
    private static TypeSyntax? SelfTestOperandType(BinaryExpressionSyntax binary)
        => binary.Left is ThisExpressionSyntax ? binary.Right as TypeSyntax : null;

    /// <summary>Returns the tested type of a <c>this.GetType() == typeof(T)</c> comparison, in either order, or <see langword="null"/>.</summary>
    /// <param name="binary">The equality or inequality expression.</param>
    /// <returns>The <c>typeof</c> operand's type when the other side is <c>this.GetType()</c>; otherwise <see langword="null"/>.</returns>
    private static TypeSyntax? SelfGetTypeComparisonType(BinaryExpressionSyntax binary)
        => (TypeOfOperandType(binary.Left), TypeOfOperandType(binary.Right)) switch
        {
            ({ } leftType, _) when IsThisGetTypeCall(binary.Right) => leftType,
            (_, { } rightType) when IsThisGetTypeCall(binary.Left) => rightType,
            _ => null,
        };

    /// <summary>Returns the operand type of a <c>typeof(...)</c> expression, or <see langword="null"/> when the node is not one.</summary>
    /// <param name="expression">The candidate <c>typeof</c> expression.</param>
    /// <returns>The type inside <c>typeof(...)</c>, or <see langword="null"/>.</returns>
    private static TypeSyntax? TypeOfOperandType(ExpressionSyntax expression)
        => expression is TypeOfExpressionSyntax typeOf ? typeOf.Type : null;

    /// <summary>Returns whether an expression is a zero-argument <c>this.GetType()</c> call.</summary>
    /// <param name="expression">The candidate invocation.</param>
    /// <returns><see langword="true"/> for <c>this.GetType()</c> with an explicit <c>this</c> receiver and no arguments.</returns>
    private static bool IsThisGetTypeCall(ExpressionSyntax expression)
        => expression is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Expression: ThisExpressionSyntax,
                Name: IdentifierNameSyntax { Identifier.ValueText: GetTypeMethodName },
            },
            ArgumentList.Arguments.Count: 0,
        };

    /// <summary>Binds the tested type and reports the enclosing expression when it names a class.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="testedType">The syntactic type the instance is tested against.</param>
    private static void ReportIfNamedClass(SyntaxNodeAnalysisContext context, TypeSyntax testedType)
    {
        // Bind only now, on the rare node that syntactically tests 'this' against a named type. An interface
        // (capability check), a struct, a type parameter, and an unresolved name are all left alone.
        if (context.SemanticModel.GetSymbolInfo(testedType, context.CancellationToken).Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class } named)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(DesignRules.SelfTypeCheck, context.Node.GetLocation(), named.Name));
    }
}
