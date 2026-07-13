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
/// rule probes the compilation for the static <c>Shared</c> property and registers nothing at all when
/// it is absent — on <c>netstandard2.0</c> or .NET Framework the rule is silent, and costs one type
/// lookup per compilation to find that out.
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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.UseSharedRandom);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(RandomMetadataName) is not { } randomType
                || !HasSharedProperty(randomType))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeCreation(nodeContext, randomType),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Returns whether an allocation is an argument-free <c>new</c> with nothing initialized, before any binding.</summary>
    /// <param name="creation">The allocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    /// <remarks>
    /// An initializer would be assigning properties the shared instance does not let you assign, and an
    /// argument would be a seed. Either way the shape is not one the fix can rewrite.
    /// </remarks>
    internal static bool IsParameterlessCreationShape(BaseObjectCreationExpressionSyntax creation)
        => creation is { Initializer: null, ArgumentList.Arguments.Count: 0 };

    /// <summary>Returns the rightmost identifier of a written type name.</summary>
    /// <param name="type">The written type syntax.</param>
    /// <returns>The simple name, or <see langword="null"/> when the syntax names no simple type.</returns>
    private static string? GetSimpleName(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => GetSimpleName(alias.Name),
        _ => null,
    };

    /// <summary>Reports PSH1412 for an allocation of a <c>Random</c> the shared instance could serve.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="randomType">The compilation's <c>Random</c> type.</param>
    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol randomType)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (!IsParameterlessCreationShape(creation) || !IsNamedRandomOrImplicit(creation))
        {
            return;
        }

        var model = context.SemanticModel;
        if (model.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, randomType)
            || !CanWriteReplacement(creation, model, randomType, context.CancellationToken))
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
    private static bool IsNamedRandomOrImplicit(BaseObjectCreationExpressionSyntax creation)
        => creation is not ObjectCreationExpressionSyntax explicitCreation
            || GetSimpleName(explicitCreation.Type) == RandomTypeName;

    /// <summary>Returns whether the fix can name <c>Random</c> at the allocation's position.</summary>
    /// <param name="creation">The allocation being reported.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="randomType">The compilation's <c>Random</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a compiling replacement exists.</returns>
    /// <remarks>
    /// An explicit allocation already spells the type out, and the fix reuses exactly what the author
    /// wrote — <c>System.Random</c> stays <c>System.Random.Shared</c>. A target-typed <c>new()</c> spells
    /// nothing out, so the fix has to write <c>Random</c> itself, and that only compiles where the simple
    /// name is in scope. Where it is not, the diagnostic would have no fix, so it is not reported.
    /// </remarks>
    private static bool CanWriteReplacement(
        BaseObjectCreationExpressionSyntax creation,
        SemanticModel model,
        INamedTypeSymbol randomType,
        CancellationToken cancellationToken)
    {
        if (creation is ObjectCreationExpressionSyntax { Type: NameSyntax })
        {
            return true;
        }

        if (creation is ObjectCreationExpressionSyntax)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach (var candidate in model.LookupNamespacesAndTypes(creation.SpanStart, name: RandomTypeName))
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, randomType))
            {
                return true;
            }
        }

        return false;
    }

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
