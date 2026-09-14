// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests deleting a comment together with the layout it owns.</summary>
public class CommentRemovalHelperTests
{
    /// <summary>A comment that owns its line, followed by one that trails code.</summary>
    private const string Source = "int a;\n    // owns the line\nint b; // trails\n";

    /// <summary>The comment that owns its line.</summary>
    private const string OwnLineComment = "// owns the line";

    /// <summary>The comment that trails code.</summary>
    private const string TrailingComment = "// trails";

    /// <summary>Verifies the removal takes the whole line for an own-line comment and the separating space for a trailing one.</summary>
    /// <param name="comment">The comment to remove.</param>
    /// <param name="expected">The text after removal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(OwnLineComment, "int a;\nint b; // trails\n")]
    [Arguments(TrailingComment, "int a;\n    // owns the line\nint b;\n")]
    public async Task RemovalChangeTakesTheOwnedLayoutAsync(string comment, string expected)
    {
        var text = SourceText.From(Source);

        var change = CommentRemovalHelper.RemovalChange(text, SpanOf(comment));

        await Assert.That(change.NewText).IsEqualTo(string.Empty);
        await Assert.That(text.WithChanges(change).ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies the removal is written into a document.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveAsyncWritesTheRemovalAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(CommentRemovalHelper), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(Source));

        var changed = await CommentRemovalHelper.RemoveAsync(document, SpanOf(TrailingComment), CancellationToken.None);

        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo("int a;\n    // owns the line\nint b;\n");
    }

    /// <summary>Gets the span of a comment in <see cref="Source"/>.</summary>
    /// <param name="comment">The comment text.</param>
    /// <returns>The comment's span.</returns>
    private static TextSpan SpanOf(string comment)
    {
        var start = Source.IndexOf(comment, StringComparison.Ordinal);
        return new(start, comment.Length);
    }
}
