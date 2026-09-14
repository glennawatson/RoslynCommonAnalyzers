// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a documentation element whose text is copied verbatim from another element in the
/// same documentation comment (SST1625) — for example a parameter whose description repeats
/// the summary. The comparison key includes inline reference targets (a <c>cref</c>, a
/// <c>paramref</c> name, …) so two elements that read the same but point at different references
/// — e.g. parameter descriptions differing only in their <c>&lt;see cref="…"/&gt;</c> — are not
/// mistaken for copies.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1625DuplicateDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The seed for the key hash.</summary>
    private const int HashSeed = 17;

    /// <summary>The multiplier for the key hash.</summary>
    private const int HashFactor = 31;

    /// <summary>The minimum number of elements that can duplicate one another.</summary>
    private const int MinimumComparableElements = 2;

    /// <summary>The documentation-comment node kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia);

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DocumentationRules.NoDuplicateDocumentation);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Reports any documentation element whose text repeats an earlier element's text.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var documentation = (DocumentationCommentTriviaSyntax)context.Node;
        var elementCount = 0;
        foreach (var node in documentation.Content)
        {
            if (node is XmlElementSyntax)
            {
                elementCount++;
            }
        }

        if (elementCount < MinimumComparableElements)
        {
            return;
        }

        // Each callback owns its rental; returning it clears syntax references to the compilation.
        var seen = ArrayPool<ElementKey>.Shared.Rent(elementCount);
        try
        {
            StringBuilder? builder = null;
            var seenCount = 0;
            foreach (var node in documentation.Content)
            {
                if (node is not XmlElementSyntax element)
                {
                    continue;
                }

                builder ??= new StringBuilder(element.Span.Length);
                _ = builder.Clear();
                XmlDocumentationHelper.AppendDuplicateComparisonKey(element, builder);
                if (builder.Length == 0)
                {
                    continue;
                }

                var hash = KeyHash(builder);
                if (!IsDuplicate(seen, seenCount, hash, builder))
                {
                    seen[seenCount] = new(hash, element);
                    seenCount++;
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.NoDuplicateDocumentation, element.GetLocation()));
            }
        }
        finally
        {
            ArrayPool<ElementKey>.Shared.Return(seen, clearArray: true);
        }
    }

    /// <summary>Returns whether an element's key repeats one already seen.</summary>
    /// <param name="seen">The pooled buffer holding previous elements and their key hashes.</param>
    /// <param name="seenCount">The number of populated entries in the buffer.</param>
    /// <param name="hash">The candidate element's key hash.</param>
    /// <param name="builder">The buffer holding the candidate's key.</param>
    /// <returns>Whether an earlier element has the same key text.</returns>
    private static bool IsDuplicate(ElementKey[] seen, int seenCount, int hash, StringBuilder builder)
    {
        string? candidate = null;
        for (var index = 0; index < seenCount; index++)
        {
            if (seen[index].Hash != hash)
            {
                continue;
            }

            candidate ??= builder.ToString();
            _ = builder.Clear();
            XmlDocumentationHelper.AppendDuplicateComparisonKey(seen[index].Element, builder);
            if (string.Equals(candidate, builder.ToString(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Computes an ordinal hash of the key currently in the buffer.</summary>
    /// <param name="builder">The buffer holding the key.</param>
    /// <returns>The key's hash.</returns>
    private static int KeyHash(StringBuilder builder)
    {
        var hash = HashSeed;
        for (var index = 0; index < builder.Length; index++)
        {
            hash = (hash * HashFactor) + builder[index];
        }

        return hash;
    }

    /// <summary>One documentation element and the hash of its comparison key.</summary>
    /// <param name="Hash">The key's hash.</param>
    /// <param name="Element">The element the key came from.</param>
    private readonly record struct ElementKey(int Hash, XmlElementSyntax Element);
}
