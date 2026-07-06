// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Offers up to three rewrites for a struct without equality members (PSH1005), ordered by
/// preference: convert the declaration to a record struct (readonly when its instance state
/// is already immutable) on C# 10+, implement <c>IEquatable&lt;T&gt;</c> and make the struct
/// readonly when it is eligible, or implement <c>IEquatable&lt;T&gt;</c> alone. The generated
/// members compare every instance field and auto-property through
/// <c>EqualityComparer&lt;T&gt;.Default</c> and hash them with <c>HashCode.Combine</c>, so
/// the equatable actions are offered only when <c>HashCode</c> exists and the member count
/// fits one Combine call. Structs with pointer or fixed-buffer state, or with existing
/// equality-shaped members, register no fixes.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1005ValueTypeEqualityCodeFixProvider))]
[Shared]
public sealed class Psh1005ValueTypeEqualityCodeFixProvider : CodeFixProvider
{
    /// <summary>The metadata name of the hash combiner the generated GetHashCode uses.</summary>
    private const string HashCodeMetadataName = "System.HashCode";

    /// <summary>The most members one HashCode.Combine call accepts.</summary>
    private const int MaxCombineMembers = 8;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.ValueTypeEqualityBoxes.Id);

    /// <inheritdoc/>
    public override FixAllProvider? GetFixAllProvider() => null;

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
            if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<StructDeclarationSyntax>() is not { } declaration
                || HasEqualityShapedMember(declaration)
                || TryGetDataMembers(declaration) is not { } members)
            {
                continue;
            }

            RegisterActions(context, root, model, declaration, members, diagnostic);
        }
    }

    /// <summary>Registers the applicable code actions for one reported struct.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="declaration">The reported struct declaration.</param>
    /// <param name="members">The struct's instance data members.</param>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    private static void RegisterActions(
        CodeFixContext context,
        SyntaxNode root,
        SemanticModel model,
        StructDeclarationSyntax declaration,
        ImmutableArray<(string Type, string Name)> members,
        Diagnostic diagnostic)
    {
        var immutable = Psh1014ReadonlyStructAnalyzer.HasImmutableInstanceState(declaration);
        if (declaration.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp10 })
        {
            var recordTitle = immutable ? "Convert to a readonly record struct" : "Convert to a record struct";
            context.RegisterCodeFix(
                CodeAction.Create(
                    recordTitle,
                    cancellationToken => Task.FromResult(context.Document.WithSyntaxRoot(
                        root.ReplaceNode(declaration, ConvertToRecordStruct(declaration, immutable)))),
                    equivalenceKey: nameof(Psh1005ValueTypeEqualityCodeFixProvider) + ".Record"),
                diagnostic);
        }

        if (model.Compilation.GetTypeByMetadataName(HashCodeMetadataName) is null || members.Length > MaxCombineMembers)
        {
            return;
        }

        if (immutable && !declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
            && declaration.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp7_2 })
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Implement IEquatable and make the struct readonly",
                    cancellationToken => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(
                        declaration,
                        Psh1014ReadonlyStructCodeFixProvider.AddReadonlyModifier(ImplementEquatable(model, declaration, members))))),
                    equivalenceKey: nameof(Psh1005ValueTypeEqualityCodeFixProvider) + ".EquatableReadonly"),
                diagnostic);
        }

        if (declaration.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp7 })
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Implement IEquatable",
                    cancellationToken => Task.FromResult(context.Document.WithSyntaxRoot(
                        root.ReplaceNode(declaration, ImplementEquatable(model, declaration, members)))),
                    equivalenceKey: nameof(Psh1005ValueTypeEqualityCodeFixProvider) + ".Equatable"),
                diagnostic);
        }
    }

    /// <summary>Returns whether the struct already declares an equality-shaped member the rewrites would collide with.</summary>
    /// <param name="declaration">The struct declaration.</param>
    /// <returns><see langword="true"/> when an Equals or GetHashCode method or an equality operator is declared.</returns>
    private static bool HasEqualityShapedMember(StructDeclarationSyntax declaration)
    {
        foreach (var member in declaration.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method when method.Identifier.ValueText
                    is nameof(Equals) or nameof(GetHashCode):
                case OperatorDeclarationSyntax op when op.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken)
                    || op.OperatorToken.IsKind(SyntaxKind.ExclamationEqualsToken):
                    return true;
                default:
                    continue;
            }
        }

        return false;
    }

    /// <summary>Collects the struct's instance data members, or <see langword="null"/> when generation cannot represent them.</summary>
    /// <param name="declaration">The struct declaration.</param>
    /// <returns>The type-and-name pairs, or <see langword="null"/> for pointer or fixed-buffer state.</returns>
    private static ImmutableArray<(string Type, string Name)>? TryGetDataMembers(StructDeclarationSyntax declaration)
    {
        var members = ImmutableArray.CreateBuilder<(string Type, string Name)>();
        foreach (var member in declaration.Members)
        {
            var representable = member switch
            {
                FieldDeclarationSyntax field => TryAddFieldMembers(field, members),
                PropertyDeclarationSyntax property => TryAddPropertyMember(property, members),
                _ => true,
            };

            if (!representable)
            {
                return null;
            }
        }

        return members.ToImmutable();
    }

    /// <summary>Adds a field's instance variables to the data member list.</summary>
    /// <param name="field">The field declaration.</param>
    /// <param name="members">The data member list.</param>
    /// <returns><see langword="false"/> when the field cannot be represented in generated equality.</returns>
    private static bool TryAddFieldMembers(FieldDeclarationSyntax field, ImmutableArray<(string Type, string Name)>.Builder members)
    {
        if (field.Modifiers.Any(SyntaxKind.StaticKeyword) || field.Modifiers.Any(SyntaxKind.ConstKeyword))
        {
            return true;
        }

        if (field.Modifiers.Any(SyntaxKind.FixedKeyword) || field.Declaration.Type is PointerTypeSyntax or FunctionPointerTypeSyntax)
        {
            return false;
        }

        foreach (var variable in field.Declaration.Variables)
        {
            members.Add((field.Declaration.Type.ToString(), variable.Identifier.ValueText));
        }

        return true;
    }

    /// <summary>Adds an auto-property's backing value to the data member list.</summary>
    /// <param name="property">The property declaration.</param>
    /// <param name="members">The data member list.</param>
    /// <returns><see langword="false"/> when the property cannot be represented in generated equality.</returns>
    private static bool TryAddPropertyMember(PropertyDeclarationSyntax property, ImmutableArray<(string Type, string Name)>.Builder members)
    {
        if (!IsInstanceAutoProperty(property))
        {
            return true;
        }

        if (property.Type is PointerTypeSyntax or FunctionPointerTypeSyntax)
        {
            return false;
        }

        members.Add((property.Type.ToString(), property.Identifier.ValueText));
        return true;
    }

    /// <summary>Returns whether a property is an instance auto-property with a synthesized backing field.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns><see langword="true"/> when every accessor is bodiless.</returns>
    private static bool IsInstanceAutoProperty(PropertyDeclarationSyntax property)
    {
        if (property.Modifiers.Any(SyntaxKind.StaticKeyword) || property.AccessorList is not { } accessors)
        {
            return false;
        }

        foreach (var accessor in accessors.Accessors)
        {
            if (accessor.Body is not null || accessor.ExpressionBody is not null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Converts the struct declaration to a record struct, readonly when eligible.</summary>
    /// <param name="declaration">The struct declaration.</param>
    /// <param name="immutable">Whether the instance state allows the readonly modifier.</param>
    /// <returns>The record struct declaration.</returns>
    private static RecordDeclarationSyntax ConvertToRecordStruct(StructDeclarationSyntax declaration, bool immutable)
    {
        var structKeyword = declaration.Keyword;
        var record = SyntaxFactory.RecordDeclaration(
            SyntaxKind.RecordStructDeclaration,
            declaration.AttributeLists,
            declaration.Modifiers,
            SyntaxFactory.Token(SyntaxKind.RecordKeyword)
                .WithLeadingTrivia(structKeyword.LeadingTrivia)
                .WithTrailingTrivia(SyntaxFactory.Space),
            structKeyword.WithLeadingTrivia(),
            declaration.Identifier,
            declaration.TypeParameterList,
            parameterList: null,
            declaration.BaseList,
            declaration.ConstraintClauses,
            declaration.OpenBraceToken,
            declaration.Members,
            declaration.CloseBraceToken,
            declaration.SemicolonToken);

        return immutable && !declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
            ? (RecordDeclarationSyntax)Psh1014ReadonlyStructCodeFixProvider.AddReadonlyModifier(record)
            : record;
    }

    /// <summary>Adds the equatable base and generated equality members to the struct.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="declaration">The struct declaration.</param>
    /// <param name="members">The instance data members to compare.</param>
    /// <returns>The struct implementing <c>IEquatable&lt;T&gt;</c>.</returns>
    private static StructDeclarationSyntax ImplementEquatable(
        SemanticModel model,
        StructDeclarationSyntax declaration,
        ImmutableArray<(string Type, string Name)> members)
    {
        var selfType = declaration.Identifier.ValueText + declaration.TypeParameterList;
        var position = declaration.SpanStart;
        var equatable = ResolvesInSystem(model, position, "IEquatable") ? "IEquatable" : "global::System.IEquatable";
        var comparer = ResolvesGenericCollections(model, position)
            ? "EqualityComparer"
            : "global::System.Collections.Generic.EqualityComparer";
        var hashCode = ResolvesInSystem(model, position, "HashCode") ? "HashCode" : "global::System.HashCode";
        var objectParameterType = model.GetNullableContext(position).AnnotationsEnabled() ? "object?" : "object";

        var baseType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName($"{equatable}<{selfType}>"));
        var indentation = GetMemberIndentation(declaration);
        var lineBreak = LineEndingHelper.GetLineBreak(declaration);
        return AddEquatableBase(declaration, baseType).AddMembers(
            ParseMember($"public bool Equals({selfType} other) => {BuildEqualsExpression(members, comparer)};", indentation, lineBreak),
            ParseMember($"public override bool Equals({objectParameterType} obj) => obj is {selfType} other && Equals(other);", indentation, lineBreak),
            ParseMember($"public override int GetHashCode() => {BuildHashExpression(members, hashCode)};", indentation, lineBreak),
            ParseMember($"public static bool operator ==({selfType} left, {selfType} right) => left.Equals(right);", indentation, lineBreak),
            ParseMember($"public static bool operator !=({selfType} left, {selfType} right) => !left.Equals(right);", indentation, lineBreak));
    }

    /// <summary>Returns the indentation for generated members: the struct's own indent plus one level.</summary>
    /// <param name="declaration">The struct declaration.</param>
    /// <returns>The member indentation whitespace.</returns>
    private static string GetMemberIndentation(StructDeclarationSyntax declaration)
    {
        var leading = declaration.GetLeadingTrivia();
        var structIndent = leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? leading[leading.Count - 1].ToString()
            : string.Empty;
        return structIndent + "    ";
    }

    /// <summary>Appends the equatable base type to the struct's base list, creating one when absent.</summary>
    /// <param name="declaration">The struct declaration.</param>
    /// <param name="baseType">The equatable base type.</param>
    /// <returns>The struct with the base recorded.</returns>
    private static StructDeclarationSyntax AddEquatableBase(StructDeclarationSyntax declaration, SimpleBaseTypeSyntax baseType)
    {
        if (declaration.BaseList is { } baseList)
        {
            return declaration.WithBaseList(baseList.AddTypes(baseType));
        }

        var identifier = declaration.TypeParameterList is null ? declaration.Identifier.WithoutTrivia() : declaration.Identifier;
        return declaration
            .WithIdentifier(identifier)
            .WithTypeParameterList(declaration.TypeParameterList?.WithoutTrailingTrivia())
            .WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(baseType))
                .WithLeadingTrivia(SyntaxFactory.Space)
                .WithTrailingTrivia(GetNameTrailingTrivia(declaration)));
    }

    /// <summary>Returns the trivia that followed the struct name, to carry after a new base list.</summary>
    /// <param name="declaration">The struct declaration.</param>
    /// <returns>The trailing trivia of the name or type parameter list.</returns>
    private static SyntaxTriviaList GetNameTrailingTrivia(StructDeclarationSyntax declaration)
        => declaration.TypeParameterList is { } typeParameters
            ? typeParameters.GetTrailingTrivia()
            : declaration.Identifier.TrailingTrivia;

    /// <summary>Parses one generated member, placing it on its own line after a separating blank line.</summary>
    /// <param name="text">The member source text.</param>
    /// <param name="indentation">The member indentation whitespace.</param>
    /// <param name="lineBreak">The file's line-break trivia.</param>
    /// <returns>The parsed member.</returns>
    private static MemberDeclarationSyntax ParseMember(string text, string indentation, SyntaxTrivia lineBreak)
        => SyntaxFactory.ParseMemberDeclaration(text)!
            .WithLeadingTrivia(lineBreak, SyntaxFactory.Whitespace(indentation))
            .WithTrailingTrivia(lineBreak);

    /// <summary>Builds the strongly typed Equals body comparing every data member.</summary>
    /// <param name="members">The instance data members.</param>
    /// <param name="comparer">The comparer type spelling.</param>
    /// <returns>The comparison expression text.</returns>
    private static string BuildEqualsExpression(ImmutableArray<(string Type, string Name)> members, string comparer)
    {
        if (members.Length == 0)
        {
            return "true";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < members.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(" && ");
            }

            builder.Append(comparer).Append('<').Append(members[i].Type).Append(">.Default.Equals(")
                .Append(members[i].Name).Append(", other.").Append(members[i].Name).Append(')');
        }

        return builder.ToString();
    }

    /// <summary>Builds the GetHashCode body combining every data member.</summary>
    /// <param name="members">The instance data members.</param>
    /// <param name="hashCode">The hash combiner type spelling.</param>
    /// <returns>The hash expression text.</returns>
    private static string BuildHashExpression(ImmutableArray<(string Type, string Name)> members, string hashCode)
    {
        if (members.Length == 0)
        {
            return "0";
        }

        var builder = new StringBuilder(hashCode).Append(".Combine(");
        for (var i = 0; i < members.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(members[i].Name);
        }

        return builder.Append(')').ToString();
    }

    /// <summary>Returns whether a simple name resolves to a type in the System namespace at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <param name="name">The simple type name.</param>
    /// <returns><see langword="true"/> when the simple spelling binds.</returns>
    private static bool ResolvesInSystem(SemanticModel model, int position, string name)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: name))
        {
            if (candidate is INamedTypeSymbol { ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the generic comparer resolves by simple name at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <returns><see langword="true"/> when the simple spelling binds.</returns>
    private static bool ResolvesGenericCollections(SemanticModel model, int position)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: "EqualityComparer"))
        {
            if (candidate is INamedTypeSymbol { IsGenericType: true } named
                && named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
            {
                return true;
            }
        }

        return false;
    }
}
