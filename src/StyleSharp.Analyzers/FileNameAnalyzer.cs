// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires a file's name to match the first type it declares (SST1649). Generic
/// arity is ignored, and any <c>.suffix</c> before the extension is allowed (so
/// <c>Widget.Logic.cs</c> matches a partial <c>Widget</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileNameAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DocumentationRules.FileNameMatchesType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Analyzes the file name against the first declared type.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var stem = FileStem(context.Tree.FilePath);
        if (stem.Length == 0)
        {
            return;
        }

        if (!TryGetFirstTypeIdentifier(context.Tree.GetRoot(context.CancellationToken), out var identifier))
        {
            return;
        }

        if (string.Equals(identifier.ValueText, stem, StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.FileNameMatchesType, identifier.GetLocation(), identifier.ValueText));
    }

    /// <summary>Returns the file name without its path, the extension, or any <c>.suffix</c>.</summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The file name stem, or an empty string.</returns>
    private static string FileStem(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return string.Empty;
        }

        var slash = filePath!.LastIndexOfAny(['/', '\\']);
        var name = slash >= 0 ? filePath[(slash + 1)..] : filePath;

        // Cut at the extension/suffix ('.'), or a generic-arity marker ('{T}' or backtick).
        var cut = name.IndexOfAny(['.', '{', '`']);
        return cut >= 0 ? name[..cut] : name;
    }

    /// <summary>Finds the identifier of the first non-partial type declaration in the file.</summary>
    /// <param name="root">The compilation unit.</param>
    /// <param name="identifier">The first type's identifier when found.</param>
    /// <returns><see langword="true"/> when a non-partial type declaration exists.</returns>
    private static bool TryGetFirstTypeIdentifier(SyntaxNode root, out SyntaxToken identifier)
    {
        var state = (Found: false, Identifier: default(SyntaxToken));
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, (bool Found, SyntaxToken Identifier)>(root, ref state, VisitTypeLikeDeclaration);
        identifier = state.Identifier;
        return state.Found;
    }

    /// <summary>Records the first type-like declaration encountered in preorder.</summary>
    /// <param name="node">The visited syntax node.</param>
    /// <param name="state">The current search state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> to stop.</returns>
    private static bool VisitTypeLikeDeclaration(SyntaxNode node, ref (bool Found, SyntaxToken Identifier) state)
    {
        switch (node)
        {
            case BaseTypeDeclarationSyntax type:
                {
                    // A partial type may legitimately live in any number of files.
                    if (ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword))
                    {
                        return false;
                    }

                    state = (true, type.Identifier);
                    return false;
                }

            case DelegateDeclarationSyntax @delegate:
                {
                    state = (true, @delegate.Identifier);
                    return false;
                }

            default:
                return true;
        }
    }
}
