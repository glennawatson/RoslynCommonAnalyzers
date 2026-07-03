// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests replacing zero-length array allocations with the shared empty array
/// (PSH1001). A creation qualifies when its first rank has a single literal-zero size
/// with no (or an empty) initializer, or when the sizes are omitted and the initializer
/// contains zero expressions (<c>new T[] { }</c>). Creations inside attribute arguments
/// are skipped because a method call is not a valid attribute constant, and
/// multi-dimensional creations are skipped because <c>Array.Empty&lt;T&gt;()</c> cannot
/// produce them. The rule is resolved once per compilation by probing
/// <c>System.Array</c> for an <c>Empty</c> member, so it reports nothing on frameworks
/// without the API. On C# 12+ the suggested replacement is an empty collection
/// expression (which compiles to the same shared instance) whenever the creation sits
/// in an unambiguous array-typed target position and the
/// <c>performancesharp.prefer_collection_expressions</c> option (default <c>true</c>)
/// has not been switched off; the choice is carried to the code fix in the
/// diagnostic's properties.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1001UseArrayEmptyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property that tells the code fix to emit an empty collection expression.</summary>
    internal const string UseCollectionExpressionKey = "UseCollectionExpression";

    /// <summary>Editorconfig key that opts out of collection-expression replacements.</summary>
    private const string PreferCollectionExpressionsKey = "performancesharp.prefer_collection_expressions";

    /// <summary>The cached properties attached when the fix should emit a collection expression.</summary>
    private static readonly ImmutableDictionary<string, string?> CollectionExpressionProperties =
        ImmutableDictionary<string, string?>.Empty.Add(UseCollectionExpressionKey, "true");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.UseArrayEmpty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetSpecialType(SpecialType.System_Array).GetMembers("Empty").Length == 0)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeArrayCreation, SyntaxKind.ArrayCreationExpression);
        });
    }

    /// <summary>Returns whether an array creation is a syntax-level zero-length rank-1 candidate.</summary>
    /// <param name="creation">The array creation to inspect.</param>
    /// <returns><see langword="true"/> when the creation allocates a zero-length single-dimensional array.</returns>
    internal static bool IsZeroLengthCreation(ArrayCreationExpressionSyntax creation)
    {
        var rankSpecifiers = creation.Type.RankSpecifiers;
        if (rankSpecifiers.Count == 0 || rankSpecifiers[0].Sizes is not [var size])
        {
            return false;
        }

        if (size.IsKind(SyntaxKind.OmittedArraySizeExpression))
        {
            return creation.Initializer is { Expressions.Count: 0 };
        }

        return IsLiteralZero(size) && creation.Initializer is null or { Expressions.Count: 0 };
    }

    /// <summary>Returns whether a node sits inside an attribute argument, where a method call is not a valid constant.</summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns><see langword="true"/> when an <see cref="AttributeArgumentSyntax"/> ancestor is found before any statement or member.</returns>
    internal static bool IsInsideAttributeArgument(SyntaxNode node)
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

    /// <summary>Reports PSH1001 for a zero-length array allocation that the shared empty array can replace.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeArrayCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ArrayCreationExpressionSyntax)context.Node;
        if (!IsZeroLengthCreation(creation) || IsInsideAttributeArgument(creation))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not IArrayTypeSymbol arrayType
            || !IsValidTypeArgument(arrayType.ElementType))
        {
            return;
        }

        if (ShouldUseCollectionExpression(context, creation, arrayType))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                AllocationRules.UseArrayEmpty,
                creation.SyntaxTree,
                creation.Span,
                CollectionExpressionProperties,
                "[]"));
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.UseArrayEmpty,
            creation.SyntaxTree,
            creation.Span,
            $"Array.Empty<{arrayType.ElementType.ToMinimalDisplayString(context.SemanticModel, creation.SpanStart)}>()"));
    }

    /// <summary>Returns whether the fix should emit an empty collection expression for this creation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="creation">The reported array creation.</param>
    /// <param name="arrayType">The created array type symbol.</param>
    /// <returns><see langword="true"/> when C# 12+ collection expressions are preferred and the position is array-target-typed.</returns>
    private static bool ShouldUseCollectionExpression(SyntaxNodeAnalysisContext context, ArrayCreationExpressionSyntax creation, IArrayTypeSymbol arrayType)
        => creation.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp12 }
            && !IsCollectionExpressionPreferenceDisabled(context)
            && IsArrayTargetTypedPosition(context, creation, arrayType);

    /// <summary>Returns whether the collection-expression preference was explicitly switched off (it defaults to on).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <returns><see langword="true"/> when the option is set to a falsy value.</returns>
    private static bool IsCollectionExpressionPreferenceDisabled(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        return options.TryGetValue(PreferCollectionExpressionsKey, out var value)
            && IsFalse(value);
    }

    /// <summary>Returns whether an editorconfig value is falsy.</summary>
    /// <param name="value">The option value.</param>
    /// <returns><see langword="true"/> for common falsy values.</returns>
    private static bool IsFalse(string value)
        => value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value.Equals("0", StringComparison.Ordinal)
            || value.Equals("no", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns whether the creation sits in a position where <c>[]</c> unambiguously produces the same array.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="creation">The reported array creation.</param>
    /// <param name="arrayType">The created array type symbol.</param>
    /// <returns><see langword="true"/> for explicitly array-typed initializers, returns, and assignment targets.</returns>
    private static bool IsArrayTargetTypedPosition(SyntaxNodeAnalysisContext context, ArrayCreationExpressionSyntax creation, IArrayTypeSymbol arrayType)
        => creation.Parent switch
        {
            EqualsValueClauseSyntax equalsValue => MatchesDeclaredArrayType(GetDeclaredType(equalsValue), creation.Type),
            ReturnStatementSyntax returnStatement => MatchesDeclaredArrayType(GetEnclosingReturnType(returnStatement), creation.Type),
            ArrowExpressionClauseSyntax arrow => MatchesDeclaredArrayType(GetArrowOwnerType(arrow), creation.Type),
            AssignmentExpressionSyntax assignment => IsMatchingSimpleAssignment(context, assignment, creation, arrayType),
            _ => false
        };

    /// <summary>Returns whether a declared type is an array type naming the created array's shape.</summary>
    /// <param name="declared">The declared (target) type syntax, when one exists.</param>
    /// <param name="created">The created array type syntax.</param>
    /// <returns><see langword="true"/> when the declared type is a matching array type.</returns>
    private static bool MatchesDeclaredArrayType(TypeSyntax? declared, ArrayTypeSyntax created)
        => declared is ArrayTypeSyntax declaredArray && MatchesCreatedArray(declaredArray, created);

    /// <summary>Returns whether an assignment is a simple assignment of the creation to a target of the same array type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="assignment">The assignment expression.</param>
    /// <param name="creation">The reported array creation.</param>
    /// <param name="arrayType">The created array type symbol.</param>
    /// <returns><see langword="true"/> for a matching non-covariant assignment target.</returns>
    private static bool IsMatchingSimpleAssignment(
        SyntaxNodeAnalysisContext context,
        AssignmentExpressionSyntax assignment,
        ArrayCreationExpressionSyntax creation,
        IArrayTypeSymbol arrayType)
        => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && assignment.Right == creation
            && IsMatchingArrayTarget(context, assignment.Left, arrayType);

    /// <summary>Gets the declared type behind an initializer's equals clause, for locals, fields, and properties.</summary>
    /// <param name="equalsValue">The initializer clause.</param>
    /// <returns>The declared type syntax, or <see langword="null"/> when the owner is not explicitly typed.</returns>
    private static TypeSyntax? GetDeclaredType(EqualsValueClauseSyntax equalsValue)
        => equalsValue.Parent switch
        {
            VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } => declaration.Type,
            PropertyDeclarationSyntax property => property.Type,
            _ => null
        };

    /// <summary>Gets the declared return type that target-types a return statement's expression.</summary>
    /// <param name="returnStatement">The return statement.</param>
    /// <returns>The declared return type, or <see langword="null"/> inside anonymous functions or unsupported members.</returns>
    private static TypeSyntax? GetEnclosingReturnType(ReturnStatementSyntax returnStatement)
    {
        for (SyntaxNode? current = returnStatement.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AnonymousFunctionExpressionSyntax:
                    return null;

                case MethodDeclarationSyntax method:
                    return method.ReturnType;

                case LocalFunctionStatementSyntax localFunction:
                    return localFunction.ReturnType;

                case AccessorDeclarationSyntax accessor when accessor.IsKind(SyntaxKind.GetAccessorDeclaration):
                    return GetAccessorOwnerType(accessor);

                case MemberDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Gets the declared type that target-types an expression body.</summary>
    /// <param name="arrow">The arrow expression clause.</param>
    /// <returns>The owner's declared type, or <see langword="null"/> for unsupported owners.</returns>
    private static TypeSyntax? GetArrowOwnerType(ArrowExpressionClauseSyntax arrow)
        => arrow.Parent switch
        {
            MethodDeclarationSyntax method => method.ReturnType,
            LocalFunctionStatementSyntax localFunction => localFunction.ReturnType,
            PropertyDeclarationSyntax property => property.Type,
            IndexerDeclarationSyntax indexer => indexer.Type,
            AccessorDeclarationSyntax accessor when accessor.IsKind(SyntaxKind.GetAccessorDeclaration) => GetAccessorOwnerType(accessor),
            _ => null
        };

    /// <summary>Gets the property or indexer type that owns a get accessor.</summary>
    /// <param name="accessor">The get accessor.</param>
    /// <returns>The owning member's declared type, or <see langword="null"/>.</returns>
    private static TypeSyntax? GetAccessorOwnerType(AccessorDeclarationSyntax accessor)
        => accessor.Parent?.Parent switch
        {
            PropertyDeclarationSyntax property => property.Type,
            IndexerDeclarationSyntax indexer => indexer.Type,
            _ => null
        };

    /// <summary>Returns whether an assignment target is exactly the created array type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="target">The assignment's left side.</param>
    /// <param name="arrayType">The created array type symbol.</param>
    /// <returns><see langword="true"/> when the target's type equals the created array (no covariant widening).</returns>
    private static bool IsMatchingArrayTarget(SyntaxNodeAnalysisContext context, ExpressionSyntax target, IArrayTypeSymbol arrayType)
        => context.SemanticModel.GetTypeInfo(target, context.CancellationToken).Type is IArrayTypeSymbol targetType
            && SymbolEqualityComparer.Default.Equals(targetType, arrayType);

    /// <summary>Returns whether a declared array type names the same shape as the created array type.</summary>
    /// <param name="declared">The declared (target) array type syntax.</param>
    /// <param name="created">The created array type syntax, whose first rank carries the zero size.</param>
    /// <returns><see langword="true"/> when element types and rank structure are equivalent.</returns>
    private static bool MatchesCreatedArray(ArrayTypeSyntax declared, ArrayTypeSyntax created)
    {
        if (declared.RankSpecifiers.Count != created.RankSpecifiers.Count
            || !SyntaxFactory.AreEquivalent(declared.ElementType, created.ElementType))
        {
            return false;
        }

        for (var i = 0; i < declared.RankSpecifiers.Count; i++)
        {
            if (declared.RankSpecifiers[i].Rank != created.RankSpecifiers[i].Rank)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an expression is the numeric literal zero.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for a literal <c>0</c>.</returns>
    private static bool IsLiteralZero(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.NumericLiteralExpression)
            && literal.Token.Value is 0;

    /// <summary>Returns whether an element type is usable as the <c>Array.Empty&lt;T&gt;()</c> type argument.</summary>
    /// <param name="elementType">The array element type.</param>
    /// <returns><see langword="false"/> for pointer, function-pointer, and ref-like element types.</returns>
    private static bool IsValidTypeArgument(ITypeSymbol elementType)
        => elementType.TypeKind != TypeKind.Pointer
            && elementType.TypeKind != TypeKind.FunctionPointer
            && !elementType.IsRefLikeType;
}
