// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Replaces the parameterless <c>new Guid()</c> with the value it actually produces, <c>Guid.Empty</c> (SST2012).</summary>
/// <remarks>
/// The replacement is spelled the way the construction was: <c>new Guid()</c> becomes <c>Guid.Empty</c>,
/// <c>new System.Guid()</c> becomes <c>System.Guid.Empty</c>. A target-typed <c>new()</c> has no spelling to
/// borrow, so <c>Guid.Empty</c> is tried first and the fully qualified name is the fallback for a file with no
/// <c>using System</c>. Registration checks that the globally qualified field is available; candidate
/// spellings are bound when applying. If the global name is ambiguous or unavailable, registration also
/// checks the written spelling speculatively so an alias can still make the fix available.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2012UseGuidEmptyCodeFixProvider))]
[Shared]
public sealed class Sst2012UseGuidEmptyCodeFixProvider : CodeFixProvider
{
    /// <summary>The name of the field that holds the all-zero GUID.</summary>
    private const string EmptyFieldName = "Empty";

    /// <summary>The unqualified type name, used when the construction had no spelling to borrow.</summary>
    private const string GuidTypeName = "Guid";

    /// <summary>The fully qualified fallback, used when the simple name does not bind.</summary>
    private const string QualifiedEmpty = "global::System.Guid.Empty";

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseGuidEmpty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use 'Guid.Empty'",
            nameof(Sst2012UseGuidEmptyCodeFixProvider),
            CanRewrite,
            TryRewrite);

    /// <summary>Resolves the reported construction and builds the first replacement that binds.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The construction and its <c>Guid.Empty</c> replacement, or <see langword="null"/> when none binds.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not BaseObjectCreationExpressionSyntax creation)
        {
            return null;
        }

        var position = creation.SpanStart;
        var written = BuildEmptyAccess(GetTypeName(creation));
        if (BindsToGuidEmpty(model, position, written))
        {
            return new NodeReplacement(creation, written.WithTriviaFrom(creation));
        }

        var qualified = SyntaxFactory.ParseExpression(QualifiedEmpty);
        return BindsToGuidEmpty(model, position, qualified)
            ? new NodeReplacement(creation, qualified.WithTriviaFrom(creation))
            : null;
    }

    /// <summary>Checks the global field first and binds candidate spellings only when it is unusable.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether a replacement binds.</returns>
    private static bool CanRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is BaseObjectCreationExpressionSyntax creation
            && (CanUseQualifiedEmpty(model, creation.SpanStart) || TryRewrite(root, model, diagnostic) is not null);

    /// <summary>Checks the global field directly so ordinary registration needs no candidate syntax.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The position where the field would be used.</param>
    /// <returns>Whether the fully qualified fallback is unambiguous and accessible.</returns>
    /// <remarks>Unusual namespace or reference conflicts fall back to speculative binding of the written name.</remarks>
    private static bool CanUseQualifiedEmpty(SemanticModel model, int position) =>
        model.Compilation.GetTypeByMetadataName(Sst2012UseGuidEmptyAnalyzer.GuidMetadataName) is { } guid
            && model.LookupNamespacesAndTypes(position, model.Compilation.GlobalNamespace, nameof(System)) is [INamespaceSymbol system]
            && model.LookupNamespacesAndTypes(position, system, GuidTypeName) is [INamedTypeSymbol globalGuid]
            && SymbolEqualityComparer.Default.Equals(globalGuid, guid)
            && guid.GetMembers(EmptyFieldName) is [IFieldSymbol { IsStatic: true } field]
            && model.IsAccessible(position, guid)
            && model.IsAccessible(position, field);

    /// <summary>Gets the type name the construction was written with, or the bare name for a target-typed one.</summary>
    /// <param name="creation">The reported construction.</param>
    /// <returns>The type name the replacement should borrow.</returns>
    private static string GetTypeName(BaseObjectCreationExpressionSyntax creation) =>
        creation is ObjectCreationExpressionSyntax { Type: { } type }
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionSyntax BuildEmptyAccess(string type) =>
        SyntaxFactory.ParseExpression($"{type}.{EmptyFieldName}");

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
