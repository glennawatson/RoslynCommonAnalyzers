// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a source file that carries usings or comments but declares no namespace, type, or top-level
/// statement (SST1533). A file with assembly-level attributes, or one that is genuinely empty, is left
/// alone; generated files are excluded from analysis. A file whose content sits inside an inactive
/// <c>#if</c> region is left alone too — a polyfill declares its type on the frameworks that need it and
/// looks empty only on the ones that do not. There is no code fix — what the file should contain, or
/// whether it should be deleted, is a judgement the analyzer cannot make.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1533FileWithoutCodeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(LayoutRules.FileWithoutCode);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Reports a file that has content but declares nothing.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        if (context.Tree.GetRoot(context.CancellationToken) is not CompilationUnitSyntax root
            || root.Members.Count != 0
            || root.AttributeLists.Count != 0
            || DeclaresSomethingInAnotherConfiguration(root))
        {
            return;
        }

        if (root.Usings.Count != 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.FileWithoutCode, root.Usings[0].GetLocation()));
            return;
        }

        if (root.Externs.Count != 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.FileWithoutCode, root.Externs[0].GetLocation()));
            return;
        }

        foreach (var trivia in root.EndOfFileToken.LeadingTrivia)
        {
            if (!IsComment(trivia.Kind()))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.FileWithoutCode, trivia.GetLocation()));
            return;
        }
    }

    /// <summary>Returns whether the file has source that this compilation happens to have compiled out.</summary>
    /// <param name="root">The compilation unit.</param>
    /// <returns><see langword="true"/> when an inactive <c>#if</c> region holds the file's content.</returns>
    /// <remarks>
    /// A polyfill guarded by <c>#if</c> looks empty on the target frameworks that already have the API, and
    /// declares its type on the ones that do not. The file is not the empty shell it appears to be here, and
    /// reporting it would ask for a deletion that breaks every other framework the project builds.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool DeclaresSomethingInAnotherConfiguration(CompilationUnitSyntax root) =>
        root.ContainsDirectives && ContainsDisabledText(root);

    /// <summary>Returns whether a trivia kind is one of the comment forms.</summary>
    /// <param name="kind">The trivia kind.</param>
    /// <returns><see langword="true"/> for a line, block, or documentation comment.</returns>
    private static bool IsComment(SyntaxKind kind) => kind is SyntaxKind.SingleLineCommentTrivia
        or SyntaxKind.MultiLineCommentTrivia
        or SyntaxKind.SingleLineDocumentationCommentTrivia
        or SyntaxKind.MultiLineDocumentationCommentTrivia;

    /// <summary>Scans descendant token trivia, including structured trivia, for inactive source.</summary>
    /// <param name="root">The node whose tokens are scanned.</param>
    /// <returns>Whether any descendant trivia contains inactive source.</returns>
    private static bool ContainsDisabledText(SyntaxNode root)
    {
        var found = false;
        _ = DescendantTraversalHelper.VisitDescendantTokens(root, ref found, static (in SyntaxToken token, ref bool result) =>
        {
            result = ContainsDisabledText(token.LeadingTrivia) || ContainsDisabledText(token.TrailingTrivia);
            return !result;
        });
        return found;
    }

    /// <summary>Checks a trivia list and any nested structured trivia for inactive source.</summary>
    /// <param name="triviaList">The trivia to scan.</param>
    /// <returns>Whether the list contains inactive source.</returns>
    private static bool ContainsDisabledText(in SyntaxTriviaList triviaList)
    {
        for (var i = 0; i < triviaList.Count; i++)
        {
            var trivia = triviaList[i];
            if (trivia.IsKind(SyntaxKind.DisabledTextTrivia)
                || (trivia.GetStructure() is { } structure && ContainsDisabledText(structure)))
            {
                return true;
            }
        }

        return false;
    }
}
