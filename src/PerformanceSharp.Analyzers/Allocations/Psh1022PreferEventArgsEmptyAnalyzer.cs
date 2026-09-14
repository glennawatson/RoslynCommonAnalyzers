// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports the parameterless <c>new EventArgs()</c> where the runtime already exposes the
/// <c>EventArgs.Empty</c> singleton (PSH1022) — including the target-typed <c>new()</c> form. A
/// parameterless <c>EventArgs</c> carries no state, so every construction allocates an object that
/// is indistinguishable from the one shared instance the framework keeps for exactly this purpose;
/// raising an event with <c>EventArgs.Empty</c> allocates nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only the exact type, never a subclass.</b> A derived <c>EventArgs</c> exists to carry data, so
/// its construction is not redundant even when written without arguments. The rule matches only a
/// construction whose bound constructor's containing type is <c>System.EventArgs</c> itself, so
/// <c>new MyEventArgs()</c> is left alone: its constructor's containing type is the subclass.
/// </para>
/// <para>
/// <b>Only the parameterless shape.</b> An argument or an object initializer means the construction
/// is doing something the shared singleton cannot, so neither is reported. The clean path costs one
/// token check on the construction and no binding.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1022PreferEventArgsEmptyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The simple name of the constructed type.</summary>
    internal const string EventArgsTypeName = "EventArgs";

    /// <summary>The replacement member.</summary>
    internal const string EmptyFieldName = "Empty";

    /// <summary>The metadata name of the constructed type.</summary>
    private const string EventArgsMetadataName = "System.EventArgs";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(AllocationRules.PreferEventArgsEmpty);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyCompilationValue<INamedTypeSymbol?>(compilation, ResolveEventArgsType),
            AnalyzeCreation,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    /// <summary>Returns whether an allocation is an argument-free <c>new</c> with nothing initialized, before any binding.</summary>
    /// <param name="creation">The allocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    /// <remarks>
    /// An initializer or an argument is state the shared instance does not carry, so the shape is not
    /// one the fix can rewrite.
    /// </remarks>
    internal static bool IsParameterlessCreationShape(BaseObjectCreationExpressionSyntax creation) =>
        creation is { Initializer: null, ArgumentList.Arguments.Count: 0 };

    /// <summary>Reports PSH1022 for a construction of the base <c>EventArgs</c> the singleton could serve.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation's deferred <c>EventArgs</c> type.</param>
    private static void AnalyzeCreation(in SyntaxNodeAnalysisContext context, LazyCompilationValue<INamedTypeSymbol?> types)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (!IsParameterlessCreationShape(creation)
            || !IsNamedEventArgsOrImplicit(creation)
            || types.Get() is not { } eventArgsType)
        {
            return;
        }

        var model = context.SemanticModel;
        if (model.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, eventArgsType)
            || !SharedInstanceReplacement.CanWriteReplacement(creation, model, eventArgsType, EventArgsTypeName, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.PreferEventArgsEmpty,
            creation.SyntaxTree,
            creation.Span));
    }

    /// <summary>Rejects an explicit construction of anything not written as <c>EventArgs</c>, without binding it.</summary>
    /// <param name="creation">The allocation to inspect.</param>
    /// <returns><see langword="true"/> for a target-typed <c>new()</c>, or an explicit <c>new EventArgs()</c>.</returns>
    /// <remarks>
    /// A target-typed <c>new()</c> names nothing, so it has to be bound to be judged; every other
    /// <c>new Foo()</c> in the file is settled by a string comparison instead.
    /// </remarks>
    private static bool IsNamedEventArgsOrImplicit(BaseObjectCreationExpressionSyntax creation) =>
        creation is not ObjectCreationExpressionSyntax explicitCreation
            || SyntaxNames.GetSimpleName(explicitCreation.Type) == EventArgsTypeName;

    /// <summary>Resolves the type and verifies that its singleton exists.</summary>
    /// <param name="compilation">The compilation whose type is resolved.</param>
    /// <returns>The singleton-bearing type, or null when unavailable.</returns>
    private static INamedTypeSymbol? ResolveEventArgsType(Compilation compilation)
    {
        var type = compilation.GetTypeByMetadataName(EventArgsMetadataName);
        return type is not null && SymbolFacts.HasStaticField(type, EmptyFieldName) ? type : null;
    }
}
