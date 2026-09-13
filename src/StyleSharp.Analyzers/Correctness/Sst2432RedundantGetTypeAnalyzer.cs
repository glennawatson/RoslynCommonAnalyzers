// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>GetType()</c> called on a value that is already a <see cref="System.Type"/> (SST2432).
/// The call returns the runtime type of the reflection object itself (the internal <c>RuntimeType</c>),
/// never the type the value describes, so it is silent and always wrong.
/// </summary>
/// <remarks>
/// The clean path is a syntax check: only <c>receiver.GetType()</c> with no arguments reaches the semantic
/// model, which rejects every other invocation before a bind. <see cref="System.Type"/> is resolved only
/// after a candidate survives the syntax check, and the rule reports nothing when the type is absent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2432RedundantGetTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The reflection method whose redundant use is reported.</summary>
    private const string GetTypeName = "GetType";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.RedundantGetType);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static startContext =>
        {
            var reflectionTypes = new ReflectionTypes(startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, reflectionTypes), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports one <c>GetType()</c> call whose receiver is already a Type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="reflectionTypes">The reflection type cache for this compilation.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, ReflectionTypes reflectionTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: GetTypeName } memberAccess)
        {
            return;
        }

        if (reflectionTypes.Get() is not { } systemType)
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

    /// <summary>Resolves System.Type on first demand within one compilation.</summary>
    /// <param name="compilation">The compilation whose references are searched.</param>
    private sealed class ReflectionTypes(Compilation compilation)
    {
        /// <summary>Stores the resolved symbol, including a missing result, in an atomically assigned array.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets System.Type, resolving it on first demand.</summary>
        /// <returns>The reflection type, or null when it is absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName("System.Type")])[0];
    }
}
