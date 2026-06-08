// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a parameterless value-type construction (<c>new T()</c>) that produces the type's zero
/// value and reads more clearly as <c>default(T)</c> (SST1129). A struct that declares its own
/// parameterless constructor is not flagged, because that constructor runs real code and is not
/// equivalent to <c>default</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1129DefaultValueTypeConstructorAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.DefaultValueTypeConstructor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ObjectCreationExpression);
    }

    /// <summary>Reports a parameterless value-type construction.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is not { Arguments.Count: 0 } || creation.Initializer is not null)
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not { IsValueType: true })
        {
            return;
        }

        // A user-declared parameterless struct constructor runs code, so 'new T()' is not 'default'.
        if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is IMethodSymbol { IsImplicitlyDeclared: false })
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.DefaultValueTypeConstructor, creation.GetLocation()));
    }
}
