// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an equality or identity assertion whose two compared operands are the same expression (SST2501), so
/// the assertion cannot fail (or, for a negated assertion, cannot pass) whatever the value is: xUnit
/// <c>Assert.Equal</c>/<c>StrictEqual</c>/<c>Same</c>/<c>NotEqual</c>/<c>NotSame</c>, NUnit
/// <c>Assert.AreEqual</c>/<c>AreSame</c> and <c>Assert.That(x, Is.EqualTo(x))</c>/<c>Is.SameAs(x)</c>, and MSTest
/// <c>Assert.AreEqual</c>/<c>AreSame</c>/<c>AreNotEqual</c>/<c>AreNotSame</c>.
/// </summary>
/// <remarks>
/// <para>
/// The whole rule is gated at compilation start on at least one test framework's <c>Assert</c> type resolving —
/// <c>Xunit.Assert</c>, <c>NUnit.Framework.Assert</c>, <c>NUnit.Framework.Legacy.ClassicAssert</c>, or the MSTest
/// <c>Assert</c>. A project that references none registers no callback and pays nothing.
/// </para>
/// <para>
/// The clean path is syntax only. Every invocation is seen, but all but the equality/identity assertion names are
/// rejected before anything binds; only when the two operands are syntactically equivalent
/// (<see cref="SyntaxFactory.AreEquivalent(SyntaxNode, SyntaxNode, bool)"/>, which ignores trivia) is the call
/// bound to confirm it really is one of the resolved <c>Assert</c> types. A call, object creation, await, or
/// assignment on either side stops the report, because two such expressions need not evaluate to the same value —
/// so only provably stable operands (identifiers, member and element access, literals) are compared.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2501SelfComparisonAssertionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The message tail for a positive assertion, which always passes when it compares an expression with itself.</summary>
    private const string PositiveConsequence = "always passes and verifies nothing";

    /// <summary>The message tail for a negated assertion, which always fails when it compares an expression with itself.</summary>
    private const string NegativeConsequence = "always fails, whatever the operand's value";

    /// <summary>The NUnit constraint-builder equality member name whose argument is compared against the actual value.</summary>
    private const string EqualToConstraintName = "EqualTo";

    /// <summary>The NUnit constraint-builder identity member name.</summary>
    private const string SameAsConstraintName = "SameAs";

    /// <summary>The NUnit constraint-model assertion method whose second argument carries the constraint.</summary>
    private const string ThatMethodName = "That";

    /// <summary>The metadata names of the framework <c>Assert</c> types whose members this rule inspects.</summary>
    private static readonly string[] AssertionHostMetadataNames =
    [
        "Xunit.Assert",
        "NUnit.Framework.Assert",
        "NUnit.Framework.Legacy.ClassicAssert",
        "Microsoft.VisualStudio.TestTools.UnitTesting.Assert",
    ];

    /// <summary>The kind of equality assertion an invocation was recognized as.</summary>
    private enum AssertionShape
    {
        /// <summary>Not an equality or identity assertion.</summary>
        None,

        /// <summary>A positive equality/identity assertion (<c>Equal</c>, <c>StrictEqual</c>, <c>AreEqual</c>, <c>Same</c>, <c>AreSame</c>), which always passes when self-comparing.</summary>
        PositiveEquality,

        /// <summary>A negated equality/identity assertion (<c>NotEqual</c>, <c>NotSame</c>, <c>AreNotEqual</c>, <c>AreNotSame</c>), which always fails when self-comparing.</summary>
        NegativeEquality,

        /// <summary>An <c>Assert.That</c> constraint-model assertion whose <c>EqualTo</c>/<c>SameAs</c> operand may be the actual value.</summary>
        Constraint,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(TestingRules.SelfComparisonAssertion);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Resolves the framework <c>Assert</c> types once, then analyzes each invocation when at least one is present.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var names = AssertionHostMetadataNames;
        var resolved = new INamedTypeSymbol[names.Length];
        var count = 0;
        for (var i = 0; i < names.Length; i++)
        {
            if (context.Compilation.GetTypeByMetadataName(names[i]) is { } type)
            {
                resolved[count++] = type;
            }
        }

        if (count == 0)
        {
            return;
        }

        var hosts = new INamedTypeSymbol[count];
        for (var i = 0; i < count; i++)
        {
            hosts[i] = resolved[i];
        }

        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, hosts), SyntaxKind.InvocationExpression);
    }

    /// <summary>Analyzes one invocation for a self-comparing assertion.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="hosts">The resolved framework <c>Assert</c> types.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol[] hosts)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var shape = Classify(GetInvokedSimpleName(invocation.Expression));
        if (shape == AssertionShape.None)
        {
            return;
        }

        var operand = GetSelfOperand(shape, invocation.ArgumentList.Arguments);
        if (operand is null || !IsStableOperand(operand))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsAssertionHost(method.ContainingType, hosts))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            TestingRules.SelfComparisonAssertion,
            invocation.GetLocation(),
            Consequence(shape)));
    }

    /// <summary>Returns the invoked member's simple name for the supported call shapes.</summary>
    /// <param name="expression">The invocation's expression.</param>
    /// <returns>The invoked name, or <see langword="null"/> for unsupported expression shapes.</returns>
    private static string? GetInvokedSimpleName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Classifies an invoked method name as a positive, negated, or constraint-model equality assertion.</summary>
    /// <param name="name">The invoked method's simple name.</param>
    /// <returns>The assertion shape, or <see cref="AssertionShape.None"/> when the name is not an equality assertion.</returns>
    private static AssertionShape Classify(string? name)
    {
        if (name is null)
        {
            return AssertionShape.None;
        }

        if (IsPositiveEqualityName(name))
        {
            return AssertionShape.PositiveEquality;
        }

        if (IsNegativeEqualityName(name))
        {
            return AssertionShape.NegativeEquality;
        }

        return name == ThatMethodName ? AssertionShape.Constraint : AssertionShape.None;
    }

    /// <summary>Returns whether a name is a positive equality/identity assertion that always passes when self-comparing.</summary>
    /// <param name="name">The invoked method's simple name.</param>
    /// <returns><see langword="true"/> for <c>Equal</c>, <c>StrictEqual</c>, <c>AreEqual</c>, <c>Same</c>, or <c>AreSame</c>.</returns>
    private static bool IsPositiveEqualityName(string name)
        => name is "Equal" or "StrictEqual" or "AreEqual" or "Same" or "AreSame";

    /// <summary>Returns whether a name is a negated equality/identity assertion that always fails when self-comparing.</summary>
    /// <param name="name">The invoked method's simple name.</param>
    /// <returns><see langword="true"/> for <c>NotEqual</c>, <c>NotSame</c>, <c>AreNotEqual</c>, or <c>AreNotSame</c>.</returns>
    private static bool IsNegativeEqualityName(string name)
        => name is "NotEqual" or "NotSame" or "AreNotEqual" or "AreNotSame";

    /// <summary>Returns the shared operand of a self-comparing assertion, or <see langword="null"/> when the call does not compare a value with itself.</summary>
    /// <param name="shape">The recognized assertion shape.</param>
    /// <param name="arguments">The invocation's arguments.</param>
    /// <returns>The operand compared with itself, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? GetSelfOperand(AssertionShape shape, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        if (arguments.Count < 2 || arguments[0].NameColon is not null)
        {
            return null;
        }

        return shape == AssertionShape.Constraint
            ? GetConstraintSelfOperand(arguments)
            : GetPositionalSelfOperand(arguments);
    }

    /// <summary>Returns the shared operand of a two-argument assertion whose first two arguments are the same expression.</summary>
    /// <param name="arguments">The invocation's arguments.</param>
    /// <returns>The first operand when the first two positional arguments are equivalent; otherwise <see langword="null"/>.</returns>
    private static ExpressionSyntax? GetPositionalSelfOperand(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        if (arguments[1].NameColon is not null)
        {
            return null;
        }

        var first = arguments[0].Expression;
        return AreSameExpression(first, arguments[1].Expression) ? first : null;
    }

    /// <summary>Returns the actual-value operand of an <c>Assert.That(x, Is.EqualTo(x))</c> / <c>Is.SameAs(x)</c> call when it compares a value with itself.</summary>
    /// <param name="arguments">The invocation's arguments.</param>
    /// <returns>The actual operand when it equals the constraint's expected operand; otherwise <see langword="null"/>.</returns>
    private static ExpressionSyntax? GetConstraintSelfOperand(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        if (arguments[1].Expression is not InvocationExpressionSyntax constraint)
        {
            return null;
        }

        var constraintName = GetInvokedSimpleName(constraint.Expression);
        if (constraintName is not (EqualToConstraintName or SameAsConstraintName))
        {
            return null;
        }

        var constraintArguments = constraint.ArgumentList.Arguments;
        if (constraintArguments.Count != 1 || constraintArguments[0].NameColon is not null)
        {
            return null;
        }

        var actual = arguments[0].Expression;
        return AreSameExpression(actual, constraintArguments[0].Expression) ? actual : null;
    }

    /// <summary>Returns whether two expressions are the same expression, ignoring trivia.</summary>
    /// <param name="left">The first expression.</param>
    /// <param name="right">The second expression.</param>
    /// <returns><see langword="true"/> when the two are syntactically equivalent.</returns>
    private static bool AreSameExpression(ExpressionSyntax left, ExpressionSyntax right)
        => SyntaxFactory.AreEquivalent(left, right, topLevel: false);

    /// <summary>Returns whether an operand is provably value-stable, so comparing it with itself is guaranteed regardless of value.</summary>
    /// <param name="operand">The operand to classify.</param>
    /// <returns><see langword="false"/> when the operand or any descendant can have a side effect or vary between evaluations.</returns>
    private static bool IsStableOperand(ExpressionSyntax operand)
    {
        if (IsVolatileNode(operand))
        {
            return false;
        }

        StabilityScan scan = default;
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, StabilityScan>(operand, ref scan, VisitForVolatility);
        return !scan.FoundVolatile;
    }

    /// <summary>Records the first side-effecting or non-deterministic descendant and stops the walk.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/> once such a node is found, which stops the walk.</returns>
    private static bool VisitForVolatility(SyntaxNode node, ref StabilityScan scan)
    {
        if (!IsVolatileNode(node))
        {
            return true;
        }

        scan.FoundVolatile = true;
        return false;
    }

    /// <summary>Returns whether a node can have a side effect or evaluate to a different value on a second read.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for a call, an object/array creation, a mutation, a lambda, or a query.</returns>
    private static bool IsVolatileNode(SyntaxNode node)
        => IsCallOrAwait(node) || IsCreation(node) || IsMutation(node) || IsClosureOrQuery(node);

    /// <summary>Returns whether a node is a method/delegate call or an await.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for an invocation or an await expression.</returns>
    private static bool IsCallOrAwait(SyntaxNode node)
        => node is InvocationExpressionSyntax or AwaitExpressionSyntax;

    /// <summary>Returns whether a node allocates a fresh object, array, or anonymous instance.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for any object, array, or stackalloc creation.</returns>
    private static bool IsCreation(SyntaxNode node)
        => node is ObjectCreationExpressionSyntax
            or ImplicitObjectCreationExpressionSyntax
            or AnonymousObjectCreationExpressionSyntax
            or ArrayCreationExpressionSyntax
            or ImplicitArrayCreationExpressionSyntax
            or StackAllocArrayCreationExpressionSyntax
            or ImplicitStackAllocArrayCreationExpressionSyntax;

    /// <summary>Returns whether a node writes state through an assignment, increment, or decrement.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for an assignment or a pre/post increment/decrement.</returns>
    private static bool IsMutation(SyntaxNode node) => node switch
    {
        AssignmentExpressionSyntax => true,
        PrefixUnaryExpressionSyntax prefix => prefix.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression,
        PostfixUnaryExpressionSyntax postfix => postfix.Kind() is SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression,
        _ => false,
    };

    /// <summary>Returns whether a node introduces a closure, a query, or a ref alias.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for a lambda, an anonymous method, a query, or a ref expression.</returns>
    private static bool IsClosureOrQuery(SyntaxNode node)
        => node is SimpleLambdaExpressionSyntax
            or ParenthesizedLambdaExpressionSyntax
            or AnonymousMethodExpressionSyntax
            or QueryExpressionSyntax
            or RefExpressionSyntax;

    /// <summary>Returns the message tail describing the consequence of the self-comparison for the recognized shape.</summary>
    /// <param name="shape">The recognized assertion shape.</param>
    /// <returns>The negated tail for a negated assertion; otherwise the positive tail.</returns>
    private static string Consequence(AssertionShape shape)
        => shape == AssertionShape.NegativeEquality ? NegativeConsequence : PositiveConsequence;

    /// <summary>Returns whether a bound method's containing type is one of the resolved framework <c>Assert</c> types.</summary>
    /// <param name="containingType">The bound method's containing type.</param>
    /// <param name="hosts">The resolved framework <c>Assert</c> types.</param>
    /// <returns><see langword="true"/> when the call belongs to a framework <c>Assert</c>.</returns>
    private static bool IsAssertionHost(INamedTypeSymbol containingType, INamedTypeSymbol[] hosts)
    {
        for (var i = 0; i < hosts.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(containingType, hosts[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The state threaded through an operand's stability scan.</summary>
    private record struct StabilityScan
    {
        /// <summary>Gets or sets a value indicating whether a side-effecting or non-deterministic node was found.</summary>
        public bool FoundVolatile { get; set; }
    }
}
