// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace StyleSharp.Analyzers;

/// <summary>Reports value-flow, cast, and LINQ readability preferences with syntax-first candidate filtering (SST2220-SST2233).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModernSyntaxValueAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property that carries a replacement type name.</summary>
    internal const string TypeProperty = "Type";

    /// <summary>The diagnostic property that carries the collection element type for a foreach cast.</summary>
    internal const string ElementTypeProperty = "ElementType";

    /// <summary>The diagnostic property that records the if-null fold shape.</summary>
    internal const string FoldKindProperty = "FoldKind";

    /// <summary>The fold kind for fallback assignments.</summary>
    internal const string AssignmentFold = "Assignment";

    /// <summary>The fold kind for throw statements.</summary>
    internal const string ThrowFold = "Throw";

    /// <summary>Editorconfig key that enables LINQ diagnostics on performance-sensitive paths.</summary>
    private const string AvoidLinqOnHotPathKey = "stylesharp.avoid_linq_on_hot_path";

    /// <summary>The numeric C# 7 language-version value.</summary>
    private const int CSharp7 = 700;

    /// <summary>The numeric C# 8 language-version value.</summary>
    private const int CSharp8 = 800;

    /// <summary>The numeric C# 9 language-version value.</summary>
    private const int CSharp9 = 900;

    /// <summary>The numeric C# 14 language-version value.</summary>
    private const int CSharp14 = 1400;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernSyntaxRules.SimplifyInterpolation,
        ModernSyntaxRules.MakeIgnoredExpressionValueExplicit,
        ModernSyntaxRules.RemoveOverwrittenValue,
        ModernSyntaxRules.UseCoalesceAssignment,
        ModernSyntaxRules.ConvertAnonymousObjectToTuple,
        ModernSyntaxRules.AddExplicitForeachCast,
        ModernSyntaxRules.AddVisibleInnerCast,
        ModernSyntaxRules.FoldNullCheckIntoAssignment,
        ModernSyntaxRules.UseLocalFunction,
        ModernSyntaxRules.CollapseLinqWhereTerminal,
        ModernSyntaxRules.CollapseLinqTypeFilter,
        ModernSyntaxRules.UseDirectNullPattern,
        ModernSyntaxRules.UseUnboundGenericName,
        ModernSyntaxRules.AvoidLinqOnHotPath);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeInterpolation, SyntaxKind.Interpolation);
        context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
        context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
        context.RegisterSyntaxNodeAction(AnalyzeIfStatement, SyntaxKind.IfStatement);
        context.RegisterSyntaxNodeAction(AnalyzeCoalesceExpression, SyntaxKind.CoalesceExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAnonymousObject, SyntaxKind.AnonymousObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeForeach, SyntaxKind.ForEachStatement);
        context.RegisterSyntaxNodeAction(AnalyzeCast, SyntaxKind.CastExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIsPattern, SyntaxKind.IsPatternExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIsExpression, SyntaxKind.IsExpression);
    }

    /// <summary>Gets whether a source node is parsed with at least the supplied C# language version.</summary>
    /// <param name="node">The syntax node.</param>
    /// <param name="version">The numeric language version.</param>
    /// <returns><see langword="true"/> when the syntax tree supports the requested version.</returns>
    internal static bool IsLanguageVersionAtLeast(SyntaxNode node, int version)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= version;

    /// <summary>Returns whether an expression can be evaluated without observable side effects.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> for literals, defaults, and local or parameter reads.</returns>
    internal static bool IsSideEffectFreeValue(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken)
    {
        expression = ExpressionSimplificationAnalyzer.Unwrap(expression);
        if (expression is LiteralExpressionSyntax or DefaultExpressionSyntax)
        {
            return true;
        }

        if (expression is not IdentifierNameSyntax)
        {
            return false;
        }

        return model.GetSymbolInfo(expression, cancellationToken).Symbol is ILocalSymbol or IParameterSymbol;
    }

    /// <summary>Returns whether a target can be read twice safely by a compound null assignment rewrite.</summary>
    /// <param name="expression">The target expression.</param>
    /// <returns><see langword="true"/> for identifiers, <c>this</c>, and member-access chains rooted in those.</returns>
    internal static bool IsSideEffectFreeTarget(ExpressionSyntax expression)
        => CompoundAssignmentOperators.IsSideEffectFreeTarget(ExpressionSimplificationAnalyzer.Unwrap(expression));

    /// <summary>Returns whether an interpolation can remove or fold a <c>ToString</c> call.</summary>
    /// <param name="interpolation">The interpolation.</param>
    /// <param name="replacement">The replacement interpolation.</param>
    /// <returns><see langword="true"/> when the interpolation has a supported simplification.</returns>
    internal static bool TryGetSimplifiedInterpolation(InterpolationSyntax interpolation, out InterpolationSyntax replacement)
    {
        replacement = null!;
        if (interpolation.Expression is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "ToString",
                    Expression: { } receiver
                },
                ArgumentList.Arguments: { Count: <= 1 } arguments
            })
        {
            return false;
        }

        InterpolationFormatClauseSyntax? format = null;
        if (arguments.Count == 1 && !TryCreateInterpolationFormat(arguments[0].Expression, out format))
        {
            return false;
        }

        replacement = interpolation.WithExpression(receiver.WithTriviaFrom(interpolation.Expression)).WithFormatClause(format);
        return true;
    }

    /// <summary>Gets the named tuple element represented by an anonymous object initializer.</summary>
    /// <param name="initializer">The anonymous object member.</param>
    /// <param name="name">The tuple element name.</param>
    /// <param name="expression">The tuple element expression.</param>
    /// <returns><see langword="true"/> when the element can be represented in a tuple literal.</returns>
    internal static bool TryGetTupleElement(AnonymousObjectMemberDeclaratorSyntax initializer, out string name, out ExpressionSyntax expression)
    {
        name = string.Empty;
        expression = initializer.Expression;
        if (initializer.NameEquals is { } nameEquals)
        {
            name = nameEquals.Name.Identifier.ValueText;
            return !string.IsNullOrEmpty(name);
        }

        if (ExpressionSimplificationAnalyzer.InferredName(initializer.Expression) is not { } inferred)
        {
            return false;
        }

        name = inferred;
        return true;
    }

    /// <summary>Gets whether a pattern is a broad <c>object</c> null check.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="reportNode">The node to report.</param>
    /// <param name="negated">Whether the pattern is negated.</param>
    /// <returns><see langword="true"/> when the pattern can be rewritten as a null pattern.</returns>
    internal static bool TryGetBroadObjectNullPattern(PatternSyntax pattern, out PatternSyntax reportNode, out bool negated)
    {
        negated = false;
        if (pattern is UnaryPatternSyntax { OperatorToken.RawKind: (int)SyntaxKind.NotKeyword, Pattern: { } inner })
        {
            negated = true;
            pattern = inner;
        }

        reportNode = pattern;
        return IsObjectTypePattern(pattern);
    }

    /// <summary>Reports removable <c>ToString</c> calls inside interpolation holes.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeInterpolation(SyntaxNodeAnalysisContext context)
    {
        var interpolation = (InterpolationSyntax)context.Node;
        if (!TryGetSimplifiedInterpolation(interpolation, out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.SimplifyInterpolation, interpolation.Expression.GetLocation()));
    }

    /// <summary>Reports ignored expression values and adjacent overwritten assignments.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
    {
        var statement = (ExpressionStatementSyntax)context.Node;
        if (statement.Expression is AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment)
        {
            AnalyzeOverwrittenAssignment(context, statement, assignment);
            return;
        }

        if (!IsIgnoredValueCandidate(statement.Expression)
            || context.SemanticModel.GetTypeInfo(statement.Expression, context.CancellationToken).Type is not { } type
            || type.SpecialType == SpecialType.System_Void)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.MakeIgnoredExpressionValueExplicit, statement.Expression.GetLocation()));
    }

    /// <summary>Reports local initializers overwritten by the immediately following assignment.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        var local = (LocalDeclarationStatementSyntax)context.Node;
        if (IsLanguageVersionAtLeast(local, CSharp7) && IsLocalFunctionCandidate(local, context.SemanticModel, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseLocalFunction, local.Declaration.Variables[0].Identifier.GetLocation()));
            return;
        }

        if (local.Declaration.Type is IdentifierNameSyntax { Identifier.Text: "var" }
            || local.Declaration.Variables.Count != 1
            || local.Declaration.Variables[0] is not { Initializer.Value: { } initializer } variable
            || local.Parent is not BlockSyntax block
            || !TryGetNextIdentifierAssignment(block, local, out var assigned, out _)
            || assigned.Identifier.ValueText != variable.Identifier.ValueText
            || !IsSideEffectFreeValue(initializer, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.RemoveOverwrittenValue, initializer.GetLocation()));
    }

    /// <summary>Reports null fallback if statements and post-assignment null checks.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (ifStatement.Else is not null
            || !TryGetNullCheckedExpression(ifStatement.Condition, context.SemanticModel, context.CancellationToken, out var checkedExpression)
            || !IsSideEffectFreeTarget(checkedExpression))
        {
            return;
        }

        if (TryGetEmbeddedAssignment(ifStatement.Statement, out var target, out _)
            && SyntaxFactory.AreEquivalent(ExpressionSimplificationAnalyzer.Unwrap(checkedExpression), ExpressionSimplificationAnalyzer.Unwrap(target))
            && IsLanguageVersionAtLeast(ifStatement, CSharp8))
        {
            context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseCoalesceAssignment, ifStatement.IfKeyword.GetLocation()));
            return;
        }

        AnalyzeFoldedNullCheck(context, ifStatement, checkedExpression);
    }

    /// <summary>Reports a post-assignment null check that can be folded into the assignment.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="ifStatement">The if statement.</param>
    /// <param name="checkedExpression">The null-checked expression.</param>
    private static void AnalyzeFoldedNullCheck(SyntaxNodeAnalysisContext context, IfStatementSyntax ifStatement, ExpressionSyntax checkedExpression)
    {
        if (!TryGetPreviousStatement(ifStatement, out var previous)
            || !TryGetAssignedExpression(previous, checkedExpression, out var assignedExpression)
            || !TryGetFoldKind(ifStatement.Statement, checkedExpression, out var foldKind)
            || !CanUseCoalesce(assignedExpression, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (foldKind == ThrowFold && !IsLanguageVersionAtLeast(ifStatement, CSharp7))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(FoldKindProperty, foldKind);
        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.FoldNullCheckIntoAssignment, ifStatement.IfKeyword.GetLocation(), properties));
    }

    /// <summary>Reports <c>x ?? (x = y)</c> forms that can use <c>??=</c>.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeCoalesceExpression(SyntaxNodeAnalysisContext context)
    {
        var coalesce = (BinaryExpressionSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(coalesce, CSharp8)
            || ExpressionSimplificationAnalyzer.Unwrap(coalesce.Right) is not AssignmentExpressionSyntax assignment
            || assignment.RawKind != (int)SyntaxKind.SimpleAssignmentExpression
            || !SyntaxFactory.AreEquivalent(
                ExpressionSimplificationAnalyzer.Unwrap(coalesce.Left),
                ExpressionSimplificationAnalyzer.Unwrap(assignment.Left))
            || !IsSideEffectFreeTarget(coalesce.Left))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseCoalesceAssignment, coalesce.OperatorToken.GetLocation()));
    }

    /// <summary>Reports opt-in anonymous object to tuple conversions.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeAnonymousObject(SyntaxNodeAnalysisContext context)
    {
        var anonymous = (AnonymousObjectCreationExpressionSyntax)context.Node;
        if (anonymous.Initializers.Count < 2)
        {
            return;
        }

        for (var i = 0; i < anonymous.Initializers.Count; i++)
        {
            if (!TryGetTupleElement(anonymous.Initializers[i], out _, out _))
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.ConvertAnonymousObjectToTuple, anonymous.NewKeyword.GetLocation()));
    }

    /// <summary>Reports foreach statements that hide runtime element casts.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeForeach(SyntaxNodeAnalysisContext context)
    {
        var foreachStatement = (ForEachStatementSyntax)context.Node;
        var info = context.SemanticModel.GetForEachStatementInfo(foreachStatement);
        if (!info.ElementConversion.Exists
            || info.ElementConversion.IsImplicit
            || context.SemanticModel.GetTypeInfo(foreachStatement.Type, context.CancellationToken).Type is not { } iterationType
            || info.ElementType is not { } elementType
            || context.SemanticModel.GetTypeInfo(foreachStatement.Expression, context.CancellationToken).Type is not { } collectionType
            || !IsStronglyTypedCollection(collectionType, elementType))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(
            ElementTypeProperty,
            iterationType.ToMinimalDisplayString(context.SemanticModel, foreachStatement.SpanStart));
        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.AddExplicitForeachCast, foreachStatement.ForEachKeyword.GetLocation(), properties));
    }

    /// <summary>Reports casts that hide another explicit conversion inside the source cast.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeCast(SyntaxNodeAnalysisContext context)
    {
        var cast = (CastExpressionSyntax)context.Node;
        if (GetHighestExplicitConversion(context.SemanticModel, cast, context.CancellationToken) is not { } outer)
        {
            return;
        }

        if (outer.Conversion.IsImplicit
            || outer.Operand is not IConversionOperation inner
            || !inner.IsImplicit
            || inner.Conversion.IsImplicit
            || inner.Type is null)
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(
            TypeProperty,
            inner.Type.ToMinimalDisplayString(context.SemanticModel, cast.SpanStart));
        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.AddVisibleInnerCast, cast.GetLocation(), properties));
    }

    /// <summary>Reports LINQ chain simplifications, hot-path LINQ calls, and generic <c>nameof</c> cleanup.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (IsLanguageVersionAtLeast(invocation, CSharp14) && ContainsConcreteGenericNameOfArgument(invocation))
        {
            context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseUnboundGenericName, invocation.Expression.GetLocation()));
            return;
        }

        if (TryGetWhereTerminalCollapse(invocation, context.SemanticModel, context.CancellationToken, out var terminalName))
        {
            context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.CollapseLinqWhereTerminal, terminalName.GetLocation()));
            return;
        }

        if (TryGetTypeFilterCollapse(invocation, context.SemanticModel, context.CancellationToken, out var castName))
        {
            context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.CollapseLinqTypeFilter, castName.GetLocation()));
            return;
        }

        if (!IsAvoidLinqOnHotPathEnabled(context)
            || !TryGetInvocationName(invocation, out var name)
            || !IsHotPathLinqMethodName(name.Identifier.ValueText)
            || !IsEnumerableInvocation(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.AvoidLinqOnHotPath, name.GetLocation()));
    }

    /// <summary>Reports broad object patterns that only check for null.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeIsPattern(SyntaxNodeAnalysisContext context)
    {
        var patternExpression = (IsPatternExpressionSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(patternExpression, CSharp9)
            || !TryGetBroadObjectNullPattern(patternExpression.Pattern, out var reportNode, out _)
            || !CanUseNullPatternFor(context.SemanticModel.GetTypeInfo(patternExpression.Expression, context.CancellationToken).Type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseDirectNullPattern, reportNode.GetLocation()));
    }

    /// <summary>Reports legacy <c>is object</c> type checks that only check for null.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeIsExpression(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(binary, CSharp9)
            || !IsObjectType(binary.Right)
            || !CanUseNullPatternFor(context.SemanticModel.GetTypeInfo(binary.Left, context.CancellationToken).Type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseDirectNullPattern, binary.Right.GetLocation()));
    }

    /// <summary>Gets the next statement in a block.</summary>
    /// <param name="block">The block.</param>
    /// <param name="statement">The current statement.</param>
    /// <returns>The next statement, or <see langword="null"/>.</returns>
    private static StatementSyntax? NextStatement(BlockSyntax block, StatementSyntax statement)
    {
        var statements = block.Statements;
        for (var i = 0; i < statements.Count - 1; i++)
        {
            if (statements[i] == statement)
            {
                return statements[i + 1];
            }
        }

        return null;
    }

    /// <summary>Gets the previous statement in a block.</summary>
    /// <param name="ifStatement">The if statement.</param>
    /// <param name="previous">The previous statement.</param>
    /// <returns><see langword="true"/> when there is a previous statement.</returns>
    private static bool TryGetPreviousStatement(IfStatementSyntax ifStatement, out StatementSyntax previous)
    {
        previous = null!;
        if (ifStatement.Parent is not BlockSyntax block)
        {
            return false;
        }

        var statements = block.Statements;
        for (var i = 1; i < statements.Count; i++)
        {
            if (statements[i] == ifStatement)
            {
                previous = statements[i - 1];
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports an adjacent assignment overwritten before it is read.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="statement">The assignment statement.</param>
    /// <param name="assignment">The assignment expression.</param>
    private static void AnalyzeOverwrittenAssignment(SyntaxNodeAnalysisContext context, ExpressionStatementSyntax statement, AssignmentExpressionSyntax assignment)
    {
        if (assignment.Left is not IdentifierNameSyntax identifier
            || statement.Parent is not BlockSyntax block
            || !TryGetNextIdentifierAssignment(block, statement, out var nextIdentifier, out _)
            || nextIdentifier.Identifier.ValueText != identifier.Identifier.ValueText
            || !IsSideEffectFreeValue(assignment.Right, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.RemoveOverwrittenValue, assignment.GetLocation()));
    }

    /// <summary>Creates an interpolation format clause from a string literal expression.</summary>
    /// <param name="expression">The format expression.</param>
    /// <param name="format">The format clause.</param>
    /// <returns><see langword="true"/> when the expression is a non-empty string literal.</returns>
    private static bool TryCreateInterpolationFormat(ExpressionSyntax expression, out InterpolationFormatClauseSyntax format)
    {
        format = null!;
        if (expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression)
            || string.IsNullOrEmpty(literal.Token.ValueText))
        {
            return false;
        }

        var textToken = SyntaxFactory.Token(
            default,
            SyntaxKind.InterpolatedStringTextToken,
            literal.Token.ValueText,
            literal.Token.ValueText,
            default);
        format = SyntaxFactory.InterpolationFormatClause(SyntaxFactory.Token(SyntaxKind.ColonToken), textToken);
        return true;
    }

    /// <summary>Gets the next statement when it is a simple identifier assignment.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="statement">The current statement.</param>
    /// <param name="identifier">The assigned identifier.</param>
    /// <param name="value">The assigned value.</param>
    /// <returns><see langword="true"/> when the next statement is a simple identifier assignment.</returns>
    private static bool TryGetNextIdentifierAssignment(
        BlockSyntax block,
        StatementSyntax statement,
        out IdentifierNameSyntax identifier,
        out ExpressionSyntax value)
    {
        identifier = null!;
        value = null!;
        if (NextStatement(block, statement) is not ExpressionStatementSyntax expressionStatement
            || expressionStatement.Expression is not AssignmentExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                Left: IdentifierNameSyntax assigned,
                Right: { } assignedValue
            })
        {
            return false;
        }

        identifier = assigned;
        value = assignedValue;
        return true;
    }

    /// <summary>Returns whether a local delegate declaration can be represented as a local function.</summary>
    /// <param name="local">The local declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when every local reference is a direct invocation.</returns>
    private static bool IsLocalFunctionCandidate(LocalDeclarationStatementSyntax local, SemanticModel model, CancellationToken cancellationToken)
    {
        if (local.Declaration.Type is IdentifierNameSyntax { Identifier.ValueText: "var" }
            || !TryGetSingleLocalInitializer(local, out var variable, out var initializer)
            || initializer is not LambdaExpressionSyntax { AsyncKeyword.RawKind: 0 } lambda
            || model.GetDeclaredSymbol(variable, cancellationToken) is not ILocalSymbol localSymbol
            || localSymbol.Type is not INamedTypeSymbol { DelegateInvokeMethod: { } invokeMethod }
            || !CanBuildLocalFunctionParameters(lambda, invokeMethod)
            || local.Parent is not BlockSyntax block)
        {
            return false;
        }

        return IsOnlyInvokedAfterDeclaration(block, local, localSymbol, model, cancellationToken);
    }

    /// <summary>Returns whether the lambda parameters can be represented as local-function parameters.</summary>
    /// <param name="lambda">The lambda initializer.</param>
    /// <param name="invokeMethod">The delegate invocation method.</param>
    /// <returns><see langword="true"/> when the parameter list has a safe one-to-one shape.</returns>
    private static bool CanBuildLocalFunctionParameters(LambdaExpressionSyntax lambda, IMethodSymbol invokeMethod)
    {
        if (invokeMethod.Parameters.Length == 0)
        {
            return lambda switch
            {
                ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 0 } => true,
                _ => false
            };
        }

        if (lambda is SimpleLambdaExpressionSyntax)
        {
            return invokeMethod.Parameters.Length == 1;
        }

        if (lambda is not ParenthesizedLambdaExpressionSyntax parenthesized
            || parenthesized.ParameterList.Parameters.Count != invokeMethod.Parameters.Length)
        {
            return false;
        }

        for (var i = 0; i < parenthesized.ParameterList.Parameters.Count; i++)
        {
            if (parenthesized.ParameterList.Parameters[i].Modifiers.Count != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a local delegate is referenced only as a direct invocation after its declaration.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="local">The local declaration.</param>
    /// <param name="localSymbol">The local symbol.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when there is at least one direct invocation and no value use.</returns>
    private static bool IsOnlyInvokedAfterDeclaration(
        BlockSyntax block,
        LocalDeclarationStatementSyntax local,
        ILocalSymbol localSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var startIndex = IndexOf(block.Statements, local);
        if (startIndex < 0)
        {
            return false;
        }

        var seenReference = false;
        var statements = block.Statements;
        for (var i = startIndex + 1; i < statements.Count; i++)
        {
            if (!ContainsOnlyDirectInvocationReferences(statements[i], localSymbol, model, cancellationToken, ref seenReference))
            {
                return false;
            }
        }

        return seenReference;
    }

    /// <summary>Gets the index of a statement in a statement list.</summary>
    /// <param name="statements">The statement list.</param>
    /// <param name="statement">The statement to find.</param>
    /// <returns>The index, or -1.</returns>
    private static int IndexOf(SyntaxList<StatementSyntax> statements, StatementSyntax statement)
    {
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i] == statement)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Checks one statement for direct invocations of the local delegate.</summary>
    /// <param name="statement">The statement to scan.</param>
    /// <param name="localSymbol">The local symbol.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="seenReference">Whether a reference has been seen.</param>
    /// <returns><see langword="true"/> when all references in the statement are direct invocations.</returns>
    private static bool ContainsOnlyDirectInvocationReferences(
        StatementSyntax statement,
        ILocalSymbol localSymbol,
        SemanticModel model,
        CancellationToken cancellationToken,
        ref bool seenReference)
    {
        foreach (var node in statement.DescendantNodes())
        {
            if (node is not IdentifierNameSyntax identifier
                || identifier.Identifier.ValueText != localSymbol.Name
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, localSymbol))
            {
                continue;
            }

            seenReference = true;
            if (identifier.Parent is not InvocationExpressionSyntax invocation || invocation.Expression != identifier)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an expression statement candidate produces a value that can be made explicit.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns><see langword="true"/> for direct calls and object creations.</returns>
    private static bool IsIgnoredValueCandidate(ExpressionSyntax expression)
        => expression is InvocationExpressionSyntax or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax;

    /// <summary>Returns whether the hot-path LINQ rule is enabled for this compilation or tree.</summary>
    /// <param name="context">The syntax context.</param>
    /// <returns><see langword="true"/> when the opt-in rule is enabled.</returns>
    private static bool IsAvoidLinqOnHotPathEnabled(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        return options.TryGetValue(AvoidLinqOnHotPathKey, out var value)
            && IsTrue(value);
    }

    /// <summary>Returns whether an editorconfig value is truthy.</summary>
    /// <param name="value">The option value.</param>
    /// <returns><see langword="true"/> for common truthy values.</returns>
    private static bool IsTrue(string value)
        => value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns whether a <c>nameof</c> invocation contains concrete generic type arguments.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <returns><see langword="true"/> when at least one generic name can omit its type arguments.</returns>
    private static bool ContainsConcreteGenericNameOfArgument(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not IdentifierNameSyntax { Identifier.ValueText: "nameof" }
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        foreach (var node in invocation.ArgumentList.Arguments[0].DescendantNodesAndSelf())
        {
            if (node is not GenericNameSyntax genericName)
            {
                continue;
            }

            var arguments = genericName.TypeArgumentList.Arguments;
            for (var i = 0; i < arguments.Count; i++)
            {
                if (!arguments[i].IsKind(SyntaxKind.OmittedTypeArgument))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Finds a <c>Where(predicate).Any()</c> or <c>Where(predicate).Count()</c> chain.</summary>
    /// <param name="invocation">The terminal invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="terminalName">The terminal method name.</param>
    /// <returns><see langword="true"/> when the chain can collapse into a predicate terminal call.</returns>
    private static bool TryGetWhereTerminalCollapse(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out SimpleNameSyntax terminalName)
    {
        terminalName = null!;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax { Name: { } name, Expression: InvocationExpressionSyntax whereInvocation }
            || !IsPredicateTerminalName(name.Identifier.ValueText)
            || whereInvocation.ArgumentList.Arguments.Count != 1
            || whereInvocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Where" })
        {
            return false;
        }

        if (!IsEnumerableInvocation(invocation, model, cancellationToken)
            || !IsEnumerableInvocation(whereInvocation, model, cancellationToken))
        {
            return false;
        }

        terminalName = name;
        return true;
    }

    /// <summary>Finds a <c>Where(x =&gt; x is T).Cast&lt;T&gt;()</c> chain.</summary>
    /// <param name="invocation">The cast invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="castName">The cast method name.</param>
    /// <returns><see langword="true"/> when the chain can be represented as one typed filter call.</returns>
    private static bool TryGetTypeFilterCollapse(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out GenericNameSyntax castName)
    {
        castName = null!;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Cast" } name, Expression: InvocationExpressionSyntax whereInvocation }
            || name.TypeArgumentList.Arguments.Count != 1
            || whereInvocation.ArgumentList.Arguments.Count != 1
            || whereInvocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Where" }
            || !TryGetLambdaTypeCheck(whereInvocation.ArgumentList.Arguments[0].Expression, out var checkedType))
        {
            return false;
        }

        var castType = name.TypeArgumentList.Arguments[0];
        if (!SyntaxFactory.AreEquivalent(checkedType, castType)
            || !IsEnumerableInvocation(invocation, model, cancellationToken)
            || !IsEnumerableInvocation(whereInvocation, model, cancellationToken))
        {
            return false;
        }

        castName = name;
        return true;
    }

    /// <summary>Gets a simple lambda body of the form <c>x is T</c>.</summary>
    /// <param name="expression">The lambda expression.</param>
    /// <param name="checkedType">The checked type.</param>
    /// <returns><see langword="true"/> when the lambda is a direct type filter.</returns>
    private static bool TryGetLambdaTypeCheck(ExpressionSyntax expression, out TypeSyntax checkedType)
    {
        checkedType = null!;
        return TryGetTypePattern(
            expression switch
            {
                SimpleLambdaExpressionSyntax simple => simple.ExpressionBody,
                ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ExpressionBody,
                _ => null
            },
            out checkedType);
    }

    /// <summary>Gets the type from a direct type pattern expression.</summary>
    /// <param name="body">The lambda body.</param>
    /// <param name="checkedType">The checked type.</param>
    /// <returns><see langword="true"/> when the body is a simple type pattern.</returns>
    private static bool TryGetTypePattern(ExpressionSyntax? body, out TypeSyntax checkedType)
    {
        checkedType = null!;
        if (body is not IsPatternExpressionSyntax patternExpression)
        {
            return TryGetLegacyIsType(body, out checkedType);
        }

        switch (patternExpression.Pattern)
        {
            case DeclarationPatternSyntax declarationPattern:
                {
                    checkedType = declarationPattern.Type;
                    return true;
                }

            case RecursivePatternSyntax { Type: { } type }:
                {
                    checkedType = type;
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Gets the type from a legacy <c>is T</c> expression.</summary>
    /// <param name="body">The expression body.</param>
    /// <param name="checkedType">The checked type.</param>
    /// <returns><see langword="true"/> when the body is a legacy type check.</returns>
    private static bool TryGetLegacyIsType(ExpressionSyntax? body, out TypeSyntax checkedType)
    {
        checkedType = null!;
        if (body is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression, Right: TypeSyntax type })
        {
            return false;
        }

        checkedType = type;
        return true;
    }

    /// <summary>Gets an invocation method name without allocating strings for non-member shapes.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> when a simple method name was found.</returns>
    private static bool TryGetInvocationName(InvocationExpressionSyntax invocation, out SimpleNameSyntax name)
    {
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax { Name: { } memberName }:
                {
                    name = memberName;
                    return true;
                }

            case IdentifierNameSyntax identifier:
                {
                    name = identifier;
                    return true;
                }

            case GenericNameSyntax generic:
                {
                    name = generic;
                    return true;
                }

            default:
                {
                    name = null!;
                    return false;
                }
        }
    }

    /// <summary>Returns whether the method name is a common <see cref="System.Linq.Enumerable"/> operator worth screening semantically.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> when the method can be a LINQ iterator or terminal call.</returns>
    [SuppressMessage("Critical Code Smell", "S1541:Methods and properties should not be too complex", Justification = "A flat name switch avoids an allocation-heavy lookup on every invocation.")]
    private static bool IsHotPathLinqMethodName(string name)
        => name switch
        {
            "Aggregate" or "All" or "Any" or "Append" or "Cast" or "Concat" or "Contains" or "Count" or "DefaultIfEmpty"
                or "Distinct" or "ElementAt" or "Except" or "First" or "FirstOrDefault" or "GroupBy" or "Intersect"
                or "Last" or "LastOrDefault" or "Max" or "Min" or "OfType" or "OrderBy" or "OrderByDescending"
                or "Prepend" or "Reverse" or "Select" or "SelectMany" or "Single" or "SingleOrDefault" or "Skip"
                or "SkipWhile" or "Sum" or "Take" or "TakeWhile" or "ThenBy" or "ThenByDescending" or "ToArray"
                or "ToDictionary" or "ToHashSet" or "ToList" or "ToLookup" or "Union" or "Where" or "Zip" => true,
            _ => false
        };

    /// <summary>Returns whether the terminal method has an overload that accepts a predicate.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for terminal calls with predicate overloads.</returns>
    private static bool IsPredicateTerminalName(string name)
        => name is "Any" or "Count" or "First" or "FirstOrDefault" or "Last" or "LastOrDefault"
            or "Single" or "SingleOrDefault";

    /// <summary>Returns whether the invocation resolves to <see cref="System.Linq.Enumerable"/>.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the target is an in-memory LINQ method.</returns>
    private static bool IsEnumerableInvocation(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
        {
            return false;
        }

        var original = method.ReducedFrom ?? method;
        return IsSystemLinqEnumerable(original.ContainingType);
    }

    /// <summary>Returns whether a named type is <c>System.Linq.Enumerable</c>.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for <c>System.Linq.Enumerable</c>.</returns>
    private static bool IsSystemLinqEnumerable(INamedTypeSymbol? type)
        => type?.Name == "Enumerable"
            && type.ContainingNamespace?.Name == "Linq"
            && type.ContainingNamespace.ContainingNamespace?.Name == "System"
            && type.ContainingNamespace.ContainingNamespace.ContainingNamespace.IsGlobalNamespace;

    /// <summary>Returns whether a pattern only checks for an <c>object</c> reference.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns><see langword="true"/> when the pattern is a broad object type pattern.</returns>
    private static bool IsObjectTypePattern(PatternSyntax pattern)
    {
        if (pattern is DeclarationPatternSyntax { Type: { } declarationType } && IsObjectType(declarationType))
        {
            return true;
        }

        return pattern is RecursivePatternSyntax { Type: { } recursiveType } && IsObjectType(recursiveType);
    }

    /// <summary>Returns whether a type syntax is the built-in <c>object</c> type.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns><see langword="true"/> when the type is object.</returns>
    private static bool IsObjectType(SyntaxNode type)
        => type is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword };

    /// <summary>Returns whether a type can be checked for null without boxing.</summary>
    /// <param name="type">The expression type.</param>
    /// <returns><see langword="true"/> for reference types and nullable type parameters.</returns>
    private static bool CanUseNullPatternFor(ITypeSymbol? type)
        => type is not null && (!type.IsValueType || type.TypeKind == TypeKind.TypeParameter);

    /// <summary>Gets an expression checked against null by supported null-check syntax.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="checkedExpression">The checked expression.</param>
    /// <returns><see langword="true"/> when a null check was found.</returns>
    private static bool TryGetNullCheckedExpression(
        ExpressionSyntax condition,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ExpressionSyntax checkedExpression)
    {
        checkedExpression = null!;
        condition = ExpressionSimplificationAnalyzer.Unwrap(condition);
        if (condition is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.EqualsExpression))
        {
            var leftNull = binary.Left.IsKind(SyntaxKind.NullLiteralExpression);
            var rightNull = binary.Right.IsKind(SyntaxKind.NullLiteralExpression);
            if (leftNull == rightNull)
            {
                return false;
            }

            if (model.GetSymbolInfo(binary, cancellationToken).Symbol is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator })
            {
                return false;
            }

            checkedExpression = leftNull ? binary.Right : binary.Left;
            return true;
        }

        if (condition is not IsPatternExpressionSyntax pattern
            || pattern.Pattern is not ConstantPatternSyntax constantPattern
            || constantPattern.Expression is not LiteralExpressionSyntax nullLiteral
            || !nullLiteral.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return false;
        }

        checkedExpression = pattern.Expression;
        return true;
    }

    /// <summary>Gets a simple assignment from a statement or single-statement block.</summary>
    /// <param name="statement">The statement.</param>
    /// <param name="target">The assignment target.</param>
    /// <param name="value">The assigned value.</param>
    /// <returns><see langword="true"/> when a simple assignment was found.</returns>
    private static bool TryGetEmbeddedAssignment(StatementSyntax statement, out ExpressionSyntax target, out ExpressionSyntax value)
    {
        target = null!;
        value = null!;
        var candidate = statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : statement;
        if (candidate is not ExpressionStatementSyntax
            {
                Expression: AssignmentExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: { } left,
                    Right: { } right
                }
            })
        {
            return false;
        }

        target = left;
        value = right;
        return true;
    }

    /// <summary>Gets the single variable and initializer from a local declaration.</summary>
    /// <param name="local">The local declaration.</param>
    /// <param name="variable">The single variable.</param>
    /// <param name="initializer">The initializer expression.</param>
    /// <returns><see langword="true"/> when the local declares one initialized variable.</returns>
    private static bool TryGetSingleLocalInitializer(
        LocalDeclarationStatementSyntax local,
        out VariableDeclaratorSyntax variable,
        out ExpressionSyntax initializer)
    {
        variable = null!;
        initializer = null!;
        if (local.Declaration.Variables.Count != 1
            || local.Declaration.Variables[0] is not { Initializer.Value: { } value } declarator)
        {
            return false;
        }

        variable = declarator;
        initializer = value;
        return true;
    }

    /// <summary>Gets the expression assigned by a previous declaration or assignment that is checked by the if statement.</summary>
    /// <param name="statement">The previous statement.</param>
    /// <param name="checkedExpression">The expression checked against null.</param>
    /// <param name="assignedExpression">The assigned expression.</param>
    /// <returns><see langword="true"/> when the previous statement writes the checked expression.</returns>
    private static bool TryGetAssignedExpression(
        StatementSyntax statement,
        ExpressionSyntax checkedExpression,
        out ExpressionSyntax assignedExpression)
    {
        assignedExpression = null!;
        if (statement is LocalDeclarationStatementSyntax local
            && TryGetSingleLocalInitializer(local, out var variable, out var initializer)
            && checkedExpression is IdentifierNameSyntax checkedIdentifier
            && checkedIdentifier.Identifier.ValueText == variable.Identifier.ValueText)
        {
            assignedExpression = initializer;
            return true;
        }

        if (!TryGetEmbeddedAssignment(statement, out var target, out var value)
            || !SyntaxFactory.AreEquivalent(ExpressionSimplificationAnalyzer.Unwrap(target), ExpressionSimplificationAnalyzer.Unwrap(checkedExpression))
            || !IsSideEffectFreeTarget(target))
        {
            return false;
        }

        assignedExpression = value;
        return true;
    }

    /// <summary>Gets the fold kind for a post-assignment null check body.</summary>
    /// <param name="statement">The if body.</param>
    /// <param name="checkedExpression">The checked expression.</param>
    /// <param name="foldKind">The fold kind.</param>
    /// <returns><see langword="true"/> when the body can be folded.</returns>
    private static bool TryGetFoldKind(StatementSyntax statement, ExpressionSyntax checkedExpression, out string foldKind)
    {
        foldKind = string.Empty;
        var candidate = statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : statement;
        if (candidate is ThrowStatementSyntax { Expression: not null })
        {
            foldKind = ThrowFold;
            return true;
        }

        if (!TryGetEmbeddedAssignment(candidate, out var target, out _)
            || !SyntaxFactory.AreEquivalent(ExpressionSimplificationAnalyzer.Unwrap(target), ExpressionSimplificationAnalyzer.Unwrap(checkedExpression)))
        {
            return false;
        }

        foldKind = AssignmentFold;
        return true;
    }

    /// <summary>Returns whether a coalesce expression can be used for the assigned expression.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the expression is nullable or a reference type.</returns>
    private static bool CanUseCoalesce(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken)
    {
        var type = model.GetTypeInfo(expression, cancellationToken).Type;
        return type is { IsValueType: false } and not IPointerTypeSymbol;
    }

    /// <summary>Returns whether a foreach source has a strongly-typed element shape.</summary>
    /// <param name="collectionType">The collection type.</param>
    /// <param name="elementType">The element type selected by foreach.</param>
    /// <returns><see langword="true"/> when the collection is not just a legacy object enumerable.</returns>
    private static bool IsStronglyTypedCollection(ITypeSymbol collectionType, ITypeSymbol elementType)
    {
        if (elementType.SpecialType != SpecialType.System_Object
            || collectionType is IArrayTypeSymbol
            || collectionType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            return true;
        }

        foreach (var @interface in collectionType.AllInterfaces)
        {
            if (@interface.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the highest explicit conversion represented by a cast expression.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="cast">The cast expression.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns>The conversion operation, or <see langword="null"/>.</returns>
    private static IConversionOperation? GetHighestExplicitConversion(SemanticModel model, CastExpressionSyntax cast, CancellationToken cancellationToken)
    {
        var expression = cast.Expression;
        while (ExpressionSimplificationAnalyzer.Unwrap(expression) is { } unwrapped && unwrapped != expression)
        {
            expression = unwrapped;
        }

        var operation = model.GetOperation(expression, cancellationToken);
        IConversionOperation? highest = null;
        for (var current = operation?.Parent; current is IConversionOperation conversion; current = current.Parent)
        {
            if (conversion.Syntax == cast && !conversion.Conversion.IsImplicit)
            {
                highest = conversion;
            }
        }

        return highest;
    }
}
