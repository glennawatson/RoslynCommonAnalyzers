// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the pure-contract attribute (<c>System.Diagnostics.Contracts.PureAttribute</c>) on a
/// method with no result a caller could observe (SST2452). A pure method promises it changes no
/// visible state, so its only useful effect is its return value; on a method that returns nothing
/// the attribute is either wrong or the method does nothing.
/// </summary>
/// <remarks>
/// <para>
/// Three return shapes are reported: <c>void</c>, a bare <c>Task</c>, and a bare <c>ValueTask</c>.
/// The awaitable forms are the same contradiction one step removed — completing a computation that
/// changed nothing and produced nothing is not observable either. <c>Task&lt;T&gt;</c> and
/// <c>ValueTask&lt;T&gt;</c> carry a value and stay silent.
/// </para>
/// <para>
/// A method with an <c>out</c> or <c>ref</c> parameter is not reported: its result can be observed
/// through that parameter, which is how a pure <c>Deconstruct</c> produces its values.
/// </para>
/// <para>
/// The clean path binds nothing. The return shape and the attribute's simple name
/// (<c>Pure</c>/<c>PureAttribute</c>) are matched on syntax first; only a full syntactic match
/// binds the attribute — which keeps a same-named attribute from another namespace silent — and,
/// for the awaitable shapes, the return type, so a user type merely named <c>Task</c> counts as a
/// real result.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2452PureVoidMethodAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The attribute type's name.</summary>
    private const string PureAttributeTypeName = "PureAttribute";

    /// <summary>The task type's name.</summary>
    private const string TaskTypeName = "Task";

    /// <summary>The value-task type's name.</summary>
    private const string ValueTaskTypeName = "ValueTask";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.PureMethodWithoutResult);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Returns whether an attribute name is written as the pure-contract one.</summary>
    /// <param name="name">The attribute's name.</param>
    /// <returns><see langword="true"/> when the rightmost name matches, with or without the suffix.</returns>
    internal static bool IsPureAttributeName(NameSyntax name)
        => GetSimpleName(name) is "Pure" or PureAttributeTypeName;

    /// <summary>Analyzes one method declaration.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.AttributeLists.Count == 0
            || !IsResultFreeReturnShape(method.ReturnType, out var isAwaitableShape)
            || HasResultCarryingParameter(method.ParameterList)
            || FindPureNamedAttribute(method.AttributeLists) is not { } attribute)
        {
            return;
        }

        if (!BindsToContractsPure(context, attribute)
            || (isAwaitableShape && !ReturnsBclAwaitable(context, method.ReturnType)))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.PureMethodWithoutResult,
            attribute.GetLocation(),
            method.Identifier.ValueText,
            method.ReturnType.ToString()));
    }

    /// <summary>Returns whether a return type is a shape that gives the caller nothing to observe.</summary>
    /// <param name="returnType">The method's return type.</param>
    /// <param name="isAwaitableShape">Whether the shape still needs a semantic check against the real task types.</param>
    /// <returns><see langword="true"/> when the type is <c>void</c> or is written as a bare task.</returns>
    private static bool IsResultFreeReturnShape(TypeSyntax returnType, out bool isAwaitableShape)
    {
        if (returnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            isAwaitableShape = false;
            return true;
        }

        isAwaitableShape = GetRightIdentifier(returnType) is TaskTypeName or ValueTaskTypeName;
        return isAwaitableShape;
    }

    /// <summary>Gets the rightmost plain identifier of a possibly qualified type name.</summary>
    /// <param name="type">The type as written.</param>
    /// <returns>The identifier, or <see langword="null"/> when the type ends in anything else.</returns>
    /// <remarks>A generic name is not an identifier here: <c>Task&lt;T&gt;</c> carries a result.</remarks>
    private static string? GetRightIdentifier(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax { Right: IdentifierNameSyntax right } => right.Identifier.ValueText,
        AliasQualifiedNameSyntax { Name: IdentifierNameSyntax aliased } => aliased.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns whether a parameter can carry the method's result back to the caller.</summary>
    /// <param name="parameters">The method's parameter list.</param>
    /// <returns><see langword="true"/> when an <c>out</c> or <c>ref</c> parameter is present.</returns>
    private static bool HasResultCarryingParameter(ParameterListSyntax parameters)
    {
        var list = parameters.Parameters;
        for (var i = 0; i < list.Count; i++)
        {
            var modifiers = list[i].Modifiers;
            if (ModifierListHelper.Contains(modifiers, SyntaxKind.OutKeyword)
                || ModifierListHelper.Contains(modifiers, SyntaxKind.RefKeyword))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the first attribute written with the pure-contract name.</summary>
    /// <param name="attributeLists">The method's attribute lists.</param>
    /// <returns>The attribute, or <see langword="null"/> when none matches by name.</returns>
    private static AttributeSyntax? FindPureNamedAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (IsPureAttributeName(attributes[j].Name))
                {
                    return attributes[j];
                }
            }
        }

        return null;
    }

    /// <summary>Binds an attribute and returns whether it really is the pure-contract one.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="attribute">The attribute whose name matched.</param>
    /// <returns><see langword="true"/> when the attribute's type is the contracts one.</returns>
    /// <remarks>
    /// The name match is not proof: other libraries also declare a <c>Pure</c> attribute, and those
    /// carry their own meanings. The bind settles it.
    /// </remarks>
    private static bool BindsToContractsPure(SyntaxNodeAnalysisContext context, AttributeSyntax attribute)
        => context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is IMethodSymbol constructor
            && constructor.ContainingType is { Name: PureAttributeTypeName } type
            && IsNamespace(type.ContainingNamespace, "Contracts", "Diagnostics");

    /// <summary>Binds a return type and returns whether it is the runtime's bare task or value task.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="returnType">The return type whose name matched.</param>
    /// <returns><see langword="true"/> when the type is the real non-generic awaitable.</returns>
    private static bool ReturnsBclAwaitable(SyntaxNodeAnalysisContext context, TypeSyntax returnType)
        => context.SemanticModel.GetSymbolInfo(returnType, context.CancellationToken).Symbol is
            INamedTypeSymbol { Arity: 0, Name: TaskTypeName or ValueTaskTypeName } named
            && IsNamespace(named.ContainingNamespace, "Tasks", "Threading");

    /// <summary>Returns whether a namespace is <c>System.&lt;outer&gt;.&lt;inner&gt;</c> exactly.</summary>
    /// <param name="inner">The innermost namespace to test.</param>
    /// <param name="innerName">The expected innermost name.</param>
    /// <param name="outerName">The expected middle name.</param>
    /// <returns><see langword="true"/> when the chain is System, the outer name, then the inner name.</returns>
    private static bool IsNamespace(INamespaceSymbol? inner, string innerName, string outerName)
        => inner is { } current
            && current.Name == innerName
            && current.ContainingNamespace is { Name: { } middle } outer
            && middle == outerName
            && outer.ContainingNamespace is { Name: "System" } system
            && system.ContainingNamespace is { IsGlobalNamespace: true };

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased attribute name.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };
}
