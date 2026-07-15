// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a generic type whose base type nests the type inside its own type arguments — for example
/// <c>class C&lt;T&gt; : Base&lt;C&lt;C&lt;T&gt;&gt;&gt;</c> (SST2437). Such a type compiles clean and then
/// throws a <c>TypeLoadException</c> when it loads, because the runtime tries to build an ever-larger chain
/// of constructed types.
/// </summary>
/// <remarks>
/// Detection is purely syntactic: the rule scans only the base list's type-argument syntax, which is a small
/// finite tree. It never asks the semantic model to expand the base type — walking the constructed type graph
/// here does not terminate, so the analyzer must not do it.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2437RecursiveGenericInheritanceAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.RecursiveGenericInheritance);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration);
    }

    /// <summary>Reports one generic type nested inside its own base type arguments.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;

        // Only a generic type with a base list can expand into itself; this rejects almost everything.
        if (declaration.TypeParameterList is not { } typeParameters || declaration.BaseList is not { } baseList)
        {
            return;
        }

        var name = declaration.Identifier.ValueText;
        var arity = typeParameters.Parameters.Count;

        var types = baseList.Types;
        for (var i = 0; i < types.Count; i++)
        {
            if (!NestsSelfInsideOwnArguments(types[i].Type, name, arity))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.RecursiveGenericInheritance,
                declaration.Identifier.GetLocation(),
                name));
            return;
        }
    }

    /// <summary>
    /// Returns whether a base-type subtree contains a reference to the declaring type whose own type
    /// arguments again contain the declaring type — the self-nesting that expands forever.
    /// </summary>
    /// <param name="node">The base-type syntax to scan.</param>
    /// <param name="name">The declaring type's simple name.</param>
    /// <param name="arity">The declaring type's generic arity.</param>
    /// <returns><see langword="true"/> when the type nests inside its own arguments.</returns>
    private static bool NestsSelfInsideOwnArguments(SyntaxNode? node, string name, int arity)
    {
        switch (node)
        {
            case null:
                return false;

            case GenericNameSyntax generic:
            {
                // A reference to the declaring type whose arguments contain it again is the defect.
                if (generic.Identifier.ValueText == name
                    && generic.Arity == arity
                    && ContainsDeclaringReference(generic.TypeArgumentList, name, arity))
                {
                    return true;
                }

                return ScanTypeArguments(generic.TypeArgumentList, name, arity);
            }

            case QualifiedNameSyntax qualified:
                return NestsSelfInsideOwnArguments(qualified.Right, name, arity);

            case AliasQualifiedNameSyntax alias:
                return NestsSelfInsideOwnArguments(alias.Name, name, arity);

            default:
                return false;
        }
    }

    /// <summary>Scans a type-argument list for a self-nesting reference to the declaring type.</summary>
    /// <param name="arguments">The type-argument list to scan.</param>
    /// <param name="name">The declaring type's simple name.</param>
    /// <param name="arity">The declaring type's generic arity.</param>
    /// <returns><see langword="true"/> when any argument nests the declaring type inside its own arguments.</returns>
    private static bool ScanTypeArguments(TypeArgumentListSyntax arguments, string name, int arity)
    {
        var list = arguments.Arguments;
        for (var i = 0; i < list.Count; i++)
        {
            if (NestsSelfInsideOwnArguments(list[i], name, arity))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type-argument subtree contains any reference to the declaring type.</summary>
    /// <param name="arguments">The type-argument list to search.</param>
    /// <param name="name">The declaring type's simple name.</param>
    /// <param name="arity">The declaring type's generic arity.</param>
    /// <returns><see langword="true"/> when the declaring type appears anywhere inside.</returns>
    private static bool ContainsDeclaringReference(TypeArgumentListSyntax arguments, string name, int arity)
    {
        var list = arguments.Arguments;
        for (var i = 0; i < list.Count; i++)
        {
            if (ContainsDeclaringReference(list[i], name, arity))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type subtree contains any reference to the declaring type.</summary>
    /// <param name="node">The type syntax to search.</param>
    /// <param name="name">The declaring type's simple name.</param>
    /// <param name="arity">The declaring type's generic arity.</param>
    /// <returns><see langword="true"/> when the declaring type appears anywhere inside.</returns>
    private static bool ContainsDeclaringReference(SyntaxNode? node, string name, int arity)
    {
        switch (node)
        {
            case null:
                return false;

            case GenericNameSyntax generic:
            {
                if (generic.Identifier.ValueText == name && generic.Arity == arity)
                {
                    return true;
                }

                return ContainsDeclaringReference(generic.TypeArgumentList, name, arity);
            }

            case QualifiedNameSyntax qualified:
                return ContainsDeclaringReference(qualified.Right, name, arity);

            case AliasQualifiedNameSyntax alias:
                return ContainsDeclaringReference(alias.Name, name, arity);

            default:
                return false;
        }
    }
}
