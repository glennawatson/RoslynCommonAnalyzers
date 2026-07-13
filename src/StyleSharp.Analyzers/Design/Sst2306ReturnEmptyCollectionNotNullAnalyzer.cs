// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>null</c> handed back where the declared return type is a collection (SST2306) — from a
/// <c>return</c>, an expression body, or either branch of a returned conditional. "Nothing" already has
/// a value in a collection type, and it is the empty collection; null is a second, invisible one that
/// every caller then has to check for.
/// </summary>
/// <remarks>
/// <para>
/// A <b>nullable</b> return type is never reported. <c>IEnumerable&lt;T&gt;?</c> is the author saying,
/// in the signature, that null is a value here — the rule takes them at their word. <b>string</b> is
/// never reported either: it satisfies <c>IEnumerable&lt;char&gt;</c>, but nobody returns a string
/// meaning "a sequence of characters", and a rule that rewrote <c>return null</c> to an empty string in
/// every string-returning method would be both wrong and unbearable. A <c>Task&lt;T&gt;</c> or
/// <c>ValueTask&lt;T&gt;</c> is not a collection, so an async member's null belongs to another rule.
/// </para>
/// <para>
/// The suggested replacement is computed only once a violation is confirmed, and every piece of it is
/// resolved from the compilation being analyzed — never assumed from a target framework. The empty
/// collection expression is written only where the language version has it; <c>Array.Empty&lt;T&gt;()</c>
/// only where the compilation really has that member, and spelled the way the file's imports demand
/// (<c>System.Array.Empty&lt;T&gt;()</c> without <c>using System;</c>); a set or dictionary only where
/// its type resolves. A return type nothing can be safely constructed for is still reported — the fix
/// is simply not offered, because a suggestion that would not compile is worse than none.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2306ReturnEmptyCollectionNotNullAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key carrying the empty-collection expression the code fix writes.</summary>
    internal const string ReplacementKey = "Replacement";

    /// <summary>The empty collection expression, written where the language version has it.</summary>
    private const string CollectionExpressionText = "[]";

    /// <summary>The cached properties attached when the fix should write an empty collection expression.</summary>
    private static readonly ImmutableDictionary<string, string?> CollectionExpressionProperties =
        ImmutableDictionary<string, string?>.Empty.Add(ReplacementKey, CollectionExpressionText);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.ReturnEmptyCollectionNotNull);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Returns whether an expression is the <c>null</c> literal.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for a <c>null</c> literal.</returns>
    internal static bool IsNullLiteral(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>Returns whether a tree's language version can write an empty collection expression.</summary>
    /// <param name="tree">The syntax tree being analyzed or fixed.</param>
    /// <returns><see langword="true"/> on C# 12 and later.</returns>
    internal static bool SupportsCollectionExpressions(SyntaxTree tree)
        => tree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp12 };

    /// <summary>Returns whether a tree's language version target-types a conditional's branches.</summary>
    /// <param name="tree">The syntax tree being analyzed.</param>
    /// <returns><see langword="true"/> on C# 9 and later.</returns>
    /// <remarks>
    /// Below C# 9 a conditional needs a common type between its branches, so swapping <c>null</c> for an
    /// empty array in <c>flag ? _list : null</c> would leave two branches with no conversion between
    /// them. The null is still reported there; only the fix is withheld.
    /// </remarks>
    private static bool SupportsTargetTypedConditional(SyntaxTree tree)
        => tree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp9 };

    /// <summary>Prepares the per-compilation lookups, then watches every returned expression.</summary>
    /// <param name="context">The compilation start context.</param>
    /// <remarks>The lookups are built lazily inside the holder, so a compilation with no violation resolves nothing.</remarks>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var types = new EmptyCollectionTypes(context.Compilation);
        context.RegisterSyntaxNodeAction(nodeContext => AnalyzeReturnStatement(nodeContext, types), SyntaxKind.ReturnStatement);
        context.RegisterSyntaxNodeAction(nodeContext => AnalyzeArrowClause(nodeContext, types), SyntaxKind.ArrowExpressionClause);
    }

    /// <summary>Analyzes the expression of a <c>return</c> statement.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation's empty-collection lookups.</param>
    private static void AnalyzeReturnStatement(SyntaxNodeAnalysisContext context, EmptyCollectionTypes types)
    {
        if (((ReturnStatementSyntax)context.Node).Expression is not { } returned)
        {
            return;
        }

        AnalyzeReturnedExpression(context, returned, types);
    }

    /// <summary>Analyzes the expression of an expression-bodied member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation's empty-collection lookups.</param>
    private static void AnalyzeArrowClause(SyntaxNodeAnalysisContext context, EmptyCollectionTypes types)
        => AnalyzeReturnedExpression(context, ((ArrowExpressionClauseSyntax)context.Node).Expression, types);

    /// <summary>Reports the null literals a member hands back in place of an empty collection.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="returned">The returned expression.</param>
    /// <param name="types">The compilation's empty-collection lookups.</param>
    /// <remarks>
    /// The syntactic tests come first and reject nearly everything for free: an expression with no null
    /// in it, a member the language gives its shape to (a lambda, an async method), and a nullable
    /// return type never reach the semantic model.
    /// </remarks>
    private static void AnalyzeReturnedExpression(SyntaxNodeAnalysisContext context, ExpressionSyntax returned, EmptyCollectionTypes types)
    {
        var unwrapped = Unwrap(returned);
        if (!ContainsNull(unwrapped)
            || FindEnclosingMember(returned) is not { } member
            || GetDeclaredReturnType(member) is not { } returnTypeSyntax
            || returnTypeSyntax is NullableTypeSyntax)
        {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(returnTypeSyntax, context.CancellationToken).Type;
        if (type is null or ITypeParameterSymbol || !CollectionTypeClassification.IsCollection(type))
        {
            return;
        }

        var replacement = BuildReplacement(context, type, returned.SpanStart, types);
        ReportNulls(context, unwrapped, GetMemberName(member), replacement);
    }

    /// <summary>Reports the returned expression's null literal, or the null branches of its conditional.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="unwrapped">The unwrapped returned expression.</param>
    /// <param name="memberName">The name of the member handing back null.</param>
    /// <param name="replacement">The suggested empty-collection expression, when one can be written.</param>
    private static void ReportNulls(SyntaxNodeAnalysisContext context, ExpressionSyntax unwrapped, string memberName, string? replacement)
    {
        if (IsNullLiteral(unwrapped))
        {
            Report(context, unwrapped, memberName, replacement);
            return;
        }

        if (unwrapped is not ConditionalExpressionSyntax conditional)
        {
            return;
        }

        // The empty collection can only take a branch's place where the language target-types the
        // branches from the return type; below that, the null is reported with no fix offered.
        var branchReplacement = SupportsTargetTypedConditional(context.Node.SyntaxTree) ? replacement : null;
        ReportBranch(context, conditional.WhenTrue, memberName, branchReplacement);
        ReportBranch(context, conditional.WhenFalse, memberName, branchReplacement);
    }

    /// <summary>Reports one branch of a returned conditional when it is null.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="branch">The conditional branch.</param>
    /// <param name="memberName">The name of the member handing back null.</param>
    /// <param name="replacement">The suggested empty-collection expression, when one can be written.</param>
    private static void ReportBranch(SyntaxNodeAnalysisContext context, ExpressionSyntax branch, string memberName, string? replacement)
    {
        var unwrapped = Unwrap(branch);
        if (!IsNullLiteral(unwrapped))
        {
            return;
        }

        Report(context, unwrapped, memberName, replacement);
    }

    /// <summary>Reports one null literal, carrying its replacement to the code fix when there is one.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="nullLiteral">The reported null literal.</param>
    /// <param name="memberName">The name of the member handing back null.</param>
    /// <param name="replacement">The suggested empty-collection expression, when one can be written.</param>
    private static void Report(SyntaxNodeAnalysisContext context, ExpressionSyntax nullLiteral, string memberName, string? replacement)
    {
        if (replacement is null)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                DesignRules.ReturnEmptyCollectionNotNull,
                nullLiteral.SyntaxTree,
                nullLiteral.Span,
                memberName));
            return;
        }

        var properties = string.Equals(replacement, CollectionExpressionText, StringComparison.Ordinal)
            ? CollectionExpressionProperties
            : ImmutableDictionary<string, string?>.Empty.Add(ReplacementKey, replacement);
        context.ReportDiagnostic(DiagnosticHelper.Create(
            DesignRules.ReturnEmptyCollectionNotNull,
            nullLiteral.SyntaxTree,
            nullLiteral.Span,
            properties,
            memberName));
    }

    /// <summary>Returns whether an expression hands back null, directly or through a conditional branch.</summary>
    /// <param name="unwrapped">The unwrapped returned expression.</param>
    /// <returns><see langword="true"/> when a null literal is in return position.</returns>
    private static bool ContainsNull(ExpressionSyntax unwrapped)
    {
        if (IsNullLiteral(unwrapped))
        {
            return true;
        }

        return unwrapped is ConditionalExpressionSyntax conditional
            && (IsNullLiteral(Unwrap(conditional.WhenTrue)) || IsNullLiteral(Unwrap(conditional.WhenFalse)));
    }

    /// <summary>Strips the parentheses wrapping a returned expression.</summary>
    /// <param name="expression">The returned expression.</param>
    /// <returns>The expression the member actually hands back.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized.Expression;
        }

        return current;
    }

    /// <summary>Finds the member whose declared return type gives a returned expression its meaning.</summary>
    /// <param name="node">The returned expression.</param>
    /// <returns>The enclosing member, or <see langword="null"/> when the rule has nothing to say about it.</returns>
    /// <remarks>
    /// A lambda takes its shape from the delegate it is assigned to, and an async member returns a task
    /// whose result the <c>return</c> supplies — neither is this rule's business. A <c>set</c> accessor
    /// returns nothing at all.
    /// </remarks>
    private static SyntaxNode? FindEnclosingMember(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AnonymousFunctionExpressionSyntax:
                {
                    return null;
                }

                case LocalFunctionStatementSyntax localFunction:
                {
                    return ModifierListHelper.Contains(localFunction.Modifiers, SyntaxKind.AsyncKeyword) ? null : localFunction;
                }

                case MethodDeclarationSyntax method:
                {
                    return ModifierListHelper.Contains(method.Modifiers, SyntaxKind.AsyncKeyword) ? null : method;
                }

                case AccessorDeclarationSyntax accessor:
                {
                    return accessor.IsKind(SyntaxKind.GetAccessorDeclaration) ? accessor.Parent?.Parent : null;
                }

                case PropertyDeclarationSyntax property:
                {
                    return property;
                }

                case IndexerDeclarationSyntax indexer:
                {
                    return indexer;
                }

                case MemberDeclarationSyntax or GlobalStatementSyntax:
                {
                    return null;
                }
            }
        }

        return null;
    }

    /// <summary>Gets a member's declared return type.</summary>
    /// <param name="member">The enclosing member.</param>
    /// <returns>The return type syntax, or <see langword="null"/> for a member with none.</returns>
    private static TypeSyntax? GetDeclaredReturnType(SyntaxNode member) => member switch
    {
        MethodDeclarationSyntax method => method.ReturnType,
        LocalFunctionStatementSyntax localFunction => localFunction.ReturnType,
        PropertyDeclarationSyntax property => property.Type,
        IndexerDeclarationSyntax indexer => indexer.Type,
        _ => null,
    };

    /// <summary>Gets the name a member is reported under.</summary>
    /// <param name="member">The enclosing member.</param>
    /// <returns>The member's name.</returns>
    private static string GetMemberName(SyntaxNode member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        LocalFunctionStatementSyntax localFunction => localFunction.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        IndexerDeclarationSyntax indexer => indexer.ThisKeyword.ValueText,
        _ => string.Empty,
    };

    /// <summary>Builds the empty-collection expression that replaces the null, when one can be written.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="type">The member's declared return type.</param>
    /// <param name="position">The position the replacement's names are written at.</param>
    /// <param name="types">The compilation's empty-collection lookups.</param>
    /// <returns>The replacement expression text, or <see langword="null"/> when no empty value can be proven to exist.</returns>
    private static string? BuildReplacement(SyntaxNodeAnalysisContext context, ITypeSymbol type, int position, EmptyCollectionTypes types)
    {
        if (type is IArrayTypeSymbol array)
        {
            return array.Rank == 1 && CanNameAsTypeArgument(array.ElementType)
                ? BuildEmptyArray(context, array.ElementType, position, types, SupportsCollectionExpressions(context.Node.SyntaxTree))
                : null;
        }

        if (type is not INamedTypeSymbol named)
        {
            return null;
        }

        return named.TypeKind == TypeKind.Interface
            ? BuildEmptyInterface(context, named, position, types)
            : BuildEmptyConcrete(context, named, position);
    }

    /// <summary>Builds the empty value for an array, or for an interface an array satisfies.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="elementType">The sequence's element type.</param>
    /// <param name="position">The position the replacement's names are written at.</param>
    /// <param name="types">The compilation's empty-collection lookups.</param>
    /// <param name="allowCollectionExpression">Whether an empty collection expression may be written here.</param>
    /// <returns>The replacement expression text.</returns>
    /// <remarks>
    /// The collection expression is only ever written for an array target, where its meaning is exact and
    /// the compiler picks the cheapest empty array it can. Everything else is spelled out, with
    /// <c>Array</c> named the way this file's imports require, and with a plain zero-length array where
    /// the compilation has no shared empty one.
    /// </remarks>
    private static string BuildEmptyArray(
        SyntaxNodeAnalysisContext context,
        ITypeSymbol elementType,
        int position,
        EmptyCollectionTypes types,
        bool allowCollectionExpression)
    {
        if (allowCollectionExpression)
        {
            return CollectionExpressionText;
        }

        var element = elementType.ToMinimalDisplayString(context.SemanticModel, position);
        return types.HasEmptyArray()
            ? types.GetArrayName(context.SemanticModel, position) + ".Empty<" + element + ">()"
            : "new " + element + "[0]";
    }

    /// <summary>Builds the empty value for a collection interface.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="named">The declared interface type.</param>
    /// <param name="position">The position the replacement's names are written at.</param>
    /// <param name="types">The compilation's empty-collection lookups.</param>
    /// <returns>The replacement expression text, or <see langword="null"/> for an interface with no provable empty value.</returns>
    /// <remarks>
    /// An empty array satisfies every sequence interface, so those all collapse to one answer — and to
    /// the one that compiles on every framework, rather than to a collection expression whose lowering
    /// for an interface target is the compiler's business rather than the reader's. A set and a
    /// dictionary are not sequences, and get the concrete type the compilation provides for them; a
    /// custom interface gets nothing, because only its author knows what an empty one is.
    /// </remarks>
    private static string? BuildEmptyInterface(SyntaxNodeAnalysisContext context, INamedTypeSymbol named, int position, EmptyCollectionTypes types)
    {
        var arguments = named.TypeArguments;
        if (arguments.Length == 0)
        {
            return named.SpecialType == SpecialType.System_Collections_IEnumerable
                ? BuildEmptyArray(context, context.Compilation.GetSpecialType(SpecialType.System_Object), position, types, allowCollectionExpression: false)
                : null;
        }

        if (!CollectionTypeClassification.IsInSystemCollectionsGeneric(named) || !CanNameArguments(arguments))
        {
            return null;
        }

        return IsSequenceInterfaceName(named.Name)
            ? BuildEmptyArray(context, arguments[0], position, types, allowCollectionExpression: false)
            : BuildEmptyKeyedInterface(context, named.Name, arguments, position, types);
    }

    /// <summary>Returns whether an empty array satisfies the named generic interface.</summary>
    /// <param name="name">The interface's name.</param>
    /// <returns><see langword="true"/> for the sequence interfaces an array implements.</returns>
    private static bool IsSequenceInterfaceName(string name)
        => name is "IEnumerable" or "ICollection" or "IList" or "IReadOnlyCollection" or "IReadOnlyList";

    /// <summary>Builds the empty value for a set or dictionary interface, which no array satisfies.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="name">The interface's name.</param>
    /// <param name="arguments">The interface's type arguments.</param>
    /// <param name="position">The position the replacement's names are written at.</param>
    /// <param name="types">The compilation's empty-collection lookups.</param>
    /// <returns>The replacement expression text, or <see langword="null"/> when the concrete type is not in the compilation.</returns>
    private static string? BuildEmptyKeyedInterface(
        SyntaxNodeAnalysisContext context,
        string name,
        ImmutableArray<ITypeSymbol> arguments,
        int position,
        EmptyCollectionTypes types) => name switch
        {
            "ISet" or "IReadOnlySet" => BuildConstruction(context, types.GetHashSet(), arguments, position),
            "IDictionary" or "IReadOnlyDictionary" => BuildConstruction(context, types.GetDictionary(), arguments, position),
            _ => null,
        };

    /// <summary>Builds a <c>new</c> of a resolved generic collection type over the declared interface's type arguments.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="definition">The concrete type's definition, when the compilation has one.</param>
    /// <param name="arguments">The declared interface's type arguments.</param>
    /// <param name="position">The position the type name is written at.</param>
    /// <returns>The replacement expression text, or <see langword="null"/> when the type does not exist here.</returns>
    private static string? BuildConstruction(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? definition,
        ImmutableArray<ITypeSymbol> arguments,
        int position)
    {
        if (definition is null || definition.TypeParameters.Length != arguments.Length)
        {
            return null;
        }

        var constructed = definition.Construct(arguments, default);
        return "new " + constructed.ToMinimalDisplayString(context.SemanticModel, position) + "()";
    }

    /// <summary>Builds the empty value for a concrete collection type the member returns.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="named">The declared type.</param>
    /// <param name="position">The position the type name is written at.</param>
    /// <returns>The replacement expression text, or <see langword="null"/> when the type cannot simply be constructed.</returns>
    private static string? BuildEmptyConcrete(SyntaxNodeAnalysisContext context, INamedTypeSymbol named, int position)
        => HasPublicParameterlessConstructor(named)
            ? "new " + named.ToMinimalDisplayString(context.SemanticModel, position) + "()"
            : null;

    /// <summary>Returns whether every type argument can be written back out as one.</summary>
    /// <param name="arguments">The declared type's type arguments.</param>
    /// <returns><see langword="true"/> when the replacement can name them all.</returns>
    private static bool CanNameArguments(ImmutableArray<ITypeSymbol> arguments)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            if (!CanNameAsTypeArgument(arguments[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a type can be written as a type argument.</summary>
    /// <param name="type">The candidate type argument.</param>
    /// <returns><see langword="false"/> for pointer, function-pointer, and ref-like types.</returns>
    private static bool CanNameAsTypeArgument(ITypeSymbol type)
        => type.TypeKind != TypeKind.Pointer && type.TypeKind != TypeKind.FunctionPointer && !type.IsRefLikeType;

    /// <summary>Returns whether a type can be constructed with <c>new T()</c> from anywhere.</summary>
    /// <param name="named">The candidate type.</param>
    /// <returns><see langword="true"/> when a public parameterless constructor exists.</returns>
    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol named)
    {
        if (named.IsAbstract || named.IsStatic)
        {
            return false;
        }

        var constructors = named.InstanceConstructors;
        for (var i = 0; i < constructors.Length; i++)
        {
            var constructor = constructors[i];
            if (constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public)
            {
                return true;
            }
        }

        return false;
    }
}
