// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds the constructors an exception type is missing (SST1488), each forwarding to the matching
/// <c>base</c> constructor.
/// </summary>
/// <remarks>
/// The generated constructors carry XML documentation. That is not decoration: this repository — and any
/// project that turns the documentation rules on — treats an undocumented public member as a build error,
/// so a fix that emitted bare constructors would trade one diagnostic for another. Accessibility follows
/// the type: <c>protected</c> for an abstract exception, which only its derived types construct, and
/// <c>public</c> otherwise.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1488ExceptionStandardConstructorsCodeFixProvider))]
[Shared]
public sealed class Sst1488ExceptionStandardConstructorsCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.ExceptionStandardConstructors.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryGetTarget(root, diagnostic, out var declaration, out var missing))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add the standard exception constructors",
                    _ => Task.FromResult(Apply(context.Document, root, declaration!, missing)),
                    equivalenceKey: nameof(Sst1488ExceptionStandardConstructorsCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetTarget(editor.OriginalRoot, diagnostic, out var declaration, out var missing))
        {
            return;
        }

        editor.ReplaceNode(declaration!, (current, _) => current is ClassDeclarationSyntax target ? AddConstructors(target, missing) : current);
    }

    /// <summary>Applies the fix for one exception type.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="declaration">The exception type declaration.</param>
    /// <param name="missing">The constructors to add.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ClassDeclarationSyntax declaration, int missing)
        => document.WithSyntaxRoot(root.ReplaceNode(declaration, AddConstructors(declaration, missing)));

    /// <summary>Resolves the diagnostic to its type declaration and the set of constructors to add.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="declaration">The reported type declaration when found.</param>
    /// <param name="missing">The flag set naming the missing constructors.</param>
    /// <returns><see langword="true"/> when the fix can run.</returns>
    private static bool TryGetTarget(SyntaxNode root, Diagnostic diagnostic, out ClassDeclarationSyntax? declaration, out int missing)
    {
        missing = 0;
        declaration = root.FindNode(diagnostic.Location.SourceSpan) as ClassDeclarationSyntax;
        if (declaration is null
            || !diagnostic.Properties.TryGetValue(ExceptionConstructorAnalyzer.MissingConstructorsKey, out var value)
            || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out missing))
        {
            return false;
        }

        return missing != 0;
    }

    /// <summary>Adds the missing constructors to the declaration, ahead of its other members.</summary>
    /// <param name="declaration">The exception type declaration.</param>
    /// <param name="missing">The flag set naming the missing constructors.</param>
    /// <returns>The declaration with the constructors added.</returns>
    /// <remarks>
    /// The constructors are inserted after any the type already declares, so a partially-complete type
    /// keeps its constructors together rather than gaining a second group further down.
    /// </remarks>
    private static ClassDeclarationSyntax AddConstructors(ClassDeclarationSyntax declaration, int missing)
    {
        var isAbstract = ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.AbstractKeyword);
        var accessibility = isAbstract ? SyntaxKind.ProtectedKeyword : SyntaxKind.PublicKeyword;
        var name = declaration.Identifier.ValueText;
        var newLine = DetectLineEnding(declaration);

        var additions = new List<MemberDeclarationSyntax>(3);
        if ((missing & (int)StandardExceptionConstructors.Parameterless) != 0)
        {
            additions.Add(BuildConstructor(name, accessibility, newLine, withMessage: false, withInner: false));
        }

        if ((missing & (int)StandardExceptionConstructors.Message) != 0)
        {
            additions.Add(BuildConstructor(name, accessibility, newLine, withMessage: true, withInner: false));
        }

        if ((missing & (int)StandardExceptionConstructors.MessageAndInner) != 0)
        {
            additions.Add(BuildConstructor(name, accessibility, newLine, withMessage: true, withInner: true));
        }

        var members = declaration.Members;
        var index = LastConstructorIndex(members) + 1;
        SeparateMembers(additions, index, members.Count);
        return declaration
            .WithMembers(members.InsertRange(index, additions))
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Puts a blank line between the generated constructors and their neighbours.</summary>
    /// <param name="additions">The constructors being added.</param>
    /// <param name="index">The index they are inserted at.</param>
    /// <param name="existing">The number of members the type already declares.</param>
    /// <remarks>
    /// The formatter puts members on their own lines but does not separate them with a blank one, which
    /// the layout rules require. A member that opens the type body needs no blank line above it, so the
    /// leading line is added only where something already precedes it.
    /// </remarks>
    private static void SeparateMembers(List<MemberDeclarationSyntax> additions, int index, int existing)
    {
        for (var i = 0; i < additions.Count; i++)
        {
            if (index + i == 0)
            {
                continue;
            }

            additions[i] = additions[i].WithLeadingTrivia(
                additions[i].GetLeadingTrivia().Insert(0, SyntaxFactory.ElasticCarriageReturnLineFeed));
        }

        if (index >= existing)
        {
            return;
        }

        var last = additions.Count - 1;
        additions[last] = additions[last].WithTrailingTrivia(
            additions[last].GetTrailingTrivia().Add(SyntaxFactory.ElasticCarriageReturnLineFeed));
    }

    /// <summary>Finds the index of the last constructor the type already declares.</summary>
    /// <param name="members">The type's members.</param>
    /// <returns>The index, or -1 when the type declares no constructor.</returns>
    private static int LastConstructorIndex(SyntaxList<MemberDeclarationSyntax> members)
    {
        var index = -1;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is ConstructorDeclarationSyntax)
            {
                index = i;
            }
        }

        return index;
    }

    /// <summary>Builds one constructor, forwarding its parameters to the base constructor.</summary>
    /// <param name="name">The exception type's name.</param>
    /// <param name="accessibility">The accessibility keyword to declare.</param>
    /// <param name="newLine">The line ending the document uses.</param>
    /// <param name="withMessage">Whether the constructor takes the message.</param>
    /// <param name="withInner">Whether the constructor takes the inner exception.</param>
    /// <returns>The constructor declaration, documented.</returns>
    /// <remarks>
    /// The member is parsed from text rather than assembled from factory calls: the layout — the base
    /// initializer on its own line, a blank line between members — is what this repository's own layout
    /// rules require, and writing it out states it exactly instead of hoping the formatter infers it.
    /// <c>System.Exception</c> is written in full and annotated for simplification, so it binds whether or
    /// not the file has a <c>using System;</c>, and shortens to <c>Exception</c> when it does.
    /// </remarks>
    private static ConstructorDeclarationSyntax BuildConstructor(string name, SyntaxKind accessibility, string newLine, bool withMessage, bool withInner)
    {
        var keyword = SyntaxFactory.Token(accessibility).ValueText;
        var builder = new System.Text.StringBuilder();
        builder.Append("/// <summary>Initializes a new instance of the <see cref=\"")
            .Append(name)
            .Append("\"/> class.</summary>")
            .Append(newLine);

        if (withMessage)
        {
            builder.Append("/// <param name=\"message\">The message that describes the error.</param>").Append(newLine);
        }

        if (withInner)
        {
            builder.Append("/// <param name=\"innerException\">The exception that is the cause of this exception.</param>").Append(newLine);
        }

        builder.Append(keyword).Append(' ').Append(name).Append('(');
        if (withMessage)
        {
            builder.Append("string message");
        }

        if (withInner)
        {
            builder.Append(", System.Exception innerException");
        }

        builder.Append(')').Append(newLine);

        if (withMessage)
        {
            builder.Append("    : base(message");
            if (withInner)
            {
                builder.Append(", innerException");
            }

            builder.Append(')').Append(newLine);
        }

        builder.Append('{').Append(newLine).Append('}').Append(newLine);

        var constructor = (ConstructorDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(builder.ToString())!;
        return constructor
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Simplification.Simplifier.Annotation)
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Reads the line ending the document already uses.</summary>
    /// <param name="declaration">The type declaration being fixed.</param>
    /// <returns>The document's line ending, defaulting to CRLF for a document that has none.</returns>
    /// <remarks>
    /// The formatter normalizes the line endings it inserts itself but leaves verbatim ones alone, and a
    /// documentation comment parsed from text carries its newlines verbatim. Writing the machine's
    /// <c>Environment.NewLine</c> would therefore stamp the build agent's convention into the user's file.
    /// Copying what the file already uses keeps the generated documentation consistent with it.
    /// </remarks>
    private static string DetectLineEnding(SyntaxNode declaration)
    {
        foreach (var trivia in declaration.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return trivia.ToFullString();
            }
        }

        return "\r\n";
    }
}
