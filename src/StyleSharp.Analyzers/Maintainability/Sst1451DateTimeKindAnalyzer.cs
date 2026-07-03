// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Flags <c>DateTime</c> constructor calls that never state a <c>DateTimeKind</c> (SST1451). A
/// kindless DateTime is <c>DateTimeKind.Unspecified</c>, and later conversions guess whether it
/// was UTC or local — the classic source of silent timezone bugs. Constructions that pass a kind
/// (or a <c>Calendar</c>-plus-kind overload) are clean; the parameterless constructor is also left
/// alone because it is just <c>default(DateTime)</c>. Explicit creations are syntax-gated on the
/// type name spelling <c>DateTime</c>; only target-typed <c>new(...)</c> binds unconditionally.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1451DateTimeKindAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The type name used by the syntax gate.</summary>
    private const string DateTimeTypeName = "DateTime";

    /// <summary>The kind parameter type name.</summary>
    private const string DateTimeKindTypeName = "DateTimeKind";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.DateTimeKindRequired);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            AnalyzeCreation,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    /// <summary>Reports a DateTime construction whose overload takes no kind.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is not { Arguments.Count: > 0 })
        {
            return;
        }

        if (creation is ObjectCreationExpressionSyntax explicitCreation && !TypeNameIsDateTime(explicitCreation.Type))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol constructor)
        {
            return;
        }

        if (constructor.ContainingType is not { Name: DateTimeTypeName, ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } })
        {
            return;
        }

        var parameters = constructor.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Type.Name == DateTimeKindTypeName)
            {
                return;
            }
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.DateTimeKindRequired,
            creation.SyntaxTree,
            creation.Span));
    }

    /// <summary>Returns whether a creation's type syntax spells <c>DateTime</c>.</summary>
    /// <param name="type">The created type syntax.</param>
    /// <returns><see langword="true"/> when the rightmost identifier is DateTime.</returns>
    private static bool TypeNameIsDateTime(TypeSyntax type)
        => type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == DateTimeTypeName,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == DateTimeTypeName,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText == DateTimeTypeName,
            _ => false,
        };
}
