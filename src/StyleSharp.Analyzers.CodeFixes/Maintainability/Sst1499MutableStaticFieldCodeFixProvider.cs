// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.FindSymbols;

namespace StyleSharp.Analyzers;

/// <summary>
/// Makes a reported static field <c>readonly</c> (SST1499) when nothing in the solution ever reassigns it
/// outside its own initializer or its type's static constructor.
/// </summary>
/// <remarks>
/// <para>
/// The fix is offered for exactly one shape: the field is not <c>readonly</c> yet, and every write to it in
/// the whole solution already happens where a <c>readonly</c> field is allowed to be written. Then adding
/// the keyword changes nothing about how the code runs and everything about what a caller may do to it.
/// </para>
/// <para>
/// It is deliberately not offered for the other half of the rule — a <c>readonly</c> array or list, whose
/// reference is already fixed and whose contents are not. Turning that into a copy, an immutable collection
/// or a method is a decision about what the API should promise, and each choice reads and performs
/// differently. The diagnostic asks the question; it does not pretend to know the answer.
/// </para>
/// <para>
/// A write reached through a lambda or a local function is treated as disqualifying even inside the static
/// constructor: the delegate can outlive the constructor, and the compiler would reject the assignment.
/// A <c>volatile</c> field is skipped because <c>volatile readonly</c> is not legal C#.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1499MutableStaticFieldCodeFixProvider))]
[Shared]
public sealed class Sst1499MutableStaticFieldCodeFixProvider : CodeFixProvider, IAsyncBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.MutableStaticField.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => AsyncBatchEditFixAllProvider.Instance;

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
            var field = await FindReadonlyCandidateAsync(context.Document, root, diagnostic, context.CancellationToken).ConfigureAwait(false);
            if (field is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Make the field readonly",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(field, WithReadonly(field)))),
                    equivalenceKey: nameof(Sst1499MutableStaticFieldCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    async Task IAsyncBatchableCodeFix.RegisterEditsAsync(DocumentEditor editor, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var field = await FindReadonlyCandidateAsync(editor.OriginalDocument, editor.OriginalRoot, diagnostic, cancellationToken).ConfigureAwait(false);
        if (field is null)
        {
            return;
        }

        // One declaration can carry several reported variables, and so several diagnostics — all of them
        // asking for the same keyword. The replacement is computed from the node as the batch has it so far,
        // which both keeps the editor's tracking intact and makes the second request a no-op.
        editor.ReplaceNode(field, static (current, _) => AddReadonly(current));
    }

    /// <summary>Adds <c>readonly</c> to the declaration unless an earlier edit in the batch already did.</summary>
    /// <param name="node">The current field declaration, including any edits already batched.</param>
    /// <returns>The declaration with the keyword.</returns>
    private static SyntaxNode AddReadonly(SyntaxNode node)
        => node is FieldDeclarationSyntax field && !ModifierListHelper.Contains(field.Modifiers, SyntaxKind.ReadOnlyKeyword)
            ? WithReadonly(field)
            : node;

    /// <summary>Adds <c>readonly</c> after the modifiers the field already declares.</summary>
    /// <param name="field">The field declaration.</param>
    /// <returns>The updated declaration.</returns>
    private static FieldDeclarationSyntax WithReadonly(FieldDeclarationSyntax field)
        => field.WithModifiers(field.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

    /// <summary>Resolves a diagnostic to a field that can take the <c>readonly</c> keyword as it stands.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The field declaration, or <see langword="null"/> when the keyword cannot simply be added.</returns>
    private static async Task<FieldDeclarationSyntax?> FindReadonlyCandidateAsync(
        Document document,
        SyntaxNode root,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<FieldDeclarationSyntax>() is not { } field
            || !CanTakeTheKeyword(field.Modifiers))
        {
            return null;
        }

        if (await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false) is not { } model)
        {
            return null;
        }

        return await AreAllVariablesFixableAsync(document, model, field, cancellationToken).ConfigureAwait(false) ? field : null;
    }

    /// <summary>Returns whether the declaration's modifiers leave room for <c>readonly</c>.</summary>
    /// <param name="modifiers">The field's modifiers.</param>
    /// <returns><see langword="true"/> when the keyword is neither already there nor forbidden beside one it has.</returns>
    /// <remarks>
    /// A <c>readonly</c> field that is reported is the collection half of the rule, which this fix does not
    /// answer. <c>volatile readonly</c> is not legal C#, and a <c>const</c> is not a field to begin with.
    /// </remarks>
    private static bool CanTakeTheKeyword(SyntaxTokenList modifiers)
        => !ModifierListHelper.Contains(modifiers, SyntaxKind.ReadOnlyKeyword)
            && !ModifierListHelper.Contains(modifiers, SyntaxKind.ConstKeyword)
            && !ModifierListHelper.Contains(modifiers, SyntaxKind.VolatileKeyword);

    /// <summary>Returns whether every variable of the declaration would survive the keyword.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="field">The field declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when no variable is reassigned and none can be changed in place.</returns>
    /// <remarks>One declaration can declare several variables, and the keyword lands on all of them at once.</remarks>
    private static async Task<bool> AreAllVariablesFixableAsync(
        Document document,
        SemanticModel model,
        FieldDeclarationSyntax field,
        CancellationToken cancellationToken)
    {
        var variables = field.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            if (model.GetDeclaredSymbol(variables[i], cancellationToken) is not IFieldSymbol symbol
                || !CanHoldReadonly(symbol.Type)
                || await IsReassignedAsync(document.Project.Solution, symbol, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether <c>readonly</c> can be added to a field of this type without changing what it does.</summary>
    /// <param name="type">The field's type.</param>
    /// <returns><see langword="true"/> when the keyword only takes the reassignment away.</returns>
    /// <remarks>
    /// A mutable struct is the trap. <c>Origin.X = 1</c> writes the field itself, and a call to one of its
    /// non-readonly methods writes through <c>this</c>; add <c>readonly</c> and the first stops compiling
    /// while the second silently starts mutating a defensive copy. So only types that cannot be changed in
    /// place take the fix: a reference type (whose <em>object</em> stays mutable, which the keyword never
    /// claimed otherwise), a primitive, an enum, or a struct the compiler itself has marked readonly.
    /// </remarks>
    private static bool CanHoldReadonly(ITypeSymbol type)
        => type.IsReferenceType
            || type.SpecialType != SpecialType.None
            || type.TypeKind == TypeKind.Enum
            || type is INamedTypeSymbol { IsReadOnly: true };

    /// <summary>Returns whether anything in the solution writes to the field where a readonly one may not be written.</summary>
    /// <param name="solution">The solution to search.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a write would not survive the keyword.</returns>
    private static async Task<bool> IsReassignedAsync(Solution solution, IFieldSymbol field, CancellationToken cancellationToken)
    {
        var references = await SymbolFinder.FindReferencesAsync(field, solution, cancellationToken).ConfigureAwait(false);
        foreach (var reference in references)
        {
            foreach (var location in reference.Locations)
            {
                if (await IsDisqualifyingWriteAsync(location, field, cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether one reference writes to the field somewhere a readonly one could not be written.</summary>
    /// <param name="location">The reference location.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the reference disqualifies the field.</returns>
    private static async Task<bool> IsDisqualifyingWriteAsync(ReferenceLocation location, IFieldSymbol field, CancellationToken cancellationToken)
    {
        if (location.IsImplicit)
        {
            return false;
        }

        if (await location.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) is not { } root)
        {
            return true;
        }

        if (root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax reference)
        {
            return true;
        }

        var access = GetOutermostAccess(reference);
        if (!IsWritePosition(access))
        {
            return false;
        }

        return !await IsInStaticConstructorOfAsync(location.Document, access, field, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Walks a reference out to the expression that a write would actually target.</summary>
    /// <param name="reference">The referencing name.</param>
    /// <returns>The outermost expression naming the field.</returns>
    private static ExpressionSyntax GetOutermostAccess(ExpressionSyntax reference)
    {
        var access = reference;
        while (access.Parent is MemberAccessExpressionSyntax member && member.Name == access)
        {
            access = member;
        }

        return access;
    }

    /// <summary>Returns whether an expression sits where the value it names is written rather than read.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> when the expression is assigned, incremented, or passed by reference.</returns>
    private static bool IsWritePosition(ExpressionSyntax expression)
        => expression.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == expression,
            PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression),
            _ => expression.Parent is PostfixUnaryExpressionSyntax or ArgumentSyntax { RefOrOutKeyword.RawKind: not 0 },
        };

    /// <summary>Returns whether a write sits directly in the static constructor of the field's own type.</summary>
    /// <param name="document">The document holding the write.</param>
    /// <param name="access">The written expression.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the write is one a readonly field still allows.</returns>
    private static async Task<bool> IsInStaticConstructorOfAsync(
        Document document,
        ExpressionSyntax access,
        IFieldSymbol field,
        CancellationToken cancellationToken)
    {
        // A delegate or a local function can run after the constructor has returned, so the compiler rejects
        // the write even when the text of it sits inside the constructor's braces.
        if (access.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is not null
            || access.FirstAncestorOrSelf<LocalFunctionStatementSyntax>() is not null
            || access.FirstAncestorOrSelf<ConstructorDeclarationSyntax>() is not { } constructor
            || !ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword))
        {
            return false;
        }

        if (await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false) is not { } model)
        {
            return false;
        }

        return model.GetDeclaredSymbol(constructor, cancellationToken) is { } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, field.ContainingType);
    }
}
