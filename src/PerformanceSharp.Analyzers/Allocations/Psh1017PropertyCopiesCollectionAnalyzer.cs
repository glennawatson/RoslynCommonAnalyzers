// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a property whose getter allocates a fresh copy of a collection on every read (PSH1017).
/// A property reads like a field, so callers put it in a loop condition and in the loop body and
/// pay for the copy on each iteration — <c>for (var i = 0; i &lt; Items.Count; i++) Use(Items[i]);</c>
/// becomes a quadratic pile of copies. The excluded names are configured with
/// <c>performancesharp.PSH1017.excluded_properties</c>.
/// </summary>
/// <remarks>
/// <para>
/// "Is this a copy?" is answered in two stages. The syntax stage looks only at what the getter
/// <em>returns</em>: a <c>ToArray</c>/<c>ToList</c>/<c>ToHashSet</c>/<c>ToDictionary</c>/<c>Clone</c>
/// call, an object creation with arguments, or an array creation with an initializer. Everything
/// else — an auto-property, a field read, a cached <c>_cache ??= Build()</c> — is rejected before
/// anything is bound, which is nearly every property in a codebase.
/// </para>
/// <para>
/// The semantic stage then insists the allocation really is a collection copy: the property's own
/// type must be a non-string, non-ref-struct <c>IEnumerable</c>; a <c>To*</c> call must return one
/// too; a <c>Clone</c> call's receiver must be one; and an object creation must be a
/// <c>System.Collections.Generic</c> or <c>System.Collections.Concurrent</c> type seeded from a
/// source collection. That last restriction is deliberate: <c>new ReadOnlyCollection&lt;T&gt;(_list)</c>
/// wraps rather than copies, and is the fix this rule wants, not the bug.
/// </para>
/// <para>
/// The block-bodied getter is scanned through its statements only, never into an expression, so a
/// <c>ToList()</c> inside a lambda or a local function in the getter is not mistaken for the
/// property's own result.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1017PropertyCopiesCollectionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The array materialization method name.</summary>
    internal const string ToArrayMethodName = "ToArray";

    /// <summary>The list materialization method name.</summary>
    internal const string ToListMethodName = "ToList";

    /// <summary>The set materialization method name.</summary>
    internal const string ToHashSetMethodName = "ToHashSet";

    /// <summary>The dictionary materialization method name.</summary>
    internal const string ToDictionaryMethodName = "ToDictionary";

    /// <summary>The clone method name.</summary>
    internal const string CloneMethodName = "Clone";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.PropertyCopiesCollection);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Returns whether an expression has the shape of a fresh collection allocation, before any binding.</summary>
    /// <param name="expression">The unwrapped returned expression.</param>
    /// <returns><see langword="true"/> when the expression could allocate a copy.</returns>
    internal static bool IsCopyShape(ExpressionSyntax expression) => expression switch
    {
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } =>
            IsCopyMethodName(access.Name.Identifier.ValueText),
        BaseObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } => true,
        ArrayCreationExpressionSyntax { Initializer: not null } => true,
        ImplicitArrayCreationExpressionSyntax => true,
        _ => false,
    };

    /// <summary>Sets up the per-tree settings cache, then analyzes every property declaration.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var optionsByTree = new ConcurrentDictionary<SyntaxTree, PropertyCopyOptions>();
        context.RegisterSyntaxNodeAction(nodeContext => AnalyzeProperty(nodeContext, optionsByTree), SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Reports one property whose getter hands back a fresh copy of a collection.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void AnalyzeProperty(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, PropertyCopyOptions> optionsByTree)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (GetGetterBody(property) is not { } body || FindCopyExpression(body) is not { } copy)
        {
            return;
        }

        var name = property.Identifier.ValueText;
        if (GetOptions(context, optionsByTree).IsExcluded(name))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken) is not { Type: { } propertyType }
            || !IsCollectionType(propertyType)
            || !IsCollectionAllocation(context, copy))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.PropertyCopiesCollection,
            property.Identifier.GetLocation(),
            name));
    }

    /// <summary>Reads the settings for the property's tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns>The resolved settings.</returns>
    private static PropertyCopyOptions GetOptions(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, PropertyCopyOptions> optionsByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (optionsByTree.TryGetValue(tree, out var options))
        {
            return options;
        }

        options = PropertyCopyOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        optionsByTree.TryAdd(tree, options);
        return options;
    }

    /// <summary>Gets the node holding the getter's result: an expression, or the accessor's block.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns>The getter body, or <see langword="null"/> for an auto-property or a set-only property.</returns>
    private static SyntaxNode? GetGetterBody(PropertyDeclarationSyntax property)
    {
        if (property.ExpressionBody is { } arrow)
        {
            return arrow.Expression;
        }

        if (property.AccessorList is not { } accessorList)
        {
            return null;
        }

        var accessors = accessorList.Accessors;
        for (var i = 0; i < accessors.Count; i++)
        {
            var accessor = accessors[i];
            if (accessor.Keyword.IsKind(SyntaxKind.GetKeyword))
            {
                return (SyntaxNode?)accessor.ExpressionBody?.Expression ?? accessor.Body;
            }
        }

        return null;
    }

    /// <summary>Finds the allocating expression the getter hands back, if there is one.</summary>
    /// <param name="body">The getter's expression or block.</param>
    /// <returns>The unwrapped allocating expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? FindCopyExpression(SyntaxNode body)
    {
        if (body is ExpressionSyntax expression)
        {
            return GetCopyExpression(expression);
        }

        return body is BlockSyntax block ? FindCopyReturn(block) : null;
    }

    /// <summary>Scans a getter's statements for a <c>return</c> of a fresh allocation.</summary>
    /// <param name="node">The statement or statement container to scan.</param>
    /// <returns>The unwrapped allocating expression, or <see langword="null"/>.</returns>
    /// <remarks>
    /// Only statement-shaped children are entered, so the walk never descends into a lambda, an
    /// anonymous method, or a local function — a <c>ToList()</c> in one of those is somebody else's
    /// return value, not the property's.
    /// </remarks>
    private static ExpressionSyntax? FindCopyReturn(SyntaxNode node)
    {
        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsNode || child.AsNode() is not { } childNode)
            {
                continue;
            }

            if (FindCopyInChild(childNode) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Takes one child of a scanned statement container as far as it goes.</summary>
    /// <param name="childNode">The child node.</param>
    /// <returns>The unwrapped allocating expression, or <see langword="null"/>.</returns>
    /// <remarks>
    /// A local function is skipped rather than entered, because its <c>return</c> is the function's
    /// result and not the property's. A <c>return</c> of anything but a fresh allocation is not a
    /// match, and the scan simply carries on to the next statement.
    /// </remarks>
    private static ExpressionSyntax? FindCopyInChild(SyntaxNode childNode) => childNode switch
    {
        LocalFunctionStatementSyntax => null,
        ReturnStatementSyntax { Expression: { } returned } => GetCopyExpression(returned),
        _ => HoldsStatements(childNode) ? FindCopyReturn(childNode) : null,
    };

    /// <summary>Returns whether a node is one of the statement shapes the scan descends into.</summary>
    /// <param name="node">The child node.</param>
    /// <returns><see langword="true"/> when the node can hold further statements.</returns>
    private static bool HoldsStatements(SyntaxNode node)
        => node is StatementSyntax or ElseClauseSyntax or CatchClauseSyntax or FinallyClauseSyntax or SwitchSectionSyntax;

    /// <summary>Returns an expression when, once unwrapped, it has the shape of a fresh allocation.</summary>
    /// <param name="expression">The expression the getter hands back.</param>
    /// <returns>The unwrapped allocating expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? GetCopyExpression(ExpressionSyntax expression)
    {
        var unwrapped = Unwrap(expression);
        return IsCopyShape(unwrapped) ? unwrapped : null;
    }

    /// <summary>Strips the parentheses and casts that wrap a returned allocation.</summary>
    /// <param name="expression">The returned expression.</param>
    /// <returns>The expression that actually allocates, if one does.</returns>
    /// <remarks><c>Array.Clone</c> returns <see cref="object"/>, so the array form is always written behind a cast.</remarks>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;
        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                {
                    current = parenthesized.Expression;
                    continue;
                }

                case CastExpressionSyntax cast:
                {
                    current = cast.Expression;
                    continue;
                }

                default:
                {
                    return current;
                }
            }
        }
    }

    /// <summary>Returns whether a member name is one of the copying calls the rule recognizes.</summary>
    /// <param name="name">The invoked member name.</param>
    /// <returns><see langword="true"/> for the materialization and clone names.</returns>
    private static bool IsCopyMethodName(string name)
        => name is ToArrayMethodName or ToListMethodName or ToHashSetMethodName or ToDictionaryMethodName or CloneMethodName;

    /// <summary>Returns whether a syntactically matched expression really allocates a collection copy.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="copy">The unwrapped allocating expression.</param>
    /// <returns><see langword="true"/> when the expression copies a collection on every evaluation.</returns>
    private static bool IsCollectionAllocation(SyntaxNodeAnalysisContext context, ExpressionSyntax copy) => copy switch
    {
        InvocationExpressionSyntax invocation => IsCopyingCall(context, invocation),
        BaseObjectCreationExpressionSyntax creation => IsSeedingConstructor(context, creation),
        ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax => true,
        _ => false,
    };

    /// <summary>Returns whether a <c>To*</c> or <c>Clone</c> call produces a fresh collection.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The matched invocation.</param>
    /// <returns><see langword="true"/> when the call allocates a collection.</returns>
    private static bool IsCopyingCall(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return false;
        }

        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        if (access.Name.Identifier.ValueText != CloneMethodName)
        {
            return IsCollectionType(method.ReturnType);
        }

        // Array.Clone is declared to return object, so the receiver is what says this is a collection.
        var receiverType = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        return receiverType is not null && IsCollectionType(receiverType);
    }

    /// <summary>Returns whether an object creation seeds a copying collection from a source sequence.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="creation">The matched object creation.</param>
    /// <returns><see langword="true"/> when the constructor copies a source collection into a new one.</returns>
    private static bool IsSeedingConstructor(SyntaxNodeAnalysisContext context, BaseObjectCreationExpressionSyntax creation)
        => context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is IMethodSymbol { Parameters.Length: > 0 } constructor
            && IsCollectionType(constructor.Parameters[0].Type)
            && constructor.ContainingType is { } created
            && IsCopyingCollectionType(created);

    /// <summary>Returns whether a created type is one whose seeding constructor copies rather than wraps.</summary>
    /// <param name="type">The created type.</param>
    /// <returns><see langword="true"/> for the BCL's generic and concurrent collections.</returns>
    /// <remarks>
    /// <c>System.Collections.ObjectModel</c> is deliberately absent: <c>ReadOnlyCollection&lt;T&gt;</c>
    /// and <c>Collection&lt;T&gt;</c> wrap the list they are handed instead of copying it, and a cached
    /// read-only view is the fix this rule suggests.
    /// </remarks>
    private static bool IsCopyingCollectionType(INamedTypeSymbol type)
        => type.ContainingNamespace is
        {
            Name: "Generic" or "Concurrent",
            ContainingNamespace:
            {
                Name: "Collections",
                ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true },
            },
        };

    /// <summary>Returns whether a type is a collection whose copy costs one allocation per element.</summary>
    /// <param name="type">The type to classify.</param>
    /// <returns><see langword="true"/> for arrays and non-string, non-ref-struct enumerables.</returns>
    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
        {
            return true;
        }

        // A span or a memory is a view, and a constant collection built into one lives in the
        // assembly's data section rather than on the heap.
        if (type.SpecialType == SpecialType.System_String || type.IsRefLikeType)
        {
            return false;
        }

        if (type.OriginalDefinition.SpecialType is SpecialType.System_Collections_IEnumerable
            or SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            return true;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (interfaces[i].SpecialType == SpecialType.System_Collections_IEnumerable)
            {
                return true;
            }
        }

        return false;
    }
}
