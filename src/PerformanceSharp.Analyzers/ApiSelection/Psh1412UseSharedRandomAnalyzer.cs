// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports the parameterless <c>new Random()</c> where the runtime already offers <c>Random.Shared</c>
/// (PSH1412) — including the target-typed <c>new()</c> form. The shared instance is thread-safe, costs
/// nothing to take, and cannot repeat the sequence of the last one; a freshly constructed
/// <c>Random</c> can do both, because two allocated close together may be seeded from the same
/// clock tick and one shared across threads without a lock is not safe at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gated on the API, never on a version number.</b> <c>Random.Shared</c> arrived in .NET 6, so the
/// rule probes the compilation for the static <c>Shared</c> property only after a candidate passes
/// the syntax checks. On <c>netstandard2.0</c> or .NET Framework the rule is silent.
/// </para>
/// <para>
/// <b>A seed is a decision, and is never reported.</b> <c>new Random(42)</c> asks for a reproducible
/// sequence — a test, a shuffle that must replay, a deterministic simulation — and <c>Random.Shared</c>
/// cannot give one. The rule matches the argument-free constructor and nothing else. A subclass of
/// <c>Random</c> is left alone too: its constructor's containing type is the subclass, not
/// <c>Random</c>, so overridden behavior is never traded away for the shared instance.
/// </para>
/// <para>
/// <b>A long-lived cached field is still reported.</b> <c>private static readonly Random Rng = new();</c>
/// pays the allocation only once, so the allocation is not the point — but the field is still not
/// thread-safe, and <c>Random.Shared</c> is, for the same one-time cost of nothing. The suggestion holds
/// there and the rule makes it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1412UseSharedRandomAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The simple name of the allocated type.</summary>
    internal const string RandomTypeName = "Random";

    /// <summary>The replacement member.</summary>
    internal const string SharedPropertyName = "Shared";

    /// <summary>The metadata name of the allocated type.</summary>
    private const string RandomMetadataName = "System.Random";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ApiSelectionRules.UseSharedRandom);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, RandomMetadataName),
            AnalyzeCreation,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    /// <summary>Returns whether an allocation is an argument-free <c>new</c> with nothing initialized, before any binding.</summary>
    /// <param name="creation">The allocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    /// <remarks>
    /// An initializer would be assigning properties the shared instance does not let you assign, and an
    /// argument would be a seed. Either way the shape is not one the fix can rewrite.
    /// </remarks>
    internal static bool IsParameterlessCreationShape(BaseObjectCreationExpressionSyntax creation) =>
        creation is { Initializer: null, ArgumentList.Arguments.Count: 0 };

    /// <summary>Reports PSH1412 for an allocation of a <c>Random</c> the shared instance could serve.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="frameworkTypes">The compilation's deferred framework type cache.</param>
    private static void AnalyzeCreation(in SyntaxNodeAnalysisContext context, LazyMetadataType frameworkTypes)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (!IsParameterlessCreationShape(creation) || !IsNamedRandomOrImplicit(creation))
        {
            return;
        }

        var model = context.SemanticModel;
        if (frameworkTypes.Get() is not { } randomType
            || !HasSharedProperty(randomType)
            || model.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, randomType)
            || !SharedInstanceReplacement.CanWriteReplacement(creation, model, randomType, RandomTypeName, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.UseSharedRandom,
            creation.SyntaxTree,
            creation.Span,
            RandomTypeName));
    }

    /// <summary>Rejects an explicit allocation of anything not written as <c>Random</c>, without binding it.</summary>
    /// <param name="creation">The allocation to inspect.</param>
    /// <returns><see langword="true"/> for a target-typed <c>new()</c>, or an explicit <c>new Random()</c>.</returns>
    /// <remarks>
    /// A target-typed <c>new()</c> names nothing, so it has to be bound to be judged; every other
    /// <c>new Foo()</c> in the file is settled by a string comparison instead.
    /// </remarks>
    private static bool IsNamedRandomOrImplicit(BaseObjectCreationExpressionSyntax creation) =>
        creation is not ObjectCreationExpressionSyntax explicitCreation
            || SyntaxNames.GetSimpleName(explicitCreation.Type) == RandomTypeName;

    /// <summary>Returns whether the compilation's <c>Random</c> exposes the shared instance.</summary>
    /// <param name="randomType">The compilation's <c>Random</c> type.</param>
    /// <returns><see langword="true"/> when the static <c>Shared</c> property exists (.NET 6 and later).</returns>
    private static bool HasSharedProperty(INamedTypeSymbol randomType)
    {
        var members = randomType.GetMembers(SharedPropertyName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IPropertySymbol { IsStatic: true, GetMethod: not null })
            {
                return true;
            }
        }

        return false;
    }
}
