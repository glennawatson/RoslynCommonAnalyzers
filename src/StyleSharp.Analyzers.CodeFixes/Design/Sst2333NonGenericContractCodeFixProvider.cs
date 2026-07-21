// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds the non-generic comparison or equality member a type is missing (SST2333), forwarding to the generic
/// one it already implements after a type check — so the runtime paths that still use the non-generic contract
/// reach the type's real logic instead of falling back to reference identity.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2333NonGenericContractCodeFixProvider))]
[Shared]
public sealed class Sst2333NonGenericContractCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DesignRules.MissingNonGenericContract.Id);

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
            if (Resolve(root, diagnostic) is not var (declaration, updated))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add the non-generic member",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, updated))),
                    equivalenceKey: nameof(Sst2333NonGenericContractCodeFixProvider) + diagnostic.Properties[Sst2333NonGenericContractAnalyzer.ContractKey]),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not var (declaration, updated))
        {
            return;
        }

        editor.ReplaceNode(declaration, updated);
    }

    /// <summary>Resolves the diagnostic to the type declaration and its rewrite carrying the non-generic member.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The declaration and its rewrite, or <see langword="null"/> when the shape no longer matches.</returns>
    private static (TypeDeclarationSyntax Declaration, TypeDeclarationSyntax Updated)? Resolve(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration
            || !diagnostic.Properties.TryGetValue(Sst2333NonGenericContractAnalyzer.ContractKey, out var contract)
            || contract is null
            || !diagnostic.Properties.TryGetValue(Sst2333NonGenericContractAnalyzer.TypeArgumentKey, out var argument)
            || argument is null)
        {
            return null;
        }

        var updated = AddContract(declaration, contract, argument);
        return updated is null ? null : (declaration, updated);
    }

    /// <summary>Adds the non-generic base type (when any) and member(s) for one contract.</summary>
    /// <param name="declaration">The type declaration.</param>
    /// <param name="contract">The contract to add.</param>
    /// <param name="argument">The fully-qualified generic type argument.</param>
    /// <returns>The rewritten declaration, or <see langword="null"/> when the contract is unknown.</returns>
    private static TypeDeclarationSyntax? AddContract(TypeDeclarationSyntax declaration, string contract, string argument)
    {
        var (baseTypeName, memberTexts) = Template(contract, argument);
        if (memberTexts is null)
        {
            return null;
        }

        var members = ParseMembers(memberTexts);
        if (members.Length == 0)
        {
            return null;
        }

        var updated = declaration;
        if (baseTypeName is not null)
        {
            var baseType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(baseTypeName).WithAdditionalAnnotations(Simplifier.Annotation));
            updated = BaseListInsertion.AddBaseType(updated, baseType);
        }

        return (TypeDeclarationSyntax)updated.AddMembers(members)
            .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation);
    }

    /// <summary>Returns the base type and member texts for a contract, or an empty template for an unknown one.</summary>
    /// <param name="contract">The contract to add.</param>
    /// <param name="argument">The fully-qualified generic type argument.</param>
    /// <returns>The base type name (or <see langword="null"/>) and the member declaration texts.</returns>
    private static (string? BaseType, string[]? Members) Template(string contract, string argument) => contract switch
    {
        Sst2333NonGenericContractAnalyzer.ComparableContract => (
            "global::System.IComparable",
            [
                $"int global::System.IComparable.CompareTo(object obj) => obj is {argument} other "
                + $"? ((global::System.IComparable<{argument}>)this).CompareTo(other) "
                + ": throw new global::System.InvalidCastException();",
            ]),
        Sst2333NonGenericContractAnalyzer.ComparerContract => (
            "global::System.Collections.IComparer",
            [
                "int global::System.Collections.IComparer.Compare(object x, object y) => "
                + $"((global::System.Collections.Generic.IComparer<{argument}>)this).Compare(({argument})x, ({argument})y);",
            ]),
        Sst2333NonGenericContractAnalyzer.EqualityComparerContract => (
            "global::System.Collections.IEqualityComparer",
            [
                "bool global::System.Collections.IEqualityComparer.Equals(object x, object y) => "
                + $"((global::System.Collections.Generic.IEqualityComparer<{argument}>)this).Equals(({argument})x, ({argument})y);",
                "int global::System.Collections.IEqualityComparer.GetHashCode(object obj) => "
                + $"((global::System.Collections.Generic.IEqualityComparer<{argument}>)this).GetHashCode(({argument})obj);",
            ]),
        Sst2333NonGenericContractAnalyzer.EquatableContract => (
            null,
            [
                $"public override bool Equals(object obj) => obj is {argument} other "
                + $"&& ((global::System.IEquatable<{argument}>)this).Equals(other);",
            ]),
        _ => (null, null),
    };

    /// <summary>Parses the member declaration texts, dropping any that fail to parse.</summary>
    /// <param name="memberTexts">The member declaration texts.</param>
    /// <returns>The parsed member declarations, formatter-annotated.</returns>
    private static MemberDeclarationSyntax[] ParseMembers(string[] memberTexts)
    {
        var parsed = new List<MemberDeclarationSyntax>(memberTexts.Length);
        for (var i = 0; i < memberTexts.Length; i++)
        {
            if (SyntaxFactory.ParseMemberDeclaration(memberTexts[i]) is { } member)
            {
                // A leading blank line separates the added member from the one before it (the last existing
                // member, or a previous added member), which the formatter would otherwise glue onto one line.
                parsed.Add(member
                    .WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)
                    .WithAdditionalAnnotations(Formatter.Annotation));
            }
        }

        return [.. parsed];
    }
}
