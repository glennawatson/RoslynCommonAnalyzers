// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Checks that the members of a type appear in StyleCop's conventional order
/// (SST1201–SST1214): by kind, then accessibility, then constant, then static,
/// then readonly. Each adjacent out-of-order pair reports the first dimension it
/// violates. The whole type's members are ranked in a single syntactic pass.
/// </summary>
/// <remarks>
/// Nested unions sort after classes and records. Because C# 15 union syntax is not
/// yet exposed by Roslyn, unions are detected by the
/// <c>System.Runtime.CompilerServices.IUnion</c> marker interface — so the only
/// semantic work happens when that marker is in the compilation and a nested
/// class/record is present; otherwise the pass stays purely syntactic.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberOrderingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        OrderingRules.OrderByKind,
        OrderingRules.OrderByAccess,
        OrderingRules.ConstantsBeforeFields,
        OrderingRules.StaticBeforeInstance,
        OrderingRules.ReadonlyBeforeNonReadonly);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var unionMarker = MemberOrder.ResolveUnionMarker(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, unionMarker),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration,
                SyntaxKind.InterfaceDeclaration);
        });
    }

    /// <summary>Orders a type's members and, for C# 14 extension blocks, the members nested inside them.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="unionMarker">The resolved <c>IUnion</c> marker, or <see langword="null"/> when no unions exist.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? unionMarker)
    {
        var type = (TypeDeclarationSyntax)context.Node;
        OrderMembers(context, type.Members, unionMarker);

        // An extension block is itself a TypeDeclarationSyntax but is not one of the kinds we
        // visit independently, so the pass above skips it. Order its members here. Detecting it
        // by "TypeDeclaration we don't otherwise visit" keeps this version-tolerant — the C# 14
        // ExtensionBlockDeclaration kind need not be named (it does not exist on the 4.8 floor).
        foreach (var member in type.Members)
        {
            if (member is TypeDeclarationSyntax nested && !IsIndependentlyVisited(nested))
            {
                OrderMembers(context, nested.Members, unionMarker);
            }
        }
    }

    /// <summary>Reports the first ordering dimension each adjacent member pair violates.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="members">The member list to order.</param>
    /// <param name="unionMarker">The resolved <c>IUnion</c> marker, or <see langword="null"/>.</param>
    private static void OrderMembers(SyntaxNodeAnalysisContext context, SyntaxList<MemberDeclarationSyntax> members, INamedTypeSymbol? unionMarker)
    {
        if (members.Count < 2)
        {
            return;
        }

        MemberOrder? previous = null;
        foreach (var member in members)
        {
            var isUnion = unionMarker is not null
                && MemberOrder.IsUnion(member, context.SemanticModel, unionMarker, context.CancellationToken);
            if (MemberOrder.Classify(member, isUnion) is not { } current)
            {
                continue;
            }

            if (previous is { } prior && current.ViolationAfter(prior) is { } rule)
            {
                var token = MemberOrder.NameToken(member);
                context.ReportDiagnostic(Diagnostic.Create(rule, token.GetLocation(), token.ValueText));
            }

            previous = current;
        }
    }

    /// <summary>Returns whether a nested type declaration is one the analyzer already visits on its own.</summary>
    /// <param name="type">The nested type declaration.</param>
    /// <returns><see langword="true"/> for the standard type kinds; <see langword="false"/> for extension blocks.</returns>
    private static bool IsIndependentlyVisited(TypeDeclarationSyntax type)
        => type.Kind() is SyntaxKind.ClassDeclaration
            or SyntaxKind.StructDeclaration
            or SyntaxKind.RecordDeclaration
            or SyntaxKind.RecordStructDeclaration
            or SyntaxKind.InterfaceDeclaration;
}
