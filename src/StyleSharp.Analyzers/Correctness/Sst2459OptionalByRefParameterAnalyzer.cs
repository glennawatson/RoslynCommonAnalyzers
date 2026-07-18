// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>[Optional]</c> written on a <c>ref</c> or <c>out</c> parameter (SST2459). A by-reference
/// argument must name a variable at every call site, so the attribute advertises an optionality C#
/// callers can never use — while reflection sees <c>IsOptional</c> and tells late-bound callers the
/// opposite.
/// </summary>
/// <remarks>
/// The questions are asked cheapest-first. A parameter with no attribute list, or with no <c>out</c> and
/// no bare <c>ref</c>, is rejected on syntax alone, so the no-diagnostic path never binds; <c>in</c> and
/// <c>ref readonly</c> are genuinely omittable and fall out here. Only an attribute whose written name is
/// <c>Optional</c> is bound, which is what confirms it is
/// <c>System.Runtime.InteropServices.OptionalAttribute</c> and not a type of the same name. A member of a
/// COM-imported type is left alone: there the compiler really does let callers omit the argument.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2459OptionalByRefParameterAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.OptionalByRefParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Parameter);
    }

    /// <summary>Reports an <c>[Optional]</c> that a by-reference parameter's callers can never act on.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var parameter = (ParameterSyntax)context.Node;

        var attributeLists = parameter.AttributeLists;
        if (attributeLists.Count == 0)
        {
            return;
        }

        // `out` and a bare `ref` bind a caller variable at every call site; `in` and `ref readonly`
        // remain omittable, so [Optional] on them is honest and is not reported.
        if (ByReferenceModifier(parameter.Modifiers) is not { } modifier)
        {
            return;
        }

        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var attribute = attributes[j];
                if (!IsOptionalName(attribute.Name)
                    || !IsFrameworkOptionalAttribute(context.SemanticModel, attribute, context.CancellationToken))
                {
                    continue;
                }

                // A by-reference [Optional] on a COM-imported member is legitimate: there the compiler
                // really does let callers omit it, so the advertised optionality is real.
                if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is { ContainingType.IsComImport: true })
                {
                    return;
                }

                context.ReportDiagnostic(DiagnosticHelper.Create(
                    CorrectnessRules.OptionalByRefParameter,
                    attribute.GetLocation(),
                    parameter.Identifier.ValueText,
                    modifier));
            }
        }
    }

    /// <summary>Names the by-reference modifier whose callers cannot omit the argument.</summary>
    /// <param name="modifiers">The parameter's modifiers.</param>
    /// <returns><c>"out"</c>, <c>"ref"</c> for a bare <c>ref</c>, or <see langword="null"/> otherwise.</returns>
    private static string? ByReferenceModifier(SyntaxTokenList modifiers)
    {
        var hasRef = false;
        var hasReadOnly = false;
        for (var i = 0; i < modifiers.Count; i++)
        {
            var kind = modifiers[i].Kind();
            if (kind == SyntaxKind.OutKeyword)
            {
                return "out";
            }

            if (kind == SyntaxKind.RefKeyword)
            {
                hasRef = true;
            }
            else if (kind == SyntaxKind.ReadOnlyKeyword)
            {
                hasReadOnly = true;
            }
        }

        return hasRef && !hasReadOnly ? "ref" : null;
    }

    /// <summary>Returns whether an attribute's written name is the optional attribute's.</summary>
    /// <param name="name">The attribute name as written.</param>
    /// <returns><see langword="true"/> for <c>Optional</c> and <c>OptionalAttribute</c>, qualified or not.</returns>
    private static bool IsOptionalName(NameSyntax name) => SimpleName(name) is "Optional" or "OptionalAttribute";

    /// <summary>Returns whether the attribute binds to <c>System.Runtime.InteropServices.OptionalAttribute</c>.</summary>
    /// <param name="semanticModel">The semantic model for the attribute's tree.</param>
    /// <param name="attribute">The candidate optional attribute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> for the framework attribute, and not a same-named type.</returns>
    private static bool IsFrameworkOptionalAttribute(SemanticModel semanticModel, AttributeSyntax attribute, CancellationToken cancellationToken)
    {
        var type = semanticModel.GetSymbolInfo(attribute, cancellationToken).Symbol?.ContainingType
            ?? semanticModel.GetTypeInfo(attribute, cancellationToken).Type as INamedTypeSymbol;
        return type is
        {
            Name: "OptionalAttribute",
            ContainingNamespace:
            {
                Name: "InteropServices",
                ContainingNamespace:
                {
                    Name: "Runtime",
                    ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true },
                },
            },
        };
    }

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased name.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string SimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };
}
