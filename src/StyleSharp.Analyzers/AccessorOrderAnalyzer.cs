// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports accessors declared out of order: a property or indexer whose get accessor follows
/// its set/init accessor (SST1212), and an event whose add accessor follows its remove
/// accessor (SST1213).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AccessorOrderAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        OrderingRules.PropertyAccessorOrder,
        OrderingRules.EventAccessorOrder);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AccessorList);
    }

    /// <summary>Reports an accessor list whose primary accessor follows its secondary accessor.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var list = (AccessorListSyntax)context.Node;
        var isEvent = context.Node.Parent is EventDeclarationSyntax;

        var primaryIndex = IndexOf(list, isPrimary: true, isEvent);
        var secondaryIndex = IndexOf(list, isPrimary: false, isEvent);
        if (primaryIndex < 0 || secondaryIndex < 0 || primaryIndex < secondaryIndex)
        {
            return;
        }

        var rule = isEvent ? OrderingRules.EventAccessorOrder : OrderingRules.PropertyAccessorOrder;
        context.ReportDiagnostic(Diagnostic.Create(rule, list.Accessors[primaryIndex].Keyword.GetLocation()));
    }

    /// <summary>Returns the index of the first primary (get/add) or secondary (set/init/remove) accessor.</summary>
    /// <param name="list">The accessor list.</param>
    /// <param name="isPrimary">When <see langword="true"/>, finds the get/add accessor; otherwise the set/init/remove accessor.</param>
    /// <param name="isEvent">Whether the accessor list belongs to an event.</param>
    /// <returns>The accessor index, or -1 when not found.</returns>
    private static int IndexOf(AccessorListSyntax list, bool isPrimary, bool isEvent)
    {
        for (var index = 0; index < list.Accessors.Count; index++)
        {
            if (Matches(list.Accessors[index].Keyword.Kind(), isPrimary, isEvent))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Returns whether an accessor keyword is the requested primary or secondary accessor.</summary>
    /// <param name="kind">The accessor keyword kind.</param>
    /// <param name="isPrimary">Whether the get/add accessor is requested.</param>
    /// <param name="isEvent">Whether the accessor list belongs to an event.</param>
    /// <returns><see langword="true"/> when the keyword matches the request.</returns>
    private static bool Matches(SyntaxKind kind, bool isPrimary, bool isEvent)
    {
        if (isEvent)
        {
            return kind == (isPrimary ? SyntaxKind.AddKeyword : SyntaxKind.RemoveKeyword);
        }

        if (isPrimary)
        {
            return kind == SyntaxKind.GetKeyword;
        }

        return kind is SyntaxKind.SetKeyword or SyntaxKind.InitKeyword;
    }
}
