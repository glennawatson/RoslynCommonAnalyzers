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
            || !RepeatsAnExplicitTargetType(objectCreation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseTargetTypedNew, objectCreation.Type.GetLocation()));
    }

    /// <summary>Returns whether the creation repeats an explicit target type that <c>new(...)</c> keeps unchanged.</summary>
    /// <param name="objectCreation">The object creation expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when target-typed <c>new</c> is a conservative replacement.</returns>
    private static bool RepeatsAnExplicitTargetType(
        ObjectCreationExpressionSyntax objectCreation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        // An argument carries no single syntactic type node: overload resolution decides the target, so the
        // rewrite is confirmed by re-binding the call rather than by matching a declared type.
        if (objectCreation.Parent is ArgumentSyntax argument)
        {
            return ArgumentRepeatsParameterType(objectCreation, argument, model, cancellationToken);
        }

        if (!TryGetTargetType(objectCreation, model, cancellationToken, out var targetType))
        {
            return false;
        }

        // TryGetTargetType only succeeds with a non-null target, so an unresolved created type compares unequal.
        var createdType = model.GetTypeInfo(objectCreation, cancellationToken).Type;
        return SymbolEqualityComparer.Default.Equals(createdType, targetType);
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

        if (objectCreation.Parent is EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property })
        {
            targetType = model.GetTypeInfo(property.Type, cancellationToken).Type;
            return targetType is not null;
        }

        // A returned value's target is the enclosing member's return type. The converted type carries it
        // (including the awaited result of an async method), so comparing it to the created type is the
        // confirmation that 'new()' constructs the same type. A lambda's return type is inferred rather than
        // declared, so anonymous-function bodies are excluded.
        if (objectCreation.Parent is ReturnStatementSyntax returnStatement)
        {
            if (!ReturnTargetsExplicitMember(returnStatement))
            {
                return false;
            }

            targetType = model.GetTypeInfo(objectCreation, cancellationToken).ConvertedType;
            return targetType is not null;
        }

        // An expression-bodied member (=> ...) targets the member's return type for the same reason, once the
        // void and statement-shaped arrow bodies that carry no target type are excluded.
        if (objectCreation.Parent is ArrowExpressionClauseSyntax arrow)
        {
            if (IsStatementArrowBody(arrow))
            {
                return false;
            }

            targetType = model.GetTypeInfo(objectCreation, cancellationToken).ConvertedType;
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

    /// <summary>Returns whether the created type also binds unchanged when the argument uses target-typed <c>new</c>.</summary>
    /// <param name="objectCreation">The object creation expression passed as an argument.</param>
    /// <param name="argument">The argument that wraps the object creation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the created type equals the parameter type and the call still binds to the same method.</returns>
    private static bool ArgumentRepeatsParameterType(
        ObjectCreationExpressionSyntax objectCreation,
        ArgumentSyntax argument,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (argument.Parent is not ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation })
        {
            return false;
        }

        var typeInfo = model.GetTypeInfo(objectCreation, cancellationToken);
        if (typeInfo.Type is null
            || typeInfo.ConvertedType is null
            || !SymbolEqualityComparer.Default.Equals(typeInfo.Type, typeInfo.ConvertedType))
        {
            return false;
        }

        return TargetTypedNewKeepsTheSameCall(objectCreation, invocation, model, cancellationToken);
    }

    /// <summary>Returns whether rewriting an argument to target-typed <c>new</c> still binds the call to the same method.</summary>
    /// <param name="objectCreation">The object creation argument to rewrite.</param>
    /// <param name="invocation">The call whose overload resolution must not change.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the speculative bind resolves to the identical symbol.</returns>
    private static bool TargetTypedNewKeepsTheSameCall(
        ObjectCreationExpressionSyntax objectCreation,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var boundCall = model.GetSymbolInfo(invocation, cancellationToken).Symbol;
        var implicitNew = SyntaxFactory.ImplicitObjectCreationExpression(
            objectCreation.NewKeyword.WithTrailingTrivia(),
            objectCreation.ArgumentList!,
            objectCreation.Initializer);
        var rewritten = invocation.ReplaceNode(objectCreation, implicitNew);
        var speculativeCall = model
            .GetSpeculativeSymbolInfo(invocation.SpanStart, rewritten, SpeculativeBindingOption.BindAsExpression)
            .Symbol;

        // A target-typed rewrite the overloads make ambiguous has a null speculative symbol, and a call that
        // never bound has a null original symbol; either way the equality fails and the argument stays explicit.
        return speculativeCall is not null && SymbolEqualityComparer.Default.Equals(boundCall, speculativeCall);
    }

    /// <summary>Returns whether a return statement belongs to a member with an explicit, declared return type.</summary>
    /// <param name="returnStatement">The return statement holding the object creation.</param>
    /// <returns><see langword="true"/> when the nearest enclosing return scope is a member, accessor, or local function.</returns>
    private static bool ReturnTargetsExplicitMember(ReturnStatementSyntax returnStatement)
    {
        var scope = returnStatement.Parent;
        while (scope is not null
            and not AnonymousFunctionExpressionSyntax
            and not BaseMethodDeclarationSyntax
            and not LocalFunctionStatementSyntax
            and not AccessorDeclarationSyntax)
        {
            scope = scope.Parent;
        }

        return scope is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax;
    }

    /// <summary>Returns whether an expression-bodied member uses its arrow body as a statement rather than a value.</summary>
    /// <param name="arrow">The arrow expression clause holding the object creation.</param>
    /// <returns><see langword="true"/> for a void method or local function, or a non-<c>get</c> accessor.</returns>
    private static bool IsStatementArrowBody(ArrowExpressionClauseSyntax arrow) => arrow.Parent switch
    {
        MethodDeclarationSyntax method => IsVoidReturn(method.ReturnType),
        LocalFunctionStatementSyntax localFunction => IsVoidReturn(localFunction.ReturnType),
        AccessorDeclarationSyntax accessor => !accessor.IsKind(SyntaxKind.GetAccessorDeclaration),
        _ => false,
    };

    /// <summary>Returns whether a return-type node is the <c>void</c> keyword.</summary>
    /// <param name="returnType">The return-type syntax.</param>
    /// <returns><see langword="true"/> when the return type is <c>void</c>.</returns>
    private static bool IsVoidReturn(TypeSyntax returnType)
        => returnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword };

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
