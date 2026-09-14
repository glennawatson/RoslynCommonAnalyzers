// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>The cached set of methods a compilation converts to a delegate somewhere, resolved on demand.</summary>
/// <param name="compilation">The compilation to search.</param>
/// <remarks>
/// <para>
/// Whether a method is ever used as a method group is a whole-compilation fact, and collecting it in a
/// compilation-end action makes the resulting diagnostic non-local, which Roslyn refuses to offer a code
/// fix for. Resolving it here instead keeps the diagnostic on the parameter that carries it.
/// </para>
/// <para>
/// The walk is the expensive part, so it runs at most once and only when a rule actually reaches a
/// candidate. A name that is invoked where it stands is not a method group and is rejected on syntax
/// alone, which is nearly every mention of a method in a codebase; only what survives is bound.
/// </para>
/// </remarks>
internal sealed class MethodGroupTargets(Compilation compilation)
{
    /// <summary>Guards the one-time walk.</summary>
    private readonly object _gate = new();

    /// <summary>The resolved methods, or <see langword="null"/> before the first query.</summary>
    private HashSet<ISymbol>? _targets;

    /// <summary>Returns whether the compilation converts a method to a delegate anywhere.</summary>
    /// <param name="method">The method's original definition.</param>
    /// <param name="cancellationToken">A token that cancels the walk.</param>
    /// <returns><see langword="true"/> when the method is used as a method group.</returns>
    /// <remarks>
    /// Every call after the first is a volatile read, so callbacks never contend. The gate is only
    /// reached while the set is still missing, and it is what keeps concurrent first queries from
    /// each running the walk and discarding all but one result.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Contains(ISymbol method, CancellationToken cancellationToken) =>
        (Volatile.Read(ref _targets) ?? Resolve(cancellationToken)).Contains(method);

    /// <summary>Walks every tree collecting the methods named outside a call.</summary>
    /// <param name="compilation">The compilation to search.</param>
    /// <param name="cancellationToken">A token that cancels the walk.</param>
    /// <returns>The original definitions of every method used as a method group.</returns>
    private static HashSet<ISymbol> Collect(Compilation compilation, CancellationToken cancellationToken)
    {
        var targets = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = new MethodGroupScanState(compilation, tree, targets, cancellationToken);
            _ = DescendantTraversalHelper.VisitDescendants(
                tree.GetRoot(cancellationToken),
                ref state,
                static (SimpleNameSyntax name, ref MethodGroupScanState current) =>
                {
                    if (IsCalledWhereItStands(name) || IsNamingSomethingOtherThanAValue(name) || IsNamingAnExpressionTypeOrArgument(name))
                    {
                        return true;
                    }

                    current.Model ??= current.Compilation.GetSemanticModel(current.Tree);
                    if (current.Model.GetSymbolInfo(name, current.CancellationToken).Symbol is IMethodSymbol referenced)
                    {
                        _ = current.Targets.Add(referenced.OriginalDefinition);
                    }

                    return true;
                });
        }

        return targets;
    }

    /// <summary>Returns whether a name is the target of a call written at that name.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when the name is invoked rather than referenced.</returns>
    private static bool IsCalledWhereItStands(SimpleNameSyntax name) => name.Parent is InvocationExpressionSyntax direct
        ? direct.Expression == name
        : name.Parent is MemberAccessExpressionSyntax access
            && access.Name == name
            && access.Parent is InvocationExpressionSyntax through
            && through.Expression == access;

    /// <summary>Returns whether a name sits where a type or a namespace is written, not a value.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when no method group can appear at that position.</returns>
    /// <remarks>
    /// Only positions that cannot hold an expression are rejected. Anything else is bound, because a name
    /// wrongly treated as a non-value would leave a delegate target unrecorded, and the rule would then
    /// suggest a signature the conversion no longer matches.
    /// </remarks>
    private static bool IsNamingSomethingOtherThanAValue(SimpleNameSyntax name) => name.Parent switch
    {
        TypeSyntax
            or BaseTypeSyntax
            or BaseNamespaceDeclarationSyntax
            or UsingDirectiveSyntax
            or TypeArgumentListSyntax
            or TypeParameterConstraintClauseSyntax
            or AttributeSyntax or ObjectCreationExpressionSyntax => true,
        _ => NamesADeclaredType(name),
    };

    /// <summary>Rejects names used as types or named-argument labels within expressions and constraints.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns>Whether the name's position excludes a method group.</returns>
    private static bool IsNamingAnExpressionTypeOrArgument(SimpleNameSyntax name) => name.Parent is
        NameColonSyntax
        or NameEqualsSyntax
        or TypeOfExpressionSyntax
        or SizeOfExpressionSyntax
        or DefaultExpressionSyntax
        or DeclarationExpressionSyntax
        or CatchDeclarationSyntax
        or TypeConstraintSyntax;

    /// <summary>Returns whether a name is the declared type of the member it belongs to.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when the name is a declaration's type rather than a value.</returns>
    private static bool NamesADeclaredType(SimpleNameSyntax name) => name.Parent switch
    {
        ParameterSyntax parameter => parameter.Type == name,
        VariableDeclarationSyntax declaration => declaration.Type == name,
        MethodDeclarationSyntax method => method.ReturnType == name,
        LocalFunctionStatementSyntax localFunction => localFunction.ReturnType == name,
        PropertyDeclarationSyntax property => property.Type == name,
        IndexerDeclarationSyntax indexer => indexer.Type == name,
        EventDeclarationSyntax @event => @event.Type == name,
        OperatorDeclarationSyntax @operator => @operator.ReturnType == name,
        ConversionOperatorDeclarationSyntax conversion => conversion.Type == name,
        DelegateDeclarationSyntax @delegate => @delegate.ReturnType == name,
        _ => false,
    };

    /// <summary>Runs the walk once and publishes it, off the inlined fast path.</summary>
    /// <param name="cancellationToken">A token that cancels the walk.</param>
    /// <returns>The resolved methods.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private HashSet<ISymbol> Resolve(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var targets = _targets;
            if (targets is null)
            {
                targets = Collect(compilation, cancellationToken);
                Volatile.Write(ref _targets, targets);
            }

            return targets;
        }
    }

    /// <summary>Carries the per-tree method-group collection state without capturing a closure.</summary>
    /// <param name="Compilation">The compilation used to resolve the semantic model on demand.</param>
    /// <param name="Tree">The syntax tree being visited.</param>
    /// <param name="Targets">The collected method definitions.</param>
    /// <param name="CancellationToken">A token that cancels symbol resolution.</param>
    private record struct MethodGroupScanState(
        Compilation Compilation,
        SyntaxTree Tree,
        HashSet<ISymbol> Targets,
        CancellationToken CancellationToken)
    {
        /// <summary>Gets or sets the semantic model, resolved only for a possible method group.</summary>
        public SemanticModel? Model { get; set; }
    }
}
