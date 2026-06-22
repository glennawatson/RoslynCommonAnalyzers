// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports conservative C# syntax upgrades whose replacements keep the same binding.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModernSyntaxStyleAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 8 language-version value.</summary>
    private const int CSharp8 = 800;

    /// <summary>The numeric C# 9 language-version value.</summary>
    private const int CSharp9 = 900;

    /// <summary>The number of arguments in <c>Substring(start)</c>.</summary>
    private const int SubstringStartOnlyArgumentCount = 1;

    /// <summary>The number of arguments in <c>Substring(start, length)</c>.</summary>
    private const int SubstringStartAndLengthArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernSyntaxRules.UseTargetTypedNew,
        ModernSyntaxRules.UseIndexOperator,
        ModernSyntaxRules.UseRangeOperator);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeElementAccess, SyntaxKind.ElementAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports object creations that can use target-typed <c>new</c>.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(objectCreation, CSharp9)
            || objectCreation.ArgumentList is null
            || !TryGetTargetType(objectCreation, context.SemanticModel, context.CancellationToken, out var targetType))
        {
            return;
        }

        var createdType = context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type;
        if (createdType is null || !SymbolEqualityComparer.Default.Equals(createdType, targetType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseTargetTypedNew, objectCreation.Type.GetLocation()));
    }

    /// <summary>Reports array/string accesses that can use the index-from-end operator.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
    {
        var elementAccess = (ElementAccessExpressionSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(elementAccess, CSharp8)
            || elementAccess.ArgumentList.Arguments.Count != 1
            || !TryGetIndexFromEnd(elementAccess, context.SemanticModel, context.CancellationToken, out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseIndexOperator, elementAccess.ArgumentList.Arguments[0].GetLocation()));
    }

    /// <summary>Reports string slices that can use the range operator.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(invocation, CSharp8)
            || !TryGetRangeFromSubstring(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseRangeOperator, invocation.Expression.GetLocation()));
    }

    /// <summary>Returns whether target-typed <c>new</c> has an explicit target type in this context.</summary>
    /// <param name="objectCreation">The object creation expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="targetType">The target type.</param>
    /// <returns><see langword="true"/> when a conservative target type was found.</returns>
    private static bool TryGetTargetType(
        ObjectCreationExpressionSyntax objectCreation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ITypeSymbol? targetType)
    {
        targetType = null;
        if (objectCreation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } }
            && declaration.Type is not IdentifierNameSyntax { Identifier.ValueText: "var" })
        {
            targetType = model.GetTypeInfo(declaration.Type, cancellationToken).Type;
            return targetType is not null;
        }

        if (objectCreation.Parent is not AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression, Left: { } left }
            || IsDiscardAssignmentTarget(left))
        {
            return false;
        }

        targetType = model.GetTypeInfo(left, cancellationToken).Type;
        return targetType is not null;
    }

    /// <summary>Returns whether an assignment target is a discard.</summary>
    /// <param name="target">The assignment target.</param>
    /// <returns><see langword="true"/> when the target is the discard identifier.</returns>
    private static bool IsDiscardAssignmentTarget(ExpressionSyntax target)
        => target is IdentifierNameSyntax { Identifier.ValueText: "_" };

    /// <summary>Extracts the expression after <c>Length -</c> from an index-from-end candidate.</summary>
    /// <param name="elementAccess">The element access expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="fromEndExpression">The expression to place after <c>^</c>.</param>
    /// <returns><see langword="true"/> when the element access is a conservative index-from-end candidate.</returns>
    private static bool TryGetIndexFromEnd(
        ElementAccessExpressionSyntax elementAccess,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ExpressionSyntax fromEndExpression)
    {
        fromEndExpression = null!;
        if (elementAccess.Expression is not IdentifierNameSyntax receiver
            || elementAccess.ArgumentList.Arguments.Count != 1
            || ExpressionSimplificationAnalyzer.Unwrap(elementAccess.ArgumentList.Arguments[0].Expression) is not BinaryExpressionSyntax binary
            || !binary.IsKind(SyntaxKind.SubtractExpression)
            || binary.Left is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } lengthAccess
            || ExpressionSimplificationAnalyzer.Unwrap(lengthAccess.Expression) is not IdentifierNameSyntax lengthReceiver
            || receiver.Identifier.ValueText != lengthReceiver.Identifier.ValueText
            || !TryGetStableReceiverType(receiver, model, cancellationToken, out var receiverType)
            || !IsArrayOrString(receiverType))
        {
            return false;
        }

        fromEndExpression = binary.Right;
        return true;
    }

    /// <summary>Returns whether a <c>Substring</c> invocation can be expressed with a range.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the invocation is a conservative string-range candidate.</returns>
    private static bool TryGetRangeFromSubstring(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetSubstringReceiver(invocation, model, cancellationToken, out _))
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (!IsSupportedSubstringArgumentCount(arguments.Count)
            || !AreStableSubstringArguments(arguments, model, cancellationToken))
        {
            return false;
        }

        return IsStringSubstring(invocation, model, cancellationToken);
    }

    /// <summary>Returns whether an invocation is a <c>string.Substring</c> call.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the method symbol is the expected string method.</returns>
    private static bool IsStringSubstring(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        return symbol is
        {
            Name: "Substring",
            ContainingType.SpecialType: SpecialType.System_String,
            Parameters.Length: SubstringStartOnlyArgumentCount or SubstringStartAndLengthArgumentCount,
            ReturnType.SpecialType: SpecialType.System_String
        };
    }

    /// <summary>Returns whether the syntax call has a stable string receiver.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="memberAccess">The member access expression.</param>
    /// <returns><see langword="true"/> when the receiver can be read without invoking user code.</returns>
    private static bool TryGetSubstringReceiver(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out MemberAccessExpressionSyntax memberAccess)
    {
        memberAccess = null!;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Substring" } candidate
            || ExpressionSimplificationAnalyzer.Unwrap(candidate.Expression) is not IdentifierNameSyntax receiver
            || !TryGetStableReceiverType(receiver, model, cancellationToken, out var receiverType)
            || receiverType?.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        memberAccess = candidate;
        return true;
    }

    /// <summary>Returns whether the <c>Substring</c> overload has a range equivalent.</summary>
    /// <param name="argumentCount">The argument count.</param>
    /// <returns><see langword="true"/> for supported overload shapes.</returns>
    private static bool IsSupportedSubstringArgumentCount(int argumentCount)
        => argumentCount is SubstringStartOnlyArgumentCount or SubstringStartAndLengthArgumentCount;

    /// <summary>Returns whether the substring arguments can be moved into a range without side effects.</summary>
    /// <param name="arguments">The invocation arguments.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> for stable argument forms.</returns>
    private static bool AreStableSubstringArguments(
        in SeparatedSyntaxList<ArgumentSyntax> arguments,
        SemanticModel model,
        CancellationToken cancellationToken)
        => IsStableBound(arguments[0].Expression, model, cancellationToken)
        && (arguments.Count == SubstringStartOnlyArgumentCount
            || IsStableBound(arguments[1].Expression, model, cancellationToken));

    /// <summary>Returns whether a range bound can be duplicated in the generated upper bound.</summary>
    /// <param name="expression">The candidate bound expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> for literals, locals, and parameters.</returns>
    private static bool IsStableBound(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken)
    {
        expression = ExpressionSimplificationAnalyzer.Unwrap(expression);
        if (expression is LiteralExpressionSyntax)
        {
            return true;
        }

        return expression is IdentifierNameSyntax identifier
            && model.GetSymbolInfo(identifier, cancellationToken).Symbol is ILocalSymbol or IParameterSymbol;
    }

    /// <summary>Returns whether the syntax tree uses at least the supplied language version.</summary>
    /// <param name="node">A syntax node in the tree.</param>
    /// <param name="version">The numeric language version.</param>
    /// <returns><see langword="true"/> when the feature is available.</returns>
    private static bool IsLanguageVersionAtLeast(SyntaxNode node, int version)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= version;

    /// <summary>Returns whether the supplied type supports intrinsic array/string index-from-end semantics.</summary>
    /// <param name="type">The type symbol.</param>
    /// <returns><see langword="true"/> for arrays and strings.</returns>
    private static bool IsArrayOrString(ITypeSymbol? type)
        => type is IArrayTypeSymbol
        || type?.SpecialType == SpecialType.System_String;

    /// <summary>Gets the type for a receiver that can be read repeatedly without invoking user code.</summary>
    /// <param name="receiver">The receiver identifier.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="receiverType">The receiver type.</param>
    /// <returns><see langword="true"/> when the receiver is a local or parameter.</returns>
    private static bool TryGetStableReceiverType(
        IdentifierNameSyntax receiver,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ITypeSymbol? receiverType)
    {
        receiverType = model.GetSymbolInfo(receiver, cancellationToken).Symbol switch
        {
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null
        };

        return receiverType is not null;
    }
}
