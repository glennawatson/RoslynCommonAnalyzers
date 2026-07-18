// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a chain of nested conditional (<c>?:</c>) expressions that all compare the same value
/// against constants (SST2246): <c>x == 1 ? a : x == 2 ? b : c</c> is a switch expression wearing
/// conditional syntax and reads more clearly as <c>x switch { 1 =&gt; a, 2 =&gt; b, _ =&gt; c }</c>.
/// </summary>
/// <remarks>
/// The clean path is pure syntax: the outer conditional must test a simple identifier against a
/// right-hand operand with <c>==</c>, and its else branch must be another conditional testing the
/// same identifier. Only when that shape matches does the rule bind — the identifier must be a
/// side-effect-free local, parameter, or non-const field of a type whose <c>==</c> agrees with a
/// constant pattern (an enum or an integral/char/string type); every else-branch test must compare
/// that same identifier against a distinct compile-time constant; and the rewritten switch
/// expression must bind to a type the original chain already converted to. A chain written with
/// parentheses around the else branch, one that mixes subjects, one that repeats a constant, or one
/// whose rewrite would not bind is left alone. Reported only from C# 8, where switch expressions
/// exist.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2246ChainedConditionalToSwitchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 8 language-version value.</summary>
    private const int CSharp8 = 800;

    /// <summary>The smallest arm count worth a switch: two constant arms and a default.</summary>
    private const int MinimumArmCount = 3;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.ConvertChainedConditionalToSwitch);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConditionalExpression);
    }

    /// <summary>Returns whether a conditional heads a same-subject equality chain of at least two tests.</summary>
    /// <param name="conditional">The conditional to inspect.</param>
    /// <returns><see langword="true"/> when the shape is a chain head worth binding.</returns>
    /// <remarks>Pure syntax; never binds a symbol so the no-diagnostic path stays allocation-free.</remarks>
    internal static bool IsChainHead(ConditionalExpressionSyntax conditional)
        => !IsChainContinuation(conditional)
            && TryGetSubject(conditional.Condition, out var subject)
            && conditional.WhenFalse is ConditionalExpressionSyntax next
            && IsSameSubjectChain(next, subject);

    /// <summary>Builds the switch expression a chain head rewrites to, when the rewrite is safe.</summary>
    /// <param name="conditional">The chain head conditional.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="switchExpression">The rewritten switch expression, when the rewrite is safe.</param>
    /// <returns><see langword="true"/> when the chain converts to an equivalent switch expression.</returns>
    internal static bool TryBuildSwitchExpression(
        ConditionalExpressionSyntax conditional,
        SemanticModel model,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out SwitchExpressionSyntax? switchExpression)
    {
        switchExpression = null;

        if (!TryResolveSubject(conditional, model, cancellationToken, out var subject)
            || !TryCollectArms(conditional, subject, model, cancellationToken, out var arms)
            || arms.Count < MinimumArmCount)
        {
            return false;
        }

        var candidate = SyntaxFactory.SwitchExpression(subject.WithoutTrivia(), arms);
        if (!RewriteKeepsType(conditional, candidate, model, cancellationToken))
        {
            return false;
        }

        switchExpression = candidate;
        return true;
    }

    /// <summary>Reports a same-subject constant conditional chain that can be a switch expression.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var conditional = (ConditionalExpressionSyntax)context.Node;
        if (conditional.SyntaxTree.Options is not CSharpParseOptions options
            || (int)options.LanguageVersion < CSharp8
            || !IsChainHead(conditional)
            || !TryBuildSwitchExpression(conditional, context.SemanticModel, context.CancellationToken, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.ConvertChainedConditionalToSwitch, conditional.GetLocation()));
    }

    /// <summary>Resolves the chain's tested value when it is a fixable subject.</summary>
    /// <param name="conditional">The chain head conditional.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="subject">The tested identifier, when it is a fixable subject.</param>
    /// <returns><see langword="true"/> for a side-effect-free variable of a constant-pattern type.</returns>
    private static bool TryResolveSubject(
        ConditionalExpressionSyntax conditional,
        SemanticModel model,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out IdentifierNameSyntax? subject)
    {
        subject = null;
        if (!TryGetSubject(conditional.Condition, out var candidate)
            || model.GetSymbolInfo(candidate, cancellationToken).Symbol is not { } symbol
            || !IsSideEffectFreeVariable(symbol)
            || model.GetTypeInfo(candidate, cancellationToken).Type is not { } subjectType
            || !MatchesConstantPattern(subjectType))
        {
            return false;
        }

        subject = candidate;
        return true;
    }

    /// <summary>Collects one constant arm per equality test and the trailing discard arm.</summary>
    /// <param name="head">The chain head conditional.</param>
    /// <param name="subject">The tested identifier every arm must compare.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="arms">The collected arms, when every test is a distinct constant.</param>
    /// <returns><see langword="true"/> when the chain is a run of distinct-constant tests.</returns>
    private static bool TryCollectArms(
        ConditionalExpressionSyntax head,
        IdentifierNameSyntax subject,
        SemanticModel model,
        CancellationToken cancellationToken,
        out SeparatedSyntaxList<SwitchExpressionArmSyntax> arms)
    {
        arms = default;
        var constants = new List<object?>(capacity: 4);
        var current = head;
        while (true)
        {
            if (!TryGetSubject(current.Condition, out var armSubject) || !SubjectsMatch(armSubject, subject))
            {
                return false;
            }

            var constantExpression = ((BinaryExpressionSyntax)current.Condition).Right;
            var constant = model.GetConstantValue(constantExpression, cancellationToken);
            if (!constant.HasValue || ContainsConstant(constants, constant.Value))
            {
                return false;
            }

            constants.Add(constant.Value);
            arms = arms.Add(SyntaxFactory.SwitchExpressionArm(
                SyntaxFactory.ConstantPattern(constantExpression.WithoutTrivia()),
                current.WhenTrue.WithoutTrivia()));

            if (current.WhenFalse is not ConditionalExpressionSyntax next)
            {
                arms = arms.Add(SyntaxFactory.SwitchExpressionArm(
                    SyntaxFactory.DiscardPattern(),
                    current.WhenFalse.WithoutTrivia()));
                return true;
            }

            current = next;
        }
    }

    /// <summary>Returns whether a conditional continues a same-subject chain started by its parent.</summary>
    /// <param name="conditional">The conditional to inspect.</param>
    /// <returns><see langword="true"/> when the conditional is the else branch of a same-subject test.</returns>
    private static bool IsChainContinuation(ConditionalExpressionSyntax conditional)
        => conditional.Parent is ConditionalExpressionSyntax parent
            && parent.WhenFalse == conditional
            && TryGetSubject(parent.Condition, out var parentSubject)
            && TryGetSubject(conditional.Condition, out var subject)
            && SubjectsMatch(parentSubject, subject);

    /// <summary>Returns whether every else-branch test compares the same identifier with <c>==</c>.</summary>
    /// <param name="conditional">The next conditional in the chain.</param>
    /// <param name="subject">The identifier the head tests.</param>
    /// <returns><see langword="true"/> when the whole else spine tests <paramref name="subject"/>.</returns>
    private static bool IsSameSubjectChain(ConditionalExpressionSyntax conditional, IdentifierNameSyntax subject)
    {
        if (!TryGetSubject(conditional.Condition, out var next) || !SubjectsMatch(next, subject))
        {
            return false;
        }

        return conditional.WhenFalse is not ConditionalExpressionSyntax following || IsSameSubjectChain(following, subject);
    }

    /// <summary>Extracts the left-hand identifier of a <c>identifier == right</c> equality test.</summary>
    /// <param name="condition">The condition to inspect.</param>
    /// <param name="subject">The tested identifier, when the condition has that shape.</param>
    /// <returns><see langword="true"/> when the condition is <c>identifier == right</c>.</returns>
    private static bool TryGetSubject(ExpressionSyntax condition, [NotNullWhen(true)] out IdentifierNameSyntax? subject)
    {
        if (condition is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression, Left: IdentifierNameSyntax identifier })
        {
            subject = identifier;
            return true;
        }

        subject = null;
        return false;
    }

    /// <summary>Returns whether two subject identifiers name the same value.</summary>
    /// <param name="first">The first identifier.</param>
    /// <param name="second">The second identifier.</param>
    /// <returns><see langword="true"/> when both spell the same name.</returns>
    private static bool SubjectsMatch(IdentifierNameSyntax first, IdentifierNameSyntax second)
        => string.Equals(first.Identifier.ValueText, second.Identifier.ValueText, StringComparison.Ordinal);

    /// <summary>Returns whether a symbol is a read that cannot have a side effect and is not itself constant.</summary>
    /// <param name="symbol">The symbol the subject binds to.</param>
    /// <returns><see langword="true"/> for a local, parameter, or non-const field.</returns>
    private static bool IsSideEffectFreeVariable(ISymbol symbol)
        => symbol switch
        {
            ILocalSymbol => true,
            IParameterSymbol => true,
            IFieldSymbol { IsConst: false } => true,
            _ => false
        };

    /// <summary>Returns whether a constant pattern on the type matches what <c>==</c> would compute.</summary>
    /// <param name="type">The subject type.</param>
    /// <returns><see langword="true"/> for an enum or an integral, char, or string type.</returns>
    /// <remarks>
    /// Floating-point and user-defined equality can disagree with a constant pattern, and a bool
    /// chain covering both values would leave the discard arm unreachable, so those types are left
    /// out.
    /// </remarks>
    private static bool MatchesConstantPattern(ITypeSymbol type)
    {
        // SpecialType values run System_Char (8) through System_UInt64 (16) for char and every
        // integral type, with System_String separate; bool sits below the range and the floating and
        // decimal types above it, so both stay excluded.
        var special = type.SpecialType;
        return type.TypeKind == TypeKind.Enum
            || special == SpecialType.System_String
            || (special >= SpecialType.System_Char && special <= SpecialType.System_UInt64);
    }

    /// <summary>Returns whether a collected constant value already appears in the chain.</summary>
    /// <param name="constants">The constant values seen so far.</param>
    /// <param name="value">The candidate value.</param>
    /// <returns><see langword="true"/> when the value duplicates an earlier arm.</returns>
    private static bool ContainsConstant(List<object?> constants, object? value)
    {
        for (var i = 0; i < constants.Count; i++)
        {
            if (Equals(constants[i], value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the rewritten switch expression binds to the type the chain converts to.</summary>
    /// <param name="conditional">The original chain head.</param>
    /// <param name="candidate">The rewritten switch expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the switch value is usable where the chain was.</returns>
    /// <remarks>
    /// A nested conditional finds its type pairwise; a switch expression finds one best type across
    /// every arm, and the two can differ. Binding the rewrite speculatively at the chain's position
    /// and requiring an implicit conversion to the chain's converted type keeps a fix that would not
    /// compile from ever being offered.
    /// </remarks>
    private static bool RewriteKeepsType(
        ConditionalExpressionSyntax conditional,
        SwitchExpressionSyntax candidate,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (model.GetTypeInfo(conditional, cancellationToken).ConvertedType is not { } targetType
            || targetType.TypeKind == TypeKind.Error)
        {
            return false;
        }

        var speculative = model.GetSpeculativeTypeInfo(conditional.SpanStart, candidate, SpeculativeBindingOption.BindAsExpression);
        if (speculative.Type is not { } switchType || switchType.TypeKind == TypeKind.Error)
        {
            return false;
        }

        return model.Compilation.ClassifyConversion(switchType, targetType).IsImplicit;
    }
}
