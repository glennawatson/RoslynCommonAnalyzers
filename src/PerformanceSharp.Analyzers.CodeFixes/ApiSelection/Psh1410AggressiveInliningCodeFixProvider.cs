// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Adds <c>[MethodImpl(MethodImplOptions.AggressiveInlining)]</c> to a reported forwarder
/// (PSH1410). The attribute goes on its own line above the member and takes over the member's
/// leading trivia — its doc comment and any surrounding directives move, rather than being
/// copied, so a member that already carries an attribute keeps exactly one of each. The
/// <c>System.Runtime.CompilerServices</c> import is added to the file when it is missing, sorted
/// into the existing block.
/// </summary>
/// <remarks>
/// A file whose imports carry a directive is left alone. A multi-targeted project compiles one linked
/// file once per framework and reconciles the results into a single document; where they differ it
/// writes conflict markers into the source. A <c>using</c> inside an <c>#if</c> is a node in one
/// compilation and disabled text in another, so whether the import is already there would not read the
/// same in both. Everywhere else the answer comes from the file text alone, which every compilation
/// shares.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1410AggressiveInliningCodeFixProvider))]
[Shared]
public sealed class Psh1410AggressiveInliningCodeFixProvider : CodeFixProvider
{
    /// <summary>The namespace the attribute and its options enum live in.</summary>
    private const string CompilerServicesNamespace = "System.Runtime.CompilerServices";

    /// <summary>The namespace whose imports sort ahead of every other.</summary>
    private const string SystemNamespace = "System";

