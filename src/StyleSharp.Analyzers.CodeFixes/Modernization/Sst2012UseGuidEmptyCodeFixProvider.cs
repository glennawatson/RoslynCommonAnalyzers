// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces the parameterless <c>new Guid()</c> with the value it actually produces, <c>Guid.Empty</c> (SST2012).</summary>
/// <remarks>
/// The replacement is spelled the way the construction was: <c>new Guid()</c> becomes <c>Guid.Empty</c>,
/// <c>new System.Guid()</c> becomes <c>System.Guid.Empty</c>. A target-typed <c>new()</c> has no spelling to
/// borrow, so <c>Guid.Empty</c> is tried first and the fully qualified name is the fallback for a file with no
/// <c>using System</c>. Each candidate is bound speculatively at the site it would occupy, and the fix is only
/// offered once one of them resolves to <c>System.Guid.Empty</c>.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2012UseGuidEmptyCodeFixProvider))]
[Shared]
public sealed class Sst2012UseGuidEmptyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The name of the field that holds the all-zero GUID.</summary>
    private const string EmptyFieldName = "Empty";

    /// <summary>The unqualified type name, used when the construction had no spelling to borrow.</summary>
    private const string GuidTypeName = "Guid";

    /// <summary>The fully qualified fallback, used when the simple name does not bind.</summary>
    private const string QualifiedEmpty = "global::System.Guid.Empty";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseGuidEmpty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryBuildReplacement(root, model, diagnostic, out var creation, out var replacement))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use 'Guid.Empty'",
                    _ => Task.FromResult(Apply(context.Document, root, creation!, replacement!)),
                    equivalenceKey: nameof(Sst2012UseGuidEmptyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryBuildReplacement(editor.OriginalRoot, editor.SemanticModel, diagnostic, out var creation, out var replacement))
        {
            return;
        }

        editor.ReplaceNode(creation!, replacement!);
    }

    /// <summary>Replaces one reported construction with the named empty GUID.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="creation">The reported construction.</param>
    /// <param name="replacement">The <c>Guid.Empty</c> expression built for it.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(
        Document document,
        SyntaxNode root,
        BaseObjectCreationExpressionSyntax creation,
        ExpressionSyntax replacement)
        => document.WithSyntaxRoot(root.ReplaceNode(creation, replacement));

    /// <summary>Resolves the reported construction and builds the first replacement that binds.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="creation">The reported construction, when the shape still matches.</param>
    /// <param name="replacement">The <c>Guid.Empty</c> expression, when one binds.</param>
    /// <returns><see langword="true"/> when the fix can be offered.</returns>
    internal static bool TryBuildReplacement(
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic,
        out BaseObjectCreationExpressionSyntax? creation,
        out ExpressionSyntax? replacement)
    {
        replacement = null;
        creation = root.FindNode(diagnostic.Location.SourceSpan) as BaseObjectCreationExpressionSyntax;
        if (creation is null)
        {
            return false;
        }

        var position = creation.SpanStart;
        var written = BuildEmptyAccess(GetTypeName(creation));
        if (BindsToGuidEmpty(model, position, written))
        {
            replacement = written.WithTriviaFrom(creation);
            return true;
        }

        var qualified = SyntaxFactory.ParseExpression(QualifiedEmpty);
        if (!BindsToGuidEmpty(model, position, qualified))
        {
            return false;
        }

        replacement = qualified.WithTriviaFrom(creation);
        return true;
    }

    /// <summary>Gets the type name the construction was written with, or the bare name for a target-typed one.</summary>
    /// <param name="creation">The reported construction.</param>
    /// <returns>The type name the replacement should borrow.</returns>
    private static string GetTypeName(BaseObjectCreationExpressionSyntax creation)
        => creation is ObjectCreationExpressionSyntax { Type: { } type }
            ? type.WithoutTrivia().ToString()
            : GuidTypeName;

    /// <summary>Builds the <c>&lt;type&gt;.Empty</c> access for a candidate type name.</summary>
    /// <param name="type">The type name to qualify with.</param>
    /// <returns>The member access expression.</returns>
    /// <remarks>
    /// Parsed rather than composed: a qualified <em>type</em> name is a <c>QualifiedName</c>, while the same
    /// text read as an expression is a chain of member accesses. Building the second from the first would
    /// produce a tree that no longer matches its own text.
    /// </remarks>
    private static ExpressionSyntax BuildEmptyAccess(string type)
        => SyntaxFactory.ParseExpression(type + "." + EmptyFieldName);

    /// <summary>Returns whether a candidate expression binds to <c>System.Guid.Empty</c> where it would sit.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The position the candidate would occupy.</param>
    /// <param name="candidate">The candidate expression.</param>
    /// <returns><see langword="true"/> when the replacement compiles and means the empty GUID.</returns>
    private static bool BindsToGuidEmpty(SemanticModel model, int position, ExpressionSyntax candidate)
    {
        var speculative = model.GetSpeculativeSymbolInfo(position, candidate, SpeculativeBindingOption.BindAsExpression);
        if (speculative.Symbol is not IFieldSymbol { IsStatic: true, Name: EmptyFieldName } field)
        {
            return false;
        }

        var guid = model.Compilation.GetTypeByMetadataName(Sst2012UseGuidEmptyAnalyzer.GuidMetadataName);
        return SymbolEqualityComparer.Default.Equals(field.ContainingType, guid);
    }
}
