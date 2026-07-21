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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.PreferEventArgsEmpty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(EventArgsMetadataName) is not { } eventArgsType
                || !HasEmptyField(eventArgsType))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeCreation(nodeContext, eventArgsType),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Returns whether an allocation is an argument-free <c>new</c> with nothing initialized, before any binding.</summary>
    /// <param name="creation">The allocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    /// <remarks>
    /// An initializer or an argument is state the shared instance does not carry, so the shape is not
    /// one the fix can rewrite.
    /// </remarks>
    internal static bool IsParameterlessCreationShape(BaseObjectCreationExpressionSyntax creation)
        => creation is { Initializer: null, ArgumentList.Arguments.Count: 0 };

    /// <summary>Reports PSH1022 for a construction of the base <c>EventArgs</c> the singleton could serve.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="eventArgsType">The compilation's <c>EventArgs</c> type.</param>
    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol eventArgsType)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (!IsParameterlessCreationShape(creation) || !IsNamedEventArgsOrImplicit(creation))
        {
            return;
        }

        var model = context.SemanticModel;
        if (model.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, eventArgsType)
            || !CanWriteReplacement(creation, model, eventArgsType, context.CancellationToken))
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
    private static bool IsNamedEventArgsOrImplicit(BaseObjectCreationExpressionSyntax creation)
        => creation is not ObjectCreationExpressionSyntax explicitCreation
            || GetSimpleName(explicitCreation.Type) == EventArgsTypeName;

    /// <summary>Returns whether the fix can name <c>EventArgs</c> at the allocation's position.</summary>
    /// <param name="creation">The allocation being reported.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="eventArgsType">The compilation's <c>EventArgs</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a compiling replacement exists.</returns>
    /// <remarks>
    /// An explicit allocation already spells the type out, and the fix reuses exactly what the author
    /// wrote — <c>System.EventArgs</c> stays <c>System.EventArgs.Empty</c>. A target-typed <c>new()</c>
    /// spells nothing out, so the fix has to write <c>EventArgs</c> itself, and that only compiles
    /// where the simple name is in scope. Where it is not, the diagnostic would have no fix, so it is
    /// not reported.
    /// </remarks>
    private static bool CanWriteReplacement(
        BaseObjectCreationExpressionSyntax creation,
        SemanticModel model,
        INamedTypeSymbol eventArgsType,
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
        foreach (var candidate in model.LookupNamespacesAndTypes(creation.SpanStart, name: EventArgsTypeName))
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, eventArgsType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the compilation's <c>EventArgs</c> exposes the shared empty instance.</summary>
    /// <param name="eventArgsType">The compilation's <c>EventArgs</c> type.</param>
    /// <returns><see langword="true"/> when the static <c>Empty</c> field exists.</returns>
    private static bool HasEmptyField(INamedTypeSymbol eventArgsType)
    {
        var members = eventArgsType.GetMembers(EmptyFieldName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IFieldSymbol { IsStatic: true })
            {
                return true;
            }
        }

        return false;
    }

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
}
