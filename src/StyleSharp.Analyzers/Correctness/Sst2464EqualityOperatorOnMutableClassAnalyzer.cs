// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a user-defined <c>operator ==</c> declared on a mutable reference type (SST2464): a <c>class</c>
/// that still exposes a settable field or property. Value-based equality on state that can change is a
/// foot-gun — once an instance is used as a <c>Dictionary</c> or <c>HashSet</c> key and then mutated, its
/// hash no longer matches the bucket it was stored in, so it can never be found again and can corrupt
/// neighbouring entries.
/// </summary>
/// <remarks>
/// <para>
/// The clean path is syntactic: the node must be an <c>operator ==</c> declaration whose containing type is
/// a plain <c>class</c> (a <c>struct</c>, a <c>record</c>, and a <c>record struct</c> are a different syntax
/// node and never reach the bind). Only then is the type bound, to confirm it is a class that is mutable —
/// it declares at least one instance field that is not <c>readonly</c>/<c>const</c>, or an instance property
/// with a <c>set</c> accessor that is not <c>init</c>-only. An immutable class — every field <c>readonly</c>,
/// every property get-only or <c>init</c>-only — is left silent. Binding also merges partial declarations, so
/// state split across files is seen.
/// </para>
/// <para>
/// Only <c>operator ==</c> is reported, once; the paired <c>operator !=</c> is left alone so a single mutable
/// class yields a single diagnostic. Whether the type also overrides <c>Equals</c>/<c>GetHashCode</c> is
/// irrelevant here — the defect is value equality on mutable state, not a missing member.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2464EqualityOperatorOnMutableClassAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.EqualityOperatorOnMutableClass);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeOperator, SyntaxKind.OperatorDeclaration);
    }

    /// <summary>Reports one <c>operator ==</c> declared on a mutable class.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeOperator(SyntaxNodeAnalysisContext context)
    {
        var operatorDeclaration = (OperatorDeclarationSyntax)context.Node;
        if (!operatorDeclaration.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken)
            || operatorDeclaration.Parent is not ClassDeclarationSyntax classDeclaration)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken) is not { TypeKind: TypeKind.Class, IsRecord: false } type
            || !IsMutable(type))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.EqualityOperatorOnMutableClass,
            operatorDeclaration.OperatorToken.GetLocation(),
            type.Name));
    }

    /// <summary>Returns whether a type declares any settable instance state.</summary>
    /// <param name="type">The containing class symbol (partial declarations already merged).</param>
    /// <returns><see langword="true"/> when the class has a non-readonly instance field or a non-init settable property.</returns>
    private static bool IsMutable(INamedTypeSymbol type)
    {
        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (member.IsStatic)
            {
                continue;
            }

            switch (member)
            {
                case IFieldSymbol { IsImplicitlyDeclared: false, IsConst: false, IsReadOnly: false }:
                case IPropertySymbol { SetMethod.IsInitOnly: false }:
                    return true;
            }
        }

        return false;
    }
}
