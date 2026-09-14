// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>Adds the constructors an exception type is missing (SST1488), each forwarding to the matching <c>base</c> constructor.</summary>
/// <remarks>
/// The generated constructors carry XML documentation. That is not decoration: this repository — and any
/// project that turns the documentation rules on — treats an undocumented public member as a build error,
/// so a fix that emitted bare constructors would trade one diagnostic for another. Accessibility follows
/// the type: <c>protected</c> for an abstract exception, which only its derived types construct, and
/// <c>public</c> otherwise.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1488ExceptionStandardConstructorsCodeFixProvider))]
[Shared]
public sealed class Sst1488ExceptionStandardConstructorsCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.ExceptionStandardConstructors.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            static (root, diagnostic) => TryGetTarget(root, diagnostic, out _, out _) ? "Add the standard exception constructors" : null,
            static _ => nameof(Sst1488ExceptionStandardConstructorsCodeFixProvider),
            TryRewrite);

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetTarget(editor.OriginalRoot, diagnostic, out var declaration, out var missing))
        {
            return;
        }

        editor.ReplaceNode(declaration!, (current, _) => current is ClassDeclarationSyntax target ? AddConstructors(target, missing) : current);
    }

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
        return declaration is not null
            && DiagnosticPropertyReader.TryGetInt32(diagnostic, ExceptionConstructorAnalyzer.MissingConstructorsKey, out missing)
            && missing != 0;
    }

    /// <summary>Resolves the reported exception type and builds it with the missing standard constructors.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the type no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetTarget(root, diagnostic, out var declaration, out var missing)
            ? new NodeReplacement(declaration!, AddConstructors(declaration!, missing))
            : null;

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

        // The formatter normalizes the line endings it inserts itself but leaves verbatim ones alone, and
        // a documentation comment parsed from text carries its newlines verbatim, so the generated
        // documentation has to be written with the line ending the file already uses.
        var newLine = LineEndingHelper.GetLineBreak(declaration).ToFullString();

        const int StandardExceptionConstructorCount = 3;

        var additions = new List<MemberDeclarationSyntax>(StandardExceptionConstructorCount);
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
        const string SummaryPrefix = "/// <summary>Initializes a new instance of the <see cref=\"";
        const string SummarySuffix = "\"/> class.</summary>";
        const string MessageDocumentation = "/// <param name=\"message\">The message that describes the error.</param>";
        const string InnerDocumentation = "/// <param name=\"innerException\">The exception that is the cause of this exception.</param>";
        const int BaseLineCount = 4;
        const int MessageLineCount = 2;
        var keyword = SyntaxFactory.Token(accessibility).ValueText;
        var capacity = SummaryPrefix.Length + name.Length + SummarySuffix.Length
            + keyword.Length + name.Length + " (){}".Length + (newLine.Length * BaseLineCount);
        if (withMessage)
        {
            capacity += MessageDocumentation.Length + "string message".Length + "    : base(message)".Length + (newLine.Length * MessageLineCount);
        }

        if (withInner)
        {
            capacity += InnerDocumentation.Length + ", System.Exception innerException".Length
                + newLine.Length + (withMessage ? ", innerException".Length : 0);
        }

        var builder = new System.Text.StringBuilder(capacity);
        _ = builder.Append(SummaryPrefix)
            .Append(name)
            .Append(SummarySuffix)
            .Append(newLine);

        if (withMessage)
        {
            _ = builder.Append(MessageDocumentation).Append(newLine);
        }

        if (withInner)
        {
            _ = builder.Append(InnerDocumentation).Append(newLine);
        }

        _ = builder.Append(keyword).Append(' ').Append(name).Append('(');
        if (withMessage)
        {
            _ = builder.Append("string message");
        }

        if (withInner)
        {
            _ = builder.Append(", System.Exception innerException");
        }

        _ = builder.Append(')').Append(newLine);

        if (withMessage)
        {
            _ = builder.Append("    : base(message");
            if (withInner)
            {
                _ = builder.Append(", innerException");
            }

            _ = builder.Append(')').Append(newLine);
        }

        _ = builder.Append('{').Append(newLine).Append('}').Append(newLine);

        var constructor = (ConstructorDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(builder.ToString())!;
        return constructor.WithAdditionalAnnotations(
            Microsoft.CodeAnalysis.Simplification.Simplifier.Annotation,
            Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }
}
