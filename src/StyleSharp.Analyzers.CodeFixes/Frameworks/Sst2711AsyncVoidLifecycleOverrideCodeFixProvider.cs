// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Simplification;

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a synchronous <c>ComponentBase</c> lifecycle override declared <c>async void</c> into the
/// Task-returning <c>...Async</c> twin the runtime actually awaits (SST2711): the return type becomes
/// <c>Task</c> and the name gains the <c>Async</c> suffix, leaving the body unchanged.
/// </summary>
/// <remarks>
/// The fix is offered only when it is safe: the reported member is an ordinary method (not an explicit interface
/// implementation) and the enclosing type does not already declare a member with the twin name, so the rename
/// can never collide with an existing method or property. When either guard fails no fix is offered, because the
/// twin the rule points at is a design decision the fix cannot make for the author.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2711AsyncVoidLifecycleOverrideCodeFixProvider))]
[Shared]
public sealed class Sst2711AsyncVoidLifecycleOverrideCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The suffix that names each synchronous hook's Task-returning twin.</summary>
    private const string AsyncSuffix = "Async";

    /// <summary>The fully-qualified task type emitted for the new return type, reduced by the simplifier.</summary>
    private const string TaskTypeName = "global::System.Threading.Tasks.Task";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(FrameworksRules.AsyncVoidLifecycleOverride.Id);

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
            if (Resolve(root, diagnostic) is not { } method)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Override the Task-returning lifecycle method",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(method, Rewrite(method)))),
                    equivalenceKey: nameof(Sst2711AsyncVoidLifecycleOverrideCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not { } method)
        {
            return;
        }

        editor.ReplaceNode(method, (current, _) => Rewrite((MethodDeclarationSyntax)current));
    }

    /// <summary>Resolves the reported method, or <see langword="null"/> when the rewrite would not be safe.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The method to rewrite, or <see langword="null"/> when no fix is offered.</returns>
    private static MethodDeclarationSyntax? Resolve(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { ExplicitInterfaceSpecifier: null } method
            || method.Parent is not TypeDeclarationSyntax type
            || DeclaresMember(type, method.Identifier.ValueText + AsyncSuffix))
        {
            return null;
        }

        return method;
    }

    /// <summary>Returns whether a type already declares a method or property with a given name.</summary>
    /// <param name="type">The enclosing type declaration.</param>
    /// <param name="name">The twin name the rename would introduce.</param>
    /// <returns><see langword="true"/> when a member with that name is already present.</returns>
    private static bool DeclaresMember(TypeDeclarationSyntax type, string name)
    {
        var members = type.Members;
        for (var i = 0; i < members.Count; i++)
        {
            var memberName = members[i] switch
            {
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                PropertyDeclarationSyntax property => property.Identifier.ValueText,
                _ => null,
            };

            if (memberName is not null && string.Equals(memberName, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Rewrites the method to return <c>Task</c> and carry the <c>...Async</c> name.</summary>
    /// <param name="method">The <c>async void</c> lifecycle override.</param>
    /// <returns>The rewritten method declaration.</returns>
    private static MethodDeclarationSyntax Rewrite(MethodDeclarationSyntax method)
    {
        var taskType = SyntaxFactory.ParseTypeName(TaskTypeName)
            .WithTriviaFrom(method.ReturnType)
            .WithAdditionalAnnotations(Simplifier.Annotation);

        var identifier = SyntaxFactory.Identifier(method.Identifier.ValueText + AsyncSuffix)
            .WithTriviaFrom(method.Identifier);

        return method
            .WithReturnType(taskType)
            .WithIdentifier(identifier);
    }
}
