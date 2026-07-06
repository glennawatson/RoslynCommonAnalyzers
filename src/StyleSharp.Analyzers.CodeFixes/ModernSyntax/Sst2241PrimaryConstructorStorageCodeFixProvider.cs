// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Converts SST2241 storage-only constructors into primary constructors and member initializers.
/// The fix keeps the transform mechanical: constructor parameters move to the type declaration,
/// field and auto-property assignments become initializers, <c>base(...)</c> arguments move to the
/// base type, and constructor <c>param</c> documentation moves to the type documentation.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2241PrimaryConstructorStorageCodeFixProvider))]
[Shared]
public sealed class Sst2241PrimaryConstructorStorageCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The number of characters in a CRLF line ending.</summary>
    private const int CrlfLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UsePrimaryConstructorStorage.Id);

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
            if (!TryCreateReplacement(root, diagnostic, out _, out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Move storage to primary constructor",
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: nameof(Sst2241PrimaryConstructorStorageCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryFindConstructor(editor.OriginalRoot, diagnostic, out var type, out var constructor)
            || type is null
            || constructor is null
            || !TryCreateReplacement(type, constructor, out _))
        {
            return;
        }

        editor.ReplaceNode(type, (current, _) =>
        {
            var currentType = (TypeDeclarationSyntax)current;
            return TryFindConstructor(currentType, constructor.Identifier.ValueText, out var currentConstructor)
                && TryCreateReplacement(currentType, currentConstructor!, out var currentReplacement)
                ? currentReplacement!
                : current;
        });
    }

    /// <summary>Applies the primary-constructor storage rewrite.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original document when the diagnostic no longer resolves.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        if (!TryCreateReplacement(root, diagnostic, out var type, out var replacement)
            || type is null
            || replacement is null)
        {
            return document;
        }

        return document.WithSyntaxRoot(root.ReplaceNode(type, replacement));
    }

    /// <summary>Creates the replacement type for one SST2241 diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="type">The original type declaration.</param>
    /// <param name="replacement">The replacement type declaration.</param>
    /// <returns><see langword="true"/> when a safe mechanical replacement was built.</returns>
    private static bool TryCreateReplacement(
        SyntaxNode root,
        Diagnostic diagnostic,
        out TypeDeclarationSyntax? type,
        out TypeDeclarationSyntax? replacement)
    {
        type = null;
        replacement = null;
        if (!TryFindConstructor(root, diagnostic, out var containingType, out var constructor)
            || containingType is null
            || constructor is null
            || !TryCreateReplacement(containingType, constructor, out replacement))
        {
            return false;
        }

        type = containingType;
        return true;
    }

    /// <summary>Creates a replacement for the specified type and constructor.</summary>
    /// <param name="containingType">The type containing the constructor.</param>
    /// <param name="constructor">The constructor to move.</param>
    /// <param name="replacement">The replacement type.</param>
    /// <returns><see langword="true"/> when the replacement was built.</returns>
    private static bool TryCreateReplacement(
        TypeDeclarationSyntax containingType,
        ConstructorDeclarationSyntax constructor,
        out TypeDeclarationSyntax? replacement)
    {
        replacement = null;
        if (constructor.Body is not { } body
            || !TryCollectAssignments(body, out var assignments)
            || HasBodyScopeNameCollision(containingType, constructor)
            || !TryRewriteMembers(containingType, constructor, assignments, out var members)
            || !TryGetBaseList(containingType, constructor.Initializer, out var baseList))
        {
            return false;
        }

        replacement = WithParameterList(
                ClearPrimaryConstructorInsertionTrivia(containingType),
                CreatePrimaryConstructorParameterList(containingType, constructor.ParameterList))
            .WithBaseList(baseList)
            .WithMembers(members)
            .WithLeadingTrivia(MoveParameterDocsToType(containingType, constructor));
        return true;
    }

    /// <summary>Finds the constructor reported by an SST2241 diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="type">The containing type.</param>
    /// <param name="constructor">The constructor declaration.</param>
    /// <returns><see langword="true"/> when the constructor was found.</returns>
    private static bool TryFindConstructor(
        SyntaxNode root,
        Diagnostic diagnostic,
        out TypeDeclarationSyntax? type,
        out ConstructorDeclarationSyntax? constructor)
    {
        constructor = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
        type = constructor?.Parent as TypeDeclarationSyntax;
        return type is not null && constructor is not null;
    }

    /// <summary>Finds the matching constructor in a current replacement type node.</summary>
    /// <param name="type">The current type declaration.</param>
    /// <param name="constructorName">The constructor name.</param>
    /// <param name="constructor">The matching constructor.</param>
    /// <returns><see langword="true"/> when the constructor was found.</returns>
    private static bool TryFindConstructor(
        TypeDeclarationSyntax type,
        string constructorName,
        out ConstructorDeclarationSyntax? constructor)
    {
        var members = type.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is ConstructorDeclarationSyntax candidate
                && string.Equals(candidate.Identifier.ValueText, constructorName, StringComparison.Ordinal))
            {
                constructor = candidate;
                return true;
            }
        }

        constructor = null;
        return false;
    }

    /// <summary>Copies a constructor parameter list onto a type declaration.</summary>
    /// <param name="type">The type declaration.</param>
    /// <param name="parameterList">The primary-constructor parameter list.</param>
    /// <returns>The type with the primary-constructor parameter list.</returns>
    private static TypeDeclarationSyntax WithParameterList(TypeDeclarationSyntax type, ParameterListSyntax parameterList)
        => type switch
        {
            ClassDeclarationSyntax classDeclaration => classDeclaration.WithParameterList(parameterList),
            StructDeclarationSyntax structDeclaration => structDeclaration.WithParameterList(parameterList),
            _ => type
        };

    /// <summary>Clears trivia before the primary-constructor insertion point so the parameter list touches the type name.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns>The type declaration with insertion-point trailing trivia removed.</returns>
    private static TypeDeclarationSyntax ClearPrimaryConstructorInsertionTrivia(TypeDeclarationSyntax type)
    {
        if (type.TypeParameterList is { } typeParameterList)
        {
            return type.WithTypeParameterList(typeParameterList.WithGreaterThanToken(typeParameterList.GreaterThanToken.WithTrailingTrivia(default(SyntaxTriviaList))));
        }

        return type.WithIdentifier(type.Identifier.WithTrailingTrivia(default(SyntaxTriviaList)));
    }

    /// <summary>Creates a primary-constructor parameter list with separator trivia moved from the type name.</summary>
    /// <param name="type">The type declaration.</param>
    /// <param name="parameterList">The constructor parameter list.</param>
    /// <returns>The parameter list to attach to the type declaration.</returns>
    private static ParameterListSyntax CreatePrimaryConstructorParameterList(TypeDeclarationSyntax type, ParameterListSyntax parameterList)
        => parameterList
            .WithoutTrivia()
            .WithLeadingTrivia(default(SyntaxTriviaList))
            .WithTrailingTrivia(GetPrimaryConstructorTrailingTrivia(type));

    /// <summary>Gets the trivia that should follow the inserted primary-constructor parameter list.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns>The trailing trivia for the parameter list.</returns>
    private static SyntaxTriviaList GetPrimaryConstructorTrailingTrivia(TypeDeclarationSyntax type)
    {
        var trailing = type.TypeParameterList is { } typeParameterList
            ? typeParameterList.GreaterThanToken.TrailingTrivia
            : type.Identifier.TrailingTrivia;

        return trailing.Count != 0 || (type.BaseList is null && type.ConstraintClauses.Count == 0)
            ? trailing
            : SyntaxFactory.TriviaList(SyntaxFactory.Space);
    }

    /// <summary>Returns the base list that preserves a constructor <c>base(...)</c> call.</summary>
    /// <param name="type">The containing type.</param>
    /// <param name="initializer">The constructor initializer.</param>
    /// <param name="baseList">The updated base list.</param>
    /// <returns><see langword="true"/> when the initializer can be represented.</returns>
    private static bool TryGetBaseList(
        TypeDeclarationSyntax type,
        ConstructorInitializerSyntax? initializer,
        out BaseListSyntax? baseList)
    {
        baseList = type.BaseList;
        if (initializer is null || initializer.ArgumentList.Arguments.Count == 0)
        {
            return true;
        }

        if (!initializer.ThisOrBaseKeyword.IsKind(SyntaxKind.BaseKeyword)
            || type.BaseList is not { Types.Count: > 0 } currentBaseList)
        {
            return false;
        }

        var first = currentBaseList.Types[0];
        BaseTypeSyntax? replacement = first switch
        {
            SimpleBaseTypeSyntax simple => SyntaxFactory.PrimaryConstructorBaseType(
                simple.Type.WithoutTrivia(),
                initializer.ArgumentList.WithoutTrivia()).WithTriviaFrom(simple),
            PrimaryConstructorBaseTypeSyntax primary => primary.WithArgumentList(initializer.ArgumentList.WithoutTrivia()),
            _ => null
        };

        if (replacement is null)
        {
            return false;
        }

        baseList = currentBaseList.WithTypes(currentBaseList.Types.Replace(first, replacement));
        return true;
    }

    /// <summary>Returns whether promoted constructor parameters would be shadowed by declarations inside the type body.</summary>
    /// <param name="type">The type declaration.</param>
    /// <param name="constructor">The constructor being converted.</param>
    /// <returns><see langword="true"/> when a promoted parameter name would collide.</returns>
    private static bool HasBodyScopeNameCollision(TypeDeclarationSyntax type, ConstructorDeclarationSyntax constructor)
    {
        var parameters = constructor.ParameterList.Parameters;
        if (parameters.Count == 0)
        {
            return false;
        }

        var members = type.Members;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == constructor || member is TypeDeclarationSyntax)
            {
                continue;
            }

            if (ContainsParameterNameDeclaration(member, parameters))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node declares one of the promoted constructor parameter names.</summary>
    /// <param name="node">The syntax node to inspect.</param>
    /// <param name="parameters">The promoted constructor parameters.</param>
    /// <returns><see langword="true"/> when a matching declaration is found.</returns>
    private static bool ContainsParameterNameDeclaration(SyntaxNode node, SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        if (node is TypeDeclarationSyntax)
        {
            return false;
        }

        if (IsPromotedParameterDeclaration(node, parameters))
        {
            return true;
        }

        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].AsNode() is { } child && ContainsParameterNameDeclaration(child, parameters))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node declares a promoted constructor parameter name.</summary>
    /// <param name="node">The node to inspect.</param>
    /// <param name="parameters">The promoted constructor parameters.</param>
    /// <returns><see langword="true"/> when the node declares a matching name.</returns>
    private static bool IsPromotedParameterDeclaration(SyntaxNode node, SeparatedSyntaxList<ParameterSyntax> parameters)
        => node switch
        {
            ParameterSyntax parameter => IsPromotedParameterName(parameter.Identifier, parameters),
            VariableDeclaratorSyntax variable => IsPromotedParameterName(variable.Identifier, parameters),
            ForEachStatementSyntax forEach => IsPromotedParameterName(forEach.Identifier, parameters),
            CatchDeclarationSyntax catchDeclaration => IsPromotedParameterName(catchDeclaration.Identifier, parameters),
            SingleVariableDesignationSyntax designation => IsPromotedParameterName(designation.Identifier, parameters),
            LocalFunctionStatementSyntax localFunction => IsPromotedParameterName(localFunction.Identifier, parameters),
            _ => false
        };

    /// <summary>Returns whether an identifier matches a promoted constructor parameter name.</summary>
    /// <param name="identifier">The identifier to inspect.</param>
    /// <param name="parameters">The promoted constructor parameters.</param>
    /// <returns><see langword="true"/> when the identifier matches a promoted parameter.</returns>
    private static bool IsPromotedParameterName(SyntaxToken identifier, SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        if (identifier.RawKind == 0)
        {
            return false;
        }

        var name = identifier.ValueText;
        for (var i = 0; i < parameters.Count; i++)
        {
            if (string.Equals(parameters[i].Identifier.ValueText, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Collects constructor assignments that can become member initializers.</summary>
    /// <param name="body">The constructor body.</param>
    /// <param name="assignments">The collected storage assignments.</param>
    /// <returns><see langword="true"/> when every statement is a simple storage assignment.</returns>
    private static bool TryCollectAssignments(BlockSyntax body, out StorageAssignment[] assignments)
    {
        assignments = [];
        var statements = body.Statements;
        if (statements.Count == 0)
        {
            return false;
        }

        var collected = new StorageAssignment[statements.Count];
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i] is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment }
                || assignment.Right is not IdentifierNameSyntax parameter
                || !TryGetTargetName(assignment.Left, out var targetName))
            {
                return false;
            }

            collected[i] = new StorageAssignment(targetName, parameter.WithoutTrivia());
        }

        assignments = collected;
        return true;
    }

    /// <summary>Returns the assigned member name from a constructor storage target.</summary>
    /// <param name="target">The assignment target.</param>
    /// <param name="name">The member name.</param>
    /// <returns><see langword="true"/> when the target shape is supported.</returns>
    private static bool TryGetTargetName(ExpressionSyntax target, out string name)
    {
        name = target switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax identifier } => identifier.Identifier.ValueText,
            _ => string.Empty
        };

        return name.Length > 0;
    }

    /// <summary>Rewrites type members by removing the constructor and adding storage initializers.</summary>
    /// <param name="type">The containing type.</param>
    /// <param name="constructor">The constructor to remove.</param>
    /// <param name="assignments">The storage assignments.</param>
    /// <param name="members">The rewritten members.</param>
    /// <returns><see langword="true"/> when every assignment was applied to a member.</returns>
    private static bool TryRewriteMembers(
        TypeDeclarationSyntax type,
        ConstructorDeclarationSyntax constructor,
        StorageAssignment[] assignments,
        out SyntaxList<MemberDeclarationSyntax> members)
    {
        var rewritten = new List<MemberDeclarationSyntax>(type.Members.Count - 1);
        var applied = new bool[assignments.Length];
        var typeMembers = type.Members;
        for (var i = 0; i < typeMembers.Count; i++)
        {
            var member = typeMembers[i];
            if (member == constructor)
            {
                continue;
            }

            if (!TryRewriteMember(member, assignments, applied, out var updated))
            {
                members = default;
                return false;
            }

            rewritten.Add(updated);
        }

        for (var i = 0; i < applied.Length; i++)
        {
            if (!applied[i])
            {
                members = default;
                return false;
            }
        }

        members = SyntaxFactory.List(rewritten);
        return true;
    }

    /// <summary>Rewrites one member declaration with any matching storage initializer.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="assignments">The storage assignments.</param>
    /// <param name="applied">Tracks assignments already applied.</param>
    /// <param name="updated">The updated member.</param>
    /// <returns><see langword="true"/> when the member can be preserved safely.</returns>
    private static bool TryRewriteMember(
        MemberDeclarationSyntax member,
        StorageAssignment[] assignments,
        bool[] applied,
        out MemberDeclarationSyntax updated)
    {
        updated = member;
        return member switch
        {
            FieldDeclarationSyntax field => TryRewriteField(field, assignments, applied, out updated),
            PropertyDeclarationSyntax property => TryRewriteProperty(property, assignments, applied, out updated),
            _ => true
        };
    }

    /// <summary>Rewrites field variables that receive constructor parameters.</summary>
    /// <param name="field">The field declaration.</param>
    /// <param name="assignments">The storage assignments.</param>
    /// <param name="applied">Tracks assignments already applied.</param>
    /// <param name="updated">The updated member.</param>
    /// <returns><see langword="true"/> when the field declaration can be rewritten safely.</returns>
    private static bool TryRewriteField(
        FieldDeclarationSyntax field,
        StorageAssignment[] assignments,
        bool[] applied,
        out MemberDeclarationSyntax updated)
    {
        var variables = field.Declaration.Variables;
        var changed = false;
        for (var i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            if (!TryFindAssignment(assignments, applied, variable.Identifier.ValueText, out var assignmentIndex, out var value))
            {
                continue;
            }

            if (variable.Initializer is not null)
            {
                updated = field;
                return false;
            }

            variables = variables.Replace(variable, variable.WithInitializer(CreateInitializer(value!)));
            applied[assignmentIndex] = true;
            changed = true;
        }

        updated = changed ? field.WithDeclaration(field.Declaration.WithVariables(variables)) : field;
        return true;
    }

    /// <summary>Rewrites an auto-property that receives a constructor parameter.</summary>
    /// <param name="property">The property declaration.</param>
    /// <param name="assignments">The storage assignments.</param>
    /// <param name="applied">Tracks assignments already applied.</param>
    /// <param name="updated">The updated member.</param>
    /// <returns><see langword="true"/> when the property can be rewritten safely.</returns>
    private static bool TryRewriteProperty(
        PropertyDeclarationSyntax property,
        StorageAssignment[] assignments,
        bool[] applied,
        out MemberDeclarationSyntax updated)
    {
        updated = property;
        if (!TryFindAssignment(assignments, applied, property.Identifier.ValueText, out var assignmentIndex, out var value))
        {
            return true;
        }

        if (!IsAutoProperty(property) || property.Initializer is not null)
        {
            return false;
        }

        updated = property
            .WithInitializer(CreateInitializer(value!))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        applied[assignmentIndex] = true;
        return true;
    }

    /// <summary>Returns whether a property can carry an initializer.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns><see langword="true"/> for auto-properties.</returns>
    private static bool IsAutoProperty(PropertyDeclarationSyntax property)
    {
        if (property.AccessorList is not { } accessors)
        {
            return false;
        }

        var accessorList = accessors.Accessors;
        for (var i = 0; i < accessorList.Count; i++)
        {
            if (accessorList[i].Body is not null || accessorList[i].ExpressionBody is not null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds the unapplied assignment for a member name.</summary>
    /// <param name="assignments">The storage assignments.</param>
    /// <param name="applied">Tracks assignments already applied.</param>
    /// <param name="targetName">The member name.</param>
    /// <param name="index">The assignment index.</param>
    /// <param name="value">The initializer value.</param>
    /// <returns><see langword="true"/> when an assignment exists.</returns>
    private static bool TryFindAssignment(
        StorageAssignment[] assignments,
        bool[] applied,
        string targetName,
        out int index,
        out ExpressionSyntax? value)
    {
        for (var i = 0; i < assignments.Length; i++)
        {
            if (!applied[i] && string.Equals(assignments[i].TargetName, targetName, StringComparison.Ordinal))
            {
                index = i;
                value = assignments[i].Value;
                return true;
            }
        }

        index = -1;
        value = null;
        return false;
    }

    /// <summary>Creates a spaced equals-value clause.</summary>
    /// <param name="value">The initializer value.</param>
    /// <returns>The initializer syntax.</returns>
    private static EqualsValueClauseSyntax CreateInitializer(ExpressionSyntax value)
        => SyntaxFactory.EqualsValueClause(
            SyntaxFactory.Token(SyntaxKind.EqualsToken).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space),
            value.WithoutTrivia());

    /// <summary>Moves constructor parameter documentation to the containing type.</summary>
    /// <param name="type">The type declaration.</param>
    /// <param name="constructor">The constructor declaration.</param>
    /// <returns>The leading trivia for the replacement type.</returns>
    private static SyntaxTriviaList MoveParameterDocsToType(TypeDeclarationSyntax type, ConstructorDeclarationSyntax constructor)
    {
        var parameterDocs = CollectParameterDocs(type, constructor);
        if (parameterDocs.Count == 0)
        {
            return type.GetLeadingTrivia();
        }

        var leading = type.GetLeadingTrivia();
        var insertionIndex = FindTypeDocumentationEnd(leading);
        var result = new List<SyntaxTrivia>(leading.Count + parameterDocs.Count);
        for (var i = 0; i < insertionIndex; i++)
        {
            result.Add(leading[i]);
        }

        for (var i = 0; i < parameterDocs.Count; i++)
        {
            result.Add(parameterDocs[i]);
        }

        for (var i = insertionIndex; i < leading.Count; i++)
        {
            result.Add(leading[i]);
        }

        return SyntaxFactory.TriviaList(result);
    }

    /// <summary>Collects constructor <c>param</c> documentation trivia normalized to the type indentation.</summary>
    /// <param name="type">The type declaration.</param>
    /// <param name="constructor">The constructor declaration.</param>
    /// <returns>The parameter documentation trivia.</returns>
    private static List<SyntaxTrivia> CollectParameterDocs(TypeDeclarationSyntax type, ConstructorDeclarationSyntax constructor)
    {
        var indentation = GetTypeIndentation(type);
        var collected = new List<SyntaxTrivia>();
        var text = constructor.GetLeadingTrivia().ToFullString();
        var start = 0;
        while (start < text.Length)
        {
            var end = start;
            while (end < text.Length && text[end] != '\r' && text[end] != '\n')
            {
                end++;
            }

            if (end < text.Length)
            {
                if (text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n')
                {
                    end += CrlfLength;
                }
                else
                {
                    end++;
                }
            }

            var line = text.Substring(start, end - start);
            if (IsParameterDocumentationLine(line))
            {
                AddNormalizedDocumentationLine(collected, line, indentation);
            }

            start = end;
        }

        return collected;
    }

    /// <summary>Adds a documentation line with indentation matching the target type.</summary>
    /// <param name="collected">The destination trivia list.</param>
    /// <param name="line">The source documentation line.</param>
    /// <param name="indentation">The target indentation.</param>
    private static void AddNormalizedDocumentationLine(List<SyntaxTrivia> collected, string line, string indentation)
    {
        var markerIndex = line.IndexOf("///", StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return;
        }

        var parsed = SyntaxFactory.ParseLeadingTrivia(indentation + line.Substring(markerIndex));
        for (var i = 0; i < parsed.Count; i++)
        {
            collected.Add(parsed[i]);
        }
    }

    /// <summary>Returns whether a documentation line is constructor parameter documentation.</summary>
    /// <param name="line">The line to inspect.</param>
    /// <returns><see langword="true"/> when the trivia contains a <c>param</c> element.</returns>
    private static bool IsParameterDocumentationLine(string line)
        => line.IndexOf("///", StringComparison.Ordinal) >= 0
            && line.IndexOf("<param ", StringComparison.Ordinal) >= 0;

    /// <summary>Finds where constructor parameter docs should be inserted in type leading trivia.</summary>
    /// <param name="leading">The type leading trivia.</param>
    /// <returns>The insertion index.</returns>
    private static int FindTypeDocumentationEnd(SyntaxTriviaList leading)
    {
        var insertionIndex = 0;
        for (var i = 0; i < leading.Count; i++)
        {
            if (leading[i].IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
            {
                insertionIndex = i + 1;
            }
        }

        return insertionIndex;
    }

    /// <summary>Gets the indentation used by the type declaration's documentation.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns>The indentation text.</returns>
    private static string GetTypeIndentation(TypeDeclarationSyntax type)
    {
        var leading = type.GetLeadingTrivia();
        var text = leading.ToFullString();
        var markerIndex = text.IndexOf("///", StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            return GetLinePrefix(text, markerIndex);
        }

        var lineStart = Math.Max(text.LastIndexOf('\n'), text.LastIndexOf('\r')) + 1;
        return lineStart < text.Length ? text.Substring(lineStart) : text;
    }

    /// <summary>Gets the text before a target index on the same line.</summary>
    /// <param name="text">The text to inspect.</param>
    /// <param name="index">The target index.</param>
    /// <returns>The text between the start of the line and the target index.</returns>
    private static string GetLinePrefix(string text, int index)
    {
        var lineStart = index;
        while (lineStart > 0 && text[lineStart - 1] != '\r' && text[lineStart - 1] != '\n')
        {
            lineStart--;
        }

        return lineStart < index ? text.Substring(lineStart, index - lineStart) : string.Empty;
    }

    /// <summary>Captures a constructor storage assignment.</summary>
    /// <param name="TargetName">The assigned member name.</param>
    /// <param name="Value">The constructor parameter expression.</param>
    private readonly record struct StorageAssignment(string TargetName, ExpressionSyntax Value);
}