    /// <summary>The attribute as it is written above the member.</summary>
    private const string AttributeText = "[MethodImpl(MethodImplOptions.AggressiveInlining)]";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.InlineTrivialForwarders.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => DocumentFixAll.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax unit || !CanWriteAttribute(unit))
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (Resolve(unit, diagnostic) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add AggressiveInlining",
                    _ => Task.FromResult(Apply(context.Document, unit, ImmutableArrays.Of(diagnostic))),
                    nameof(Psh1410AggressiveInliningCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Rewrites every reported member in one document and imports the attribute's namespace.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="unit">The document's compilation unit.</param>
    /// <param name="diagnostics">The diagnostics to fix.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, CompilationUnitSyntax unit, ImmutableArray<Diagnostic> diagnostics)
    {
        var declarations = new List<BaseMethodDeclarationSyntax>(diagnostics.Length);
        for (var i = 0; i < diagnostics.Length; i++)
        {
            if (Resolve(unit, diagnostics[i]) is { } declaration && !declarations.Contains(declaration))
            {
                declarations.Add(declaration);
            }
        }

        if (declarations.Count == 0)
        {
            return document;
        }

        var lineBreak = LineEndingHelper.GetLineBreak(unit);
        var rewritten = (CompilationUnitSyntax)unit.ReplaceNodes(declarations, (original, _) => WithAttribute(original, lineBreak));
        return document.WithSyntaxRoot(WithCompilerServicesImport(rewritten, lineBreak));
    }

    /// <summary>Resolves the member a diagnostic reports, when it still has the reported shape.</summary>
    /// <param name="unit">The document's compilation unit.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The member, or <see langword="null"/> when the shape no longer matches.</returns>
    private static BaseMethodDeclarationSyntax? Resolve(CompilationUnitSyntax unit, Diagnostic diagnostic)
        => unit.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>() is { } declaration
            && Psh1410AggressiveInliningAnalyzer.IsEligibleForwarder(declaration)
                ? declaration
                : null;

    /// <summary>Builds the member with the attribute on the line above it.</summary>
    /// <param name="declaration">The member as it was written.</param>
    /// <param name="lineBreak">The file's line-break trivia.</param>
    /// <returns>The member carrying the attribute.</returns>
    /// <remarks>
    /// The lists are taken from the already-stripped member, not the original. A member that already
    /// carries an attribute holds its doc comment and any directives on that first list, so inserting
    /// ahead of the original lists would leave a second copy of both above the one this fix writes.
    /// </remarks>
    private static BaseMethodDeclarationSyntax WithAttribute(BaseMethodDeclarationSyntax declaration, SyntaxTrivia lineBreak)
    {
        var leading = declaration.GetLeadingTrivia();
        var attributeList = ((MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration($"{AttributeText} void P();")!).AttributeLists[0]
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(lineBreak, GetIndentation(leading));

        var stripped = declaration.WithLeadingTrivia(default(SyntaxTriviaList));
        return stripped.WithAttributeLists(stripped.AttributeLists.Insert(0, attributeList));
    }

    /// <summary>Returns the indentation whitespace at the end of a member's leading trivia.</summary>
    /// <param name="leading">The member's leading trivia.</param>
    /// <returns>The indentation trivia, or elastic space when none.</returns>
    private static SyntaxTrivia GetIndentation(in SyntaxTriviaList leading)
        => leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? leading[leading.Count - 1]
            : SyntaxFactory.Whitespace(string.Empty);

    /// <summary>Adds the attribute's namespace to the file's imports when it is not there.</summary>
    /// <param name="unit">The compilation unit to import into.</param>
    /// <param name="lineBreak">The file's line-break trivia.</param>
    /// <returns>The compilation unit importing the namespace.</returns>
    private static CompilationUnitSyntax WithCompilerServicesImport(CompilationUnitSyntax unit, SyntaxTrivia lineBreak)
    {
        if (ImportsCompilerServices(unit))
        {
            return unit;
        }

        var directive = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(CompilerServicesNamespace))
            .WithTrailingTrivia(lineBreak);

        return unit.Usings.Count == 0
            ? InsertFirstImport(unit, directive, lineBreak)
            : InsertAmongImports(unit, directive);
    }

    /// <summary>Returns whether the file already imports the attribute's namespace.</summary>
    /// <param name="unit">The compilation unit to inspect.</param>
    /// <returns><see langword="true"/> when a plain import of the namespace is present.</returns>
    private static bool ImportsCompilerServices(CompilationUnitSyntax unit)
    {
        var usings = unit.Usings;
        for (var i = 0; i < usings.Count; i++)
        {
            if (IsPlainImport(usings[i]) && usings[i].Name?.ToString() == CompilerServicesNamespace)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the short attribute can be written in this file.</summary>
    /// <param name="unit">The compilation unit to inspect.</param>
    /// <returns><see langword="true"/> when the attribute will resolve once the fix has run.</returns>
    /// <remarks>
    /// The short spelling needs the import, so a file the import cannot be written into gets no edit at
    /// all rather than an attribute that does not bind. What decides that is the imports alone: a
    /// <c>using</c> inside an <c>#if</c> is a node in one compilation and inactive text in another, so
    /// whether the namespace is imported would not read the same in both. A conditional anywhere else in
    /// the file says nothing about the imports, and the position the directive is written to is fixed by
    /// the file's text, so every compilation of a linked file writes the same thing.
    /// </remarks>
    private static bool CanWriteAttribute(CompilationUnitSyntax unit)
        => !ImportsCarryDirectives(unit);

    /// <summary>Returns whether a directive stands among the file's imports.</summary>
    /// <param name="unit">The compilation unit to inspect.</param>
    /// <returns><see langword="true"/> when any import carries a directive.</returns>
    /// <remarks>
    /// Writing into the block moves the file header onto whichever directive comes first, so a region or a
    /// pragma among the imports would end up introducing a different one than it was written above.
    /// </remarks>
    private static bool ImportsCarryDirectives(CompilationUnitSyntax unit)
    {
        var usings = unit.Usings;
        for (var i = 0; i < usings.Count; i++)
        {
            if (usings[i].ContainsDirectives)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a using directive imports a namespace outright.</summary>
    /// <param name="directive">The directive to inspect.</param>
    /// <returns><see langword="true"/> for a directive that is neither an alias nor <c>static</c>.</returns>
    private static bool IsPlainImport(UsingDirectiveSyntax directive)
        => directive.Alias is null && directive.StaticKeyword.IsKind(SyntaxKind.None);

    /// <summary>Writes the file's first import, moving the file header onto it.</summary>
    /// <param name="unit">The compilation unit to import into.</param>
    /// <param name="directive">The directive to write.</param>
    /// <param name="lineBreak">The file's line-break trivia.</param>
    /// <returns>The compilation unit with one import.</returns>
    /// <remarks>
    /// With no imports of its own the file's header comment leads the first member, so the header has
    /// to move onto the directive; a blank line takes its place above the member.
    /// </remarks>
    private static CompilationUnitSyntax InsertFirstImport(CompilationUnitSyntax unit, UsingDirectiveSyntax directive, SyntaxTrivia lineBreak)
    {
        if (unit.Members.Count == 0)
        {
            return unit.WithUsings(SyntaxFactory.SingletonList(directive));
        }

        var first = unit.Members[0];
        return unit
            .WithMembers(unit.Members.Replace(first, first.WithLeadingTrivia(lineBreak)))
            .WithUsings(SyntaxFactory.SingletonList(directive.WithLeadingTrivia(first.GetLeadingTrivia())));
    }

    /// <summary>Writes the directive into the existing import block, in order.</summary>
    /// <param name="unit">The compilation unit to import into.</param>
    /// <param name="directive">The directive to write.</param>
    /// <returns>The compilation unit with the directive in place.</returns>
    private static CompilationUnitSyntax InsertAmongImports(CompilationUnitSyntax unit, UsingDirectiveSyntax directive)
    {
        var usings = unit.Usings;
        var index = 0;
        while (index < usings.Count && SortsBefore(usings[index], CompilerServicesNamespace))
        {
            index++;
        }

        if (index > 0)
        {
            return unit.WithUsings(usings.Insert(index, directive));
        }

        // The file header leads whichever directive comes first, so it travels with the position.
        var displaced = usings[0];
        return unit.WithUsings(usings
            .Replace(displaced, displaced.WithLeadingTrivia(default(SyntaxTriviaList)))
            .Insert(0, directive.WithLeadingTrivia(displaced.GetLeadingTrivia())));
    }

    /// <summary>Returns whether an existing import sorts ahead of a namespace being added.</summary>
    /// <param name="existing">The directive already in the block.</param>
    /// <param name="name">The namespace being added.</param>
    /// <returns><see langword="true"/> when the existing directive keeps its place.</returns>
    /// <remarks><c>System</c> and its children lead the block, and the rest follow in name order.</remarks>
    private static bool SortsBefore(UsingDirectiveSyntax existing, string name)
    {
        var existingName = existing.Name?.ToString() ?? string.Empty;
        var existingIsSystem = IsSystemNamespace(existingName);
        return existingIsSystem == IsSystemNamespace(name)
            ? string.CompareOrdinal(existingName, name) < 0
            : existingIsSystem;
    }

    /// <summary>Returns whether a namespace belongs to the <c>System</c> group.</summary>
    /// <param name="name">The namespace name.</param>
    /// <returns><see langword="true"/> for <c>System</c> and anything beneath it.</returns>
    private static bool IsSystemNamespace(string name)
        => name == SystemNamespace || name.StartsWith(SystemNamespace + ".", StringComparison.Ordinal);

    /// <summary>Applies every reported member in a document in one pass, so the import is written once.</summary>
    private sealed class DocumentFixAll : DocumentBasedFixAllProvider
    {
        /// <summary>The shared provider instance.</summary>
        public static readonly DocumentFixAll Instance = new();

        /// <inheritdoc/>
        protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
        {
            var root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
            return root is not CompilationUnitSyntax unit || !CanWriteAttribute(unit)
                ? document
                : Apply(document, unit, diagnostics);
        }
    }
}
