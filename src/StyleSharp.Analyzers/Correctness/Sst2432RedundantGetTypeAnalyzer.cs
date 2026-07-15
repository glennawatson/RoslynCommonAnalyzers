// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>GetType()</c> called on a value that is already a <see cref="System.Type"/> (SST2432).
/// The call returns the runtime type of the reflection object itself (the internal <c>RuntimeType</c>),
/// never the type the value describes, so it is silent and always wrong.
/// </summary>
/// <remarks>
/// The clean path is a syntax check: only <c>receiver.GetType()</c> with no arguments reaches the semantic
/// model, which rejects every other invocation before a bind. <see cref="System.Type"/> is resolved once per
/// compilation and the whole rule is gated on it, so a compilation that somehow lacks the type pays nothing.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2432RedundantGetTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The reflection method whose redundant use is reported.</summary>
    private const string GetTypeName = "GetType";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.RedundantGetType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the invocation walk only when <see cref="System.Type"/> resolves.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName("System.Type") is not { } systemType)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, systemType), SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports one <c>GetType()</c> call whose receiver is already a Type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="systemType">The resolved <see cref="System.Type"/> symbol.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol systemType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: GetTypeName } memberAccess)
        {
            return;
        }

        // The parameterless GetType inherited from object binds, on a Type receiver, to a symbol whose
        // containing type is Type itself, so the receiver already being a Type is the real signal: a
        // parameterless GetType() call on it returns the runtime type of the reflection object.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol
            {
                Name: GetTypeName,
                Parameters.IsEmpty: true,
            })
        {
            return;
        }

        if (!InheritsFromType(context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type, systemType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.RedundantGetType,
            invocation.GetLocation(),
            memberAccess.Expression.ToString()));
    }

    /// <summary>Returns whether a type is <see cref="System.Type"/> or derives from it.</summary>
    /// <param name="candidate">The receiver's type.</param>
    /// <param name="systemType">The resolved <see cref="System.Type"/> symbol.</param>
    /// <returns><see langword="true"/> when the receiver is already a Type.</returns>
    private static bool InheritsFromType(ITypeSymbol? candidate, INamedTypeSymbol systemType)
    {
        for (var current = candidate; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, systemType))
            {
                return true;
            }
        }

        return false;
    }
}
