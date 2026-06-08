// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a member or statement whose indentation differs from its siblings (SST1137). Within a
/// block, type body, namespace body, or enum body, every element that begins its own line shares the
/// first sibling's indentation. Elements that share a line with another (governed by SST1107/SST1136)
/// are skipped.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1137ElementIndentationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The container kinds whose direct children are compared.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.Block,
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.EnumDeclaration,
        SyntaxKind.NamespaceDeclaration);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.ElementsConsistentIndentation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Dispatches to the sibling list for the container kind.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);

        // Each list is walked with its own struct enumerator — no boxing to IEnumerable and no
        // intermediate collection. A single shared 'reference' indentation threads through the case.
        var reference = -1;
        switch (context.Node)
        {
            case BlockSyntax block:
            {
                foreach (var element in block.Statements)
                {
                    ProcessElement(context, text, element, ref reference);
                }

                break;
            }

            case TypeDeclarationSyntax type:
            {
                foreach (var element in type.Members)
                {
                    ProcessElement(context, text, element, ref reference);
                }

                break;
            }

            case EnumDeclarationSyntax @enum:
            {
                foreach (var element in @enum.Members)
                {
                    ProcessElement(context, text, element, ref reference);
                }

                break;
            }

            case NamespaceDeclarationSyntax @namespace:
            {
                foreach (var element in @namespace.Members)
                {
                    ProcessElement(context, text, element, ref reference);
                }

                break;
            }
        }
    }

    /// <summary>Records the first sibling's indentation and flags any later sibling that differs from it.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="element">The sibling element.</param>
    /// <param name="reference">The running reference indentation (-1 until the first own-line sibling is seen).</param>
    private static void ProcessElement(SyntaxNodeAnalysisContext context, SourceText text, SyntaxNode element, ref int reference)
    {
        var indent = OwnLineIndent(text, element);
        if (indent < 0)
        {
            return;
        }

        if (reference < 0)
        {
            reference = indent;
        }
        else if (indent != reference)
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.ElementsConsistentIndentation, element.GetFirstToken().GetLocation()));
        }
    }

    /// <summary>Returns the element's indentation width when it begins its own line, otherwise -1.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="element">The element node.</param>
    /// <returns>The number of whitespace characters before the element, or -1 when it shares a line.</returns>
    private static int OwnLineIndent(SourceText text, SyntaxNode element)
    {
        var start = element.GetFirstToken().SpanStart;
        var line = text.Lines.GetLineFromPosition(start);
        for (var position = line.Start; position < start; position++)
        {
            if (!char.IsWhiteSpace(text[position]))
            {
                return -1;
            }
        }

        return start - line.Start;
    }
}
