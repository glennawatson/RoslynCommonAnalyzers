// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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

        // The buffer is built once and cleared per element. Only the hash of each key is kept, so a
        // comment whose elements all differ - which is nearly all of them - never materialises a key
        // string at all. Two elements hashing alike are then compared in full, because a hash match
        // alone would report a duplicate that is not one.
        StringBuilder? builder = null;
        List<ElementKey>? seen = null;

        foreach (var node in documentation.Content)
        {
            if (node is not XmlElementSyntax element)
            {
                continue;
            }

            builder ??= new StringBuilder();
            builder.Clear();
            XmlDocumentationHelper.AppendDuplicateComparisonKey(element, builder);
            if (builder.Length == 0)
            {
                continue;
            }

            var hash = KeyHash(builder);
            if (seen is null)
            {
                seen = [new ElementKey(hash, element)];
                continue;
            }

            if (!IsDuplicate(seen, hash, builder))
            {
                seen.Add(new ElementKey(hash, element));
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.NoDuplicateDocumentation, element.GetLocation()));
        }
    }

    /// <summary>Returns whether an element's key repeats one already seen.</summary>
    /// <param name="seen">The elements seen so far, with their key hashes.</param>
    /// <param name="hash">The candidate element's key hash.</param>
    /// <param name="builder">The buffer holding the candidate's key.</param>
    /// <returns><see langword="true"/> when an earlier element has the same key text.</returns>
    /// <remarks>
    /// Reached only when two hashes match, so the strings this compares are built for that pair rather
    /// than for every element in every comment. The buffer is left holding the earlier element's key,
    /// which the caller has finished with by this point.
    /// </remarks>
    private static bool IsDuplicate(List<ElementKey> seen, int hash, StringBuilder builder)
    {
        string? candidate = null;
        for (var index = 0; index < seen.Count; index++)
        {
            if (seen[index].Hash != hash)
            {
                continue;
            }

            candidate ??= builder.ToString();
            builder.Clear();
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
