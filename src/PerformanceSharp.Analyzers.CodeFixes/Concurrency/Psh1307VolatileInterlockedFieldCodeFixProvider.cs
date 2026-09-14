// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Wraps a reported plain field access in the matching Volatile call (PSH1307): a read
/// becomes <c>Volatile.Read(ref field)</c> and a simple assignment becomes
/// <c>Volatile.Write(ref field, value)</c>. Compound assignments and increments are reported
/// without a fix — they need an <c>Interlocked</c> read-modify-write, which changes more than
/// visibility. The Volatile type is spelled simple when the System.Threading import makes it
/// resolve, and fully qualified otherwise.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1307VolatileInterlockedFieldCodeFixProvider))]
[Shared]
public sealed class Psh1307VolatileInterlockedFieldCodeFixProvider : CodeFixProvider
{
    /// <summary>The simple name of the volatile type.</summary>
    private const string VolatileTypeName = "Volatile";

    /// <summary>The read method name.</summary>
    private const string ReadMethodName = "Read";

    /// <summary>The write method name.</summary>
    private const string WriteMethodName = "Write";

    /// <summary>The namespace the simple spelling requires.</summary>
    private const string ThreadingNamespace = "System.Threading";

    /// <summary>The fully qualified spelling used when the simple name does not resolve.</summary>
    private const string QualifiedVolatileExpression = "global::System.Threading.Volatile";

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.VolatileInterlockedField.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            static (root, _, diagnostic) => TryCreateTitle(root, diagnostic),
            static _ => nameof(Psh1307VolatileInterlockedFieldCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported access and builds its Volatile wrapper.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The replacement, or <see langword="null"/> for unfixable access shapes.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (TryGetUsage(root, diagnostic) is not { } usage)
        {
            return null;
        }

        var volatileSpelling = ResolvesVolatile(model, usage.SpanStart) ? VolatileTypeName : QualifiedVolatileExpression;
        if (usage.Parent is AssignmentExpressionSyntax assignment && assignment.Left == usage)
        {
            var write = BuildVolatileCall(
                volatileSpelling,
                WriteMethodName,
                usage,
                SyntaxFactory.Argument(
                    nameColon: null,
                    refKindKeyword: default,
                    assignment.Right.WithLeadingTrivia(SyntaxFactory.Space).WithoutTrailingTrivia()));
            return new NodeReplacement(assignment, write.WithTriviaFrom(assignment));
        }

        var read = BuildVolatileCall(volatileSpelling, ReadMethodName, usage, extraArgument: null);
        return new NodeReplacement(usage, read.WithTriviaFrom(usage));
    }

    /// <summary>Builds a <c>Volatile.X(ref field[, value])</c> invocation.</summary>
    /// <param name="volatileSpelling">The volatile type spelling.</param>
    /// <param name="methodName">Read or Write.</param>
    /// <param name="field">The field access to take by ref.</param>
    /// <param name="extraArgument">The value argument for writes, or <see langword="null"/>.</param>
    /// <returns>The invocation.</returns>
    private static InvocationExpressionSyntax BuildVolatileCall(
        string volatileSpelling,
        string methodName,
        ExpressionSyntax field,
        ArgumentSyntax? extraArgument)
    {
        var refArgument = SyntaxFactory.Argument(
            nameColon: null,
            SyntaxFactory.Token(default, SyntaxKind.RefKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            field.WithoutTrivia());
        var arguments = extraArgument is null
            ? SyntaxFactory.SingletonSeparatedList(refArgument)
            : SyntaxFactory.SeparatedList(ImmutableArrays.Of(refArgument, extraArgument));

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression(volatileSpelling),
                SyntaxFactory.IdentifierName(methodName)),
            SyntaxFactory.ArgumentList(arguments));
    }

    /// <summary>Returns whether the volatile type resolves by simple name at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <returns><see langword="true"/> when the simple spelling binds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ResolvesVolatile(SemanticModel model, int position) =>
        TypeNameLookup.ResolvesIn(model, position, VolatileTypeName, ThreadingNamespace);

    /// <summary>Finds an access that can be wrapped without changing a read-modify-write operation.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The reported access.</param>
    /// <returns>The fixable access, or null for compound writes and unsupported shapes.</returns>
    private static ExpressionSyntax? TryGetUsage(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not ExpressionSyntax usage
            || usage is not (IdentifierNameSyntax or MemberAccessExpressionSyntax))
        {
            return null;
        }

        if (usage.Parent is AssignmentExpressionSyntax assignment && assignment.Left == usage)
        {
            return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ? usage : null;
        }

        return Psh1307VolatileInterlockedFieldAnalyzer.IsWriteAccess(usage) ? null : usage;
    }

    /// <summary>Words the action for the reported field usage: a write for an assignment target, otherwise a read.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when the usage no longer resolves.</returns>
    private static string? TryCreateTitle(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetUsage(root, diagnostic) switch
        {
            null => null,
            { Parent: AssignmentExpressionSyntax assignment } usage when assignment.Left == usage => "Use Volatile.Write",
            _ => "Use Volatile.Read",
        };
}
