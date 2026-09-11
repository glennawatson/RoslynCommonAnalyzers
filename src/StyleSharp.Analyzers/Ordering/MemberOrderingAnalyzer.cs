// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Checks that the members of a type appear in the conventional order
/// (SST1201–SST1214): by kind, then accessibility, then constant, then static,
/// then readonly. Each adjacent out-of-order pair reports the first dimension it
/// violates. The whole type's members are ranked in a single syntactic pass.
/// </summary>
/// <remarks>
/// Diagnostics: SST1201, SST1202, SST1203, SST1204, SST1214, SST1215.
/// </remarks>
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
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(
        OrderingRules.OrderByKind,
        OrderingRules.OrderByAccess,
        OrderingRules.ConstantsBeforeFields,
        OrderingRules.StaticBeforeInstance,
        OrderingRules.ReadonlyBeforeNonReadonly,
        OrderingRules.InstanceReadonlyBeforeNonReadonly);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var unionMarkerCache = new UnionMarkerCache();
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, unionMarkerCache),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration,
                SyntaxKind.InterfaceDeclaration);
        });
    }

    /// <summary>Returns whether a member list contains nested class/record declarations that may need union detection.</summary>
    /// <param name="members">The member list to scan.</param>
    /// <returns><see langword="true"/> when union-marker resolution may be needed.</returns>
    internal static bool HasUnionCandidateMembers(SyntaxList<MemberDeclarationSyntax> members)
    {
        for (var i = 0; i < members.Count; i++)
        {
            switch (members[i])
            {
                case ClassDeclarationSyntax or RecordDeclarationSyntax { ClassOrStructKeyword.RawKind: 0 }:
                    return true;
            }
        }

        return false;
    }

    /// <summary>Orders a type's members and, for C# 14 extension blocks, the members nested inside them.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="unionMarkerCache">The lazy cache for the resolved <c>IUnion</c> marker.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, UnionMarkerCache unionMarkerCache)
    {
        var members = ((TypeDeclarationSyntax)context.Node).Members;
        var unionMarker = HasUnionCandidateMembers(members)
            ? unionMarkerCache.Get(context.SemanticModel.Compilation)
            : null;

        OrderMembers(context, members, unionMarker);

        // An extension block is itself a TypeDeclarationSyntax but is not one of the kinds we
        // visit independently, so the pass above skips it. Order its members here. Detecting it
        // by "TypeDeclaration we don't otherwise visit" keeps this version-tolerant — the C# 14
        // ExtensionBlockDeclaration kind need not be named (it does not exist on the 4.8 floor).
        foreach (var member in members)
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
    private static void OrderMembers(in SyntaxNodeAnalysisContext context, SyntaxList<MemberDeclarationSyntax> members, INamedTypeSymbol? unionMarker)
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
    private static bool IsIndependentlyVisited(TypeDeclarationSyntax type) =>
        type.Kind() is SyntaxKind.ClassDeclaration
            or SyntaxKind.StructDeclaration
            or SyntaxKind.RecordDeclaration
            or SyntaxKind.RecordStructDeclaration
            or SyntaxKind.InterfaceDeclaration;

    /// <summary>Lazily caches the resolved union marker for one compilation start.</summary>
    private sealed class UnionMarkerCache
    {
        /// <summary>The cached union marker, once resolved.</summary>
        private INamedTypeSymbol? _marker;

        /// <summary>The latch that publishes <see cref="_marker"/>, zero until it holds the resolved marker.</summary>
        private int _resolved;

        /// <summary>Gets the cached union marker, resolving it only when first needed.</summary>
        /// <param name="compilation">The compilation that may contain the marker.</param>
        /// <returns>The resolved marker, or <see langword="null"/>.</returns>
        /// <remarks>
        /// Resolution is deterministic for one compilation, so a caller that arrives before the latch is set
        /// resolves the marker itself and hands back what it resolved rather than waiting on the winner. The
        /// latch exists to publish the field: the exchange releases the write of <see cref="_marker"/>, and the
        /// volatile read acquires it, so a later caller never reads the field back empty.
        /// </remarks>
        public INamedTypeSymbol? Get(Compilation compilation)
        {
            if (Volatile.Read(ref _resolved) != 0)
            {
                return _marker;
            }

            var marker = MemberOrder.ResolveUnionMarker(compilation);
            _marker = marker;
            _ = Interlocked.Exchange(ref _resolved, 1);
            return marker;
        }
    }
}
