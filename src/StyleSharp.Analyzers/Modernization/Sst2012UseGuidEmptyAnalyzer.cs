// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the parameterless <c>new Guid()</c> (SST2012). It does not make a new GUID — it makes the all-zero
/// one — and <c>Guid.Empty</c> is the name of that value. <c>Guid.NewGuid()</c> and every seeded constructor
/// are left alone: they mean what they say.
/// </summary>
/// <remarks>
/// An explicit <c>new Guid()</c> is gated on the type being spelled <c>Guid</c> before anything is bound, so a
/// parameterless construction of some other type costs one string comparison. A target-typed <c>new()</c> has
/// no type to read, so it is bound — but only when its argument list is empty, which most constructions are
/// not.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2012UseGuidEmptyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the constructed type.</summary>
    internal const string GuidMetadataName = "System.Guid";

    /// <summary>The type name used by the syntax gate.</summary>
    private const string GuidTypeName = "Guid";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.UseGuidEmpty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var guid = start.Compilation.GetTypeByMetadataName(GuidMetadataName);
            if (guid is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, guid),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Reports one parameterless construction of a <c>Guid</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="guid">The <c>System.Guid</c> symbol for this compilation.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol guid)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is not { Arguments.Count: 0 } || creation.Initializer is not null)
        {
            return;
        }

        if (creation is ObjectCreationExpressionSyntax explicitCreation && GetSimpleName(explicitCreation.Type) != GuidTypeName)
        {
            return;
        }

        // The type is read rather than the constructor: a struct's parameterless constructor is synthesized,
        // and a target-typed 'new()' has no type syntax to read at all.
        var created = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;
        if (!SymbolEqualityComparer.Default.Equals(created, guid))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernizationRules.UseGuidEmpty, creation.GetLocation()));
    }

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased type name.</summary>
    /// <param name="type">The constructed type's syntax.</param>
    /// <returns>The simple name, or an empty string when the type is not a name.</returns>
    private static string GetSimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };
}
