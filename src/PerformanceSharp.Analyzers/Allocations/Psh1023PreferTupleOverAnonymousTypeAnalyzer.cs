// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a local anonymous type that could be a tuple (PSH1023). The anonymous type is a class, so it
/// costs an allocation and a later collection; the tuple carrying the same named members is a struct.
/// </summary>
/// <remarks>
/// <para>
/// Only a local whose every use is a member read is reported. That is the case where the type is a private
/// detail of one method and swapping it cannot be observed: the moment the value is returned, passed on,
/// stored, or captured, its type is part of a contract this analyzer cannot see all of, and a query
/// provider or serializer may depend on it being a class.
/// </para>
/// <para>
/// The callback is registered on the anonymous object creation itself, which is rare, so the escape scan
/// runs only for a construct that is already a candidate. The scan walks the declaring block once through
/// the shared traversal helper rather than materialising a descendant iterator.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1023PreferTupleOverAnonymousTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The language version that introduced tuples with named members.</summary>
    private const int CSharp7 = 700;

    /// <summary>The number of members below which a tuple is not worth suggesting.</summary>
    private const int MinimumMembers = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.PreferTupleOverAnonymousType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AnonymousObjectCreationExpression);
    }

    /// <summary>Reports an anonymous type that only ever has its members read inside one method.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var creation = (AnonymousObjectCreationExpressionSyntax)context.Node;
        if (!IsLanguageSupported(creation)
            || creation.Initializers.Count < MinimumMembers
            || !AllMembersAreNamed(creation.Initializers))
        {
            return;
        }

        if (creation.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }
            || declarator.Parent is not VariableDeclarationSyntax { Parent: LocalDeclarationStatementSyntax local }
            || local.Parent is not BlockSyntax block)
        {
            return;
        }

        if (EscapesTheBlock(block, declarator.Identifier.ValueText))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(AllocationRules.PreferTupleOverAnonymousType, creation.GetLocation()));
    }

    /// <summary>Returns whether every member of the anonymous type has a name a tuple can carry.</summary>
    /// <param name="initializers">The anonymous type's member declarators.</param>
    /// <returns><see langword="true"/> when each member is explicitly named or infers a name.</returns>
    private static bool AllMembersAreNamed(SeparatedSyntaxList<AnonymousObjectMemberDeclaratorSyntax> initializers)
    {
        for (var i = 0; i < initializers.Count; i++)
        {
            var initializer = initializers[i];
            if (initializer.NameEquals is not null)
            {
                continue;
            }

            // An unnamed member takes its name from the expression, which only works for a simple name.
            if (initializer.Expression is not (IdentifierNameSyntax or MemberAccessExpressionSyntax))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether the local is used as anything other than a receiver for a member read.</summary>
    /// <param name="block">The block that declares the local.</param>
    /// <param name="name">The local's name.</param>
    /// <returns><see langword="true"/> when any use could observe the anonymous type itself.</returns>
    private static bool EscapesTheBlock(BlockSyntax block, string name)
    {
        var state = new EscapeScan(name, Escapes: false);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, EscapeScan>(block, ref state, static (node, ref scan) =>
        {
            if (node.Identifier.ValueText != scan.Name)
            {
                return true;
            }

            // A read of x.Member is the only use a tuple reproduces exactly.
            if (node.Parent is MemberAccessExpressionSyntax access && access.Expression == node)
            {
                return true;
            }

            scan.Escapes = true;
            return false;
        });

        return state.Escapes;
    }

    /// <summary>Returns whether the tree is parsed at a language version that has named tuples.</summary>
    /// <param name="node">A node in the syntax tree.</param>
    /// <returns><see langword="true"/> for C# 7 or later.</returns>
    private static bool IsLanguageSupported(SyntaxNode node)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= CSharp7;

    /// <summary>Threads the local's name and the verdict through the escape traversal.</summary>
    /// <param name="Name">The local's name.</param>
    /// <param name="Escapes">Whether a use was found that observes the anonymous type itself.</param>
    private record struct EscapeScan(string Name, bool Escapes);
}
