// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Shared conservative semantic checks for private-field simplification rules.</summary>
internal static class FieldReferenceAnalysis
{
    /// <summary>
    /// Caches, per containing type, the identifiers that share a declared field name. Building this
    /// once per type turns the single-use check from a whole-type rescan per property into one shared
    /// syntactic scan, so a type with many backing-field properties no longer costs quadratic time.
    /// </summary>
    private static readonly ConditionalWeakTable<TypeDeclarationSyntax, TypeFieldReferenceIndex> IndexCache = new();

    /// <summary>Finds a private single-variable backing field referenced by a property and nowhere else.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="property">The property declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="field">The backing-field declaration.</param>
    /// <param name="variable">The backing-field variable.</param>
    /// <param name="symbol">The backing-field symbol.</param>
    /// <returns><see langword="true"/> when a suitable single-use field is found.</returns>
    public static bool TryFindSingleUseBackingField(
        SemanticModel model,
        PropertyDeclarationSyntax property,
        CancellationToken cancellationToken,
        out FieldDeclarationSyntax? field,
        out VariableDeclaratorSyntax? variable,
        out IFieldSymbol? symbol)
    {
        field = null;
        variable = null;
        symbol = null;
        if (property.Parent is not TypeDeclarationSyntax type
            || ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword)
            || property.AccessorList is null)
        {
            return false;
        }

        if (FindReferencedField(model, property, cancellationToken) is not { } candidate
            || !TryGetDeclaration(candidate, cancellationToken, out var declaration, out var declarator)
            || !IsEligible(candidate, declaration!)
            || !OnlyReferencedInside(model, type, candidate, property, cancellationToken))
        {
            return false;
        }

        field = declaration;
        variable = declarator;
        symbol = candidate;
        return true;
    }

    /// <summary>Finds a private single-variable backing field with the supplied name referenced by a property and nowhere else.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="property">The property declaration.</param>
    /// <param name="fieldName">The expected backing-field name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="field">The backing-field declaration.</param>
    /// <param name="variable">The backing-field variable.</param>
    /// <param name="symbol">The backing-field symbol.</param>
    /// <returns><see langword="true"/> when a suitable single-use field is found.</returns>
    public static bool TryFindSingleUseBackingField(
        SemanticModel model,
        PropertyDeclarationSyntax property,
        string fieldName,
        CancellationToken cancellationToken,
        out FieldDeclarationSyntax? field,
        out VariableDeclaratorSyntax? variable,
        out IFieldSymbol? symbol)
    {
        field = null;
        variable = null;
        symbol = null;
        if (property.Parent is not TypeDeclarationSyntax type
            || ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword)
            || property.AccessorList is null
            || !TryFindFieldDeclaration(type, fieldName, out var declaration, out var declarator)
            || declaration is null
            || declarator is null
            || model.GetDeclaredSymbol(declarator, cancellationToken) is not IFieldSymbol candidate
            || !IsEligible(candidate, declaration)
            || !OnlyReferencedInside(model, type, candidate, property, declaration, cancellationToken))
        {
            return false;
        }

        field = declaration;
        variable = declarator;
        symbol = candidate;
        return true;
    }

    /// <summary>Returns whether every bound reference to a field lies inside one allowed node.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The containing type.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="allowed">The node allowed to contain references.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when all references are inside the allowed node.</returns>
    public static bool OnlyReferencedInside(
        SemanticModel model,
        TypeDeclarationSyntax type,
        IFieldSymbol field,
        SyntaxNode allowed,
        CancellationToken cancellationToken)
    {
        var references = TypeFieldReferenceIndex.GetOrCreate(type).ReferencesFor(field.Name);
        var allowedSpan = allowed.FullSpan;
        var found = false;
        for (var i = 0; i < references.Count; i++)
        {
            var identifier = references[i];
            if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, field))
            {
                continue;
            }

            if (!allowedSpan.Contains(identifier.Span))
            {
                return false;
            }

            found = true;
        }

        return found;
    }

    /// <summary>Returns every identifier in the type whose text matches a declared field name, scanning the type at most once per type.</summary>
    /// <param name="type">The containing type.</param>
    /// <param name="fieldName">The declared field name to look up.</param>
    /// <returns>The matching identifiers, cached and shared across every rule that queries the same type.</returns>
    /// <remarks>
    /// Callers must still bind each returned identifier with their own semantic model to confirm it
    /// references the intended field; the index is purely syntactic (name-matched) and compilation-agnostic.
    /// </remarks>
    internal static IReadOnlyList<IdentifierNameSyntax> FieldNameReferences(TypeDeclarationSyntax type, string fieldName)
        => TypeFieldReferenceIndex.GetOrCreate(type).ReferencesFor(fieldName);

    /// <summary>Returns whether an expression is syntactically known to reference a private object field declared in the same type.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="expression">The already-unwrapped expression to inspect.</param>
    /// <returns><see langword="true"/> when the expression safely names a private object field.</returns>
    internal static bool IsPrivateObjectFieldLockTarget(TypeDeclarationSyntax type, ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax identifier && !IsShadowed(identifier)
            && TryFindPrivateObjectField(type, identifier.Identifier.ValueText);

    /// <summary>Returns whether a reference writes to a field.</summary>
    /// <param name="identifier">The field reference.</param>
    /// <returns><see langword="true"/> when the reference is a write.</returns>
    internal static bool IsWrite(IdentifierNameSyntax identifier)
    {
        SyntaxNode expression = identifier;
        if (identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier)
        {
            expression = access;
        }

        return expression.Parent switch
        {
            AssignmentExpressionSyntax assignment when assignment.Left == expression => true,
            PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression) ||
                                                  prefix.IsKind(SyntaxKind.PreDecrementExpression),
            _ => expression.Parent is PostfixUnaryExpressionSyntax or ArgumentSyntax { RefOrOutKeyword.RawKind: not 0 }
        };
    }

    /// <summary>Returns whether every bound reference to a field lies inside one allowed node, skipping one declaration.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The containing type.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="allowed">The node allowed to contain references.</param>
    /// <param name="declarationToSkip">The declaration to skip while scanning siblings.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when all references are inside the allowed node.</returns>
    private static bool OnlyReferencedInside(
        SemanticModel model,
        TypeDeclarationSyntax type,
        IFieldSymbol field,
        SyntaxNode allowed,
        FieldDeclarationSyntax? declarationToSkip,
        CancellationToken cancellationToken)
    {
        if (!ContainsFieldReference(model, allowed, field, cancellationToken))
        {
            return false;
        }

        for (var i = 0; i < type.Members.Count; i++)
        {
            var member = type.Members[i];
            if (member == allowed || member == declarationToSkip)
            {
                continue;
            }

            if (ContainsFieldReference(model, member, field, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a field and declaration meet the shared eligibility requirements.</summary>
    /// <param name="candidate">The field symbol.</param>
    /// <param name="declaration">The field declaration.</param>
    /// <returns><see langword="true"/> when the field is eligible.</returns>
    private static bool IsEligible(IFieldSymbol candidate, FieldDeclarationSyntax declaration) =>
        !candidate.IsStatic
        && candidate.DeclaredAccessibility == Accessibility.Private
        && declaration.Declaration.Variables.Count == 1
        && declaration.AttributeLists.Count == 0
        && !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.VolatileKeyword);

    /// <summary>Gets the single source declaration for a field symbol.</summary>
    /// <param name="candidate">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="declaration">The field declaration.</param>
    /// <param name="declarator">The variable declarator.</param>
    /// <returns><see langword="true"/> when a single declaration is available.</returns>
    private static bool TryGetDeclaration(
        IFieldSymbol candidate,
        CancellationToken cancellationToken,
        out FieldDeclarationSyntax? declaration,
        out VariableDeclaratorSyntax? declarator)
    {
        declaration = null;
        declarator = null;
        if (candidate.DeclaringSyntaxReferences is not [var syntaxReference]
            || syntaxReference.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax variable
            || variable.Parent?.Parent is not FieldDeclarationSyntax field)
        {
            return false;
        }

        declaration = field;
        declarator = variable;
        return true;
    }

    /// <summary>Finds the first field symbol referenced by a property.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="property">The property.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The field symbol, or <see langword="null"/>.</returns>
    private static IFieldSymbol? FindReferencedField(
        SemanticModel model,
        PropertyDeclarationSyntax property,
        CancellationToken cancellationToken)
    {
        var children = property.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsNode || child.AsNode() is not { } childNode)
            {
                continue;
            }

            if (TryFindReferencedField(childNode, model, cancellationToken, out var field))
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>Finds the first field symbol referenced by a descendant node in preorder.</summary>
    /// <param name="node">The subtree to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="field">The discovered field symbol.</param>
    /// <returns><see langword="true"/> when a field reference is found.</returns>
    private static bool TryFindReferencedField(
        SyntaxNode node,
        SemanticModel model,
        CancellationToken cancellationToken,
        out IFieldSymbol? field)
    {
        field = null;
        if (node is IdentifierNameSyntax identifier)
        {
            if (model.GetSymbolInfo(identifier, cancellationToken).Symbol is IFieldSymbol candidate)
            {
                field = candidate;
                return true;
            }

            return false;
        }

        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsNode || child.AsNode() is not { } childNode)
            {
                continue;
            }

            if (TryFindReferencedField(childNode, model, cancellationToken, out field))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a syntax subtree contains an identifier bound to the field.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="root">The subtree to inspect.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the subtree contains a bound field reference.</returns>
    private static bool ContainsFieldReference(
        SemanticModel model,
        SyntaxNode root,
        IFieldSymbol field,
        CancellationToken cancellationToken)
    {
        var children = root.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsNode || child.AsNode() is not { } childNode)
            {
                continue;
            }

            if (ContainsFieldReference(childNode, model, field, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a subtree contains a descendant identifier bound to the supplied field.</summary>
    /// <param name="node">The subtree to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a matching field reference is found.</returns>
    private static bool ContainsFieldReference(
        SyntaxNode node,
        SemanticModel model,
        IFieldSymbol field,
        CancellationToken cancellationToken)
    {
        if (node is IdentifierNameSyntax identifier)
        {
            return identifier.Identifier.ValueText == field.Name
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, field);
        }

        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsNode || child.AsNode() is not { } childNode)
            {
                continue;
            }

            if (ContainsFieldReference(childNode, model, field, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the direct field declaration with the supplied name in the containing type.</summary>
    /// <param name="type">The containing type.</param>
    /// <param name="name">The field name.</param>
    /// <param name="declaration">The matching field declaration.</param>
    /// <param name="declarator">The matching variable declarator.</param>
    /// <returns><see langword="true"/> when the field declaration is found.</returns>
    private static bool TryFindFieldDeclaration(
        TypeDeclarationSyntax type,
        string name,
        out FieldDeclarationSyntax? declaration,
        out VariableDeclaratorSyntax? declarator)
    {
        declaration = null;
        declarator = null;
        for (var memberIndex = 0; memberIndex < type.Members.Count; memberIndex++)
        {
            if (type.Members[memberIndex] is not FieldDeclarationSyntax field)
            {
                continue;
            }

            var variables = field.Declaration.Variables;
            for (var variableIndex = 0; variableIndex < variables.Count; variableIndex++)
            {
                var variable = variables[variableIndex];
                if (variable.Identifier.ValueText == name)
                {
                    declaration = field;
                    declarator = variable;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether the type contains a matching private object field.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="name">The expected field name.</param>
    /// <returns><see langword="true"/> when the field exists.</returns>
    private static bool TryFindPrivateObjectField(TypeDeclarationSyntax type, string name)
    {
        for (var i = 0; i < type.Members.Count; i++)
        {
            if (type.Members[i] is FieldDeclarationSyntax field && IsPrivateObjectField(field, name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a field declaration defines the named private object field.</summary>
    /// <param name="field">The field declaration.</param>
    /// <param name="name">The expected field name.</param>
    /// <returns><see langword="true"/> when the field matches the private object pattern.</returns>
    private static bool IsPrivateObjectField(FieldDeclarationSyntax field, string name)
    {
        if (!IsUnambiguousObjectType(field.Declaration.Type) || !HasPrivateModifier(field.Modifiers))
        {
            return false;
        }

        var variables = field.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            if (variables[i].Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the identifier is shadowed by a parameter or earlier local declaration in the current callable scope.</summary>
    /// <param name="identifier">The identifier to inspect.</param>
    /// <returns><see langword="true"/> when syntax alone cannot safely bind the field.</returns>
    private static bool IsShadowed(IdentifierNameSyntax identifier)
    {
        var name = identifier.Identifier.ValueText;
        for (var current = identifier.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case BaseMethodDeclarationSyntax method when HasParameterNamed(method.ParameterList.Parameters, name):
                case LocalFunctionStatementSyntax localFunction when HasParameterNamed(localFunction.ParameterList.Parameters, name):
                case SimpleLambdaExpressionSyntax lambda when lambda.Parameter.Identifier.ValueText == name:
                case ParenthesizedLambdaExpressionSyntax parenthesizedLambda when HasParameterNamed(parenthesizedLambda.ParameterList.Parameters, name):
                case AnonymousMethodExpressionSyntax anonymousMethod when anonymousMethod.ParameterList is not null && HasParameterNamed(anonymousMethod.ParameterList.Parameters, name):
                    return true;

                case AccessorDeclarationSyntax accessor:
                    return HasEarlierLocalNamed(accessor, identifier.SpanStart, name);

                case BaseMethodDeclarationSyntax method:
                    return HasEarlierLocalNamed(method, identifier.SpanStart, name);

                case LocalFunctionStatementSyntax localFunction:
                    return HasEarlierLocalNamed(localFunction, identifier.SpanStart, name);

                case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                    return HasEarlierLocalNamed(parenthesizedLambda, identifier.SpanStart, name);

                case SimpleLambdaExpressionSyntax simpleLambda:
                    return HasEarlierLocalNamed(simpleLambda, identifier.SpanStart, name);

                case AnonymousMethodExpressionSyntax anonymousMethod:
                    return HasEarlierLocalNamed(anonymousMethod, identifier.SpanStart, name);
            }
        }

        return false;
    }

    /// <summary>Returns whether a parameter list contains the specified name.</summary>
    /// <param name="parameters">The parameters to inspect.</param>
    /// <param name="name">The expected parameter name.</param>
    /// <returns><see langword="true"/> when a parameter matches.</returns>
    private static bool HasParameterNamed(SeparatedSyntaxList<ParameterSyntax> parameters, string name)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a callable scope declares a matching local before the field reference.</summary>
    /// <param name="scope">The scope to inspect.</param>
    /// <param name="position">The reference position.</param>
    /// <param name="name">The identifier name.</param>
    /// <returns><see langword="true"/> when an earlier local declaration shadows the field.</returns>
    private static bool HasEarlierLocalNamed(SyntaxNode scope, int position, string name)
    {
        var state = new EarlierLocalSearchState(position, name, Found: false);
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, EarlierLocalSearchState>(scope, ref state, VisitEarlierLocalCandidate);
        return state.Found;
    }

    /// <summary>Returns whether a type syntax unambiguously denotes <c>System.Object</c> without semantic binding.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns><see langword="true"/> for unambiguous object spellings.</returns>
    private static bool IsUnambiguousObjectType(TypeSyntax type)
        => (type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword))
            || (type is QualifiedNameSyntax { Right.Identifier.ValueText: "Object", Left: var left } && IsSystemNamespace(left));

    /// <summary>Records whether the scan has found a matching local declared before the reference.</summary>
    /// <param name="node">The visited syntax node.</param>
    /// <param name="state">The current search state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> to stop.</returns>
    private static bool VisitEarlierLocalCandidate(SyntaxNode node, ref EarlierLocalSearchState state)
    {
        if (node.SpanStart >= state.Position)
        {
            return true;
        }

        switch (node)
        {
            case VariableDeclaratorSyntax variable when variable.Identifier.ValueText == state.Name:
            case SingleVariableDesignationSyntax designation when designation.Identifier.ValueText == state.Name:
            case ForEachStatementSyntax foreachStatement when foreachStatement.Identifier.ValueText == state.Name:
            case CatchDeclarationSyntax catchDeclaration when catchDeclaration.Identifier.ValueText == state.Name:
                {
                    state = state with { Found = true };
                    return false;
                }

            default:
                return true;
        }
    }

    /// <summary>Returns whether a modifier list contains <c>private</c>.</summary>
    /// <param name="modifiers">The modifier list to inspect.</param>
    /// <returns><see langword="true"/> when the field is private.</returns>
    private static bool HasPrivateModifier(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.PrivateKeyword))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a name syntax denotes the <c>System</c> namespace.</summary>
    /// <param name="name">The syntax to inspect.</param>
    /// <returns><see langword="true"/> when the syntax denotes <c>System</c>.</returns>
    private static bool IsSystemNamespace(NameSyntax name)
        => name is IdentifierNameSyntax { Identifier.ValueText: "System" }
            or AliasQualifiedNameSyntax { Alias.Identifier.ValueText: "global", Name.Identifier.ValueText: "System" };

    /// <summary>Captures the state required while searching for earlier locals.</summary>
    private readonly record struct EarlierLocalSearchState(int Position, string Name, bool Found);

    /// <summary>Caches, per containing type, the identifiers that share a declared field name.</summary>
    private sealed class TypeFieldReferenceIndex
    {
        /// <summary>The empty result returned when a name is never referenced.</summary>
        private static readonly IReadOnlyList<IdentifierNameSyntax> None = [];

        /// <summary>The identifiers in the type grouped by the declared field name they spell.</summary>
        private readonly Dictionary<string, List<IdentifierNameSyntax>> _referencesByName;

        /// <summary>Initializes a new instance of the <see cref="TypeFieldReferenceIndex"/> class.</summary>
        /// <param name="referencesByName">The identifiers grouped by declared field name.</param>
        private TypeFieldReferenceIndex(Dictionary<string, List<IdentifierNameSyntax>> referencesByName)
            => _referencesByName = referencesByName;

        /// <summary>Gets the cached index for a type, building it on first request.</summary>
        /// <param name="type">The containing type declaration.</param>
        /// <returns>The reference index for the type.</returns>
        public static TypeFieldReferenceIndex GetOrCreate(TypeDeclarationSyntax type)
            => IndexCache.GetValue(type, Build);

        /// <summary>Returns every identifier in the type whose text matches a declared field name.</summary>
        /// <param name="name">The field name.</param>
        /// <returns>The matching identifiers, or an empty list when the name is never referenced.</returns>
        public IReadOnlyList<IdentifierNameSyntax> ReferencesFor(string name)
            => _referencesByName.TryGetValue(name, out var references) ? references : None;

        /// <summary>Builds the index by scanning the type once for identifiers that name a declared field.</summary>
        /// <param name="type">The containing type declaration.</param>
        /// <returns>The populated index.</returns>
        private static TypeFieldReferenceIndex Build(TypeDeclarationSyntax type)
        {
            var referencesByName = new Dictionary<string, List<IdentifierNameSyntax>>(StringComparer.Ordinal);
            var fieldNames = CollectDeclaredFieldNames(type);
            if (fieldNames.Count > 0)
            {
                var state = new ReferenceCollectorState(fieldNames, referencesByName);
                DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, ReferenceCollectorState>(type, ref state, CollectReference);
            }

            return new TypeFieldReferenceIndex(referencesByName);
        }

        /// <summary>Records an identifier whose text matches a declared field name.</summary>
        /// <param name="identifier">The visited identifier.</param>
        /// <param name="state">The collector state.</param>
        /// <returns>Always <see langword="true"/> so the whole type is scanned.</returns>
        private static bool CollectReference(IdentifierNameSyntax identifier, ref ReferenceCollectorState state)
        {
            var name = identifier.Identifier.ValueText;
            if (!state.FieldNames.Contains(name))
            {
                return true;
            }

            if (!state.ReferencesByName.TryGetValue(name, out var references))
            {
                references = [];
                state.ReferencesByName.Add(name, references);
            }

            references.Add(identifier);
            return true;
        }

        /// <summary>Collects the names of every field declared directly in the type.</summary>
        /// <param name="type">The containing type declaration.</param>
        /// <returns>The set of declared field names.</returns>
        private static HashSet<string> CollectDeclaredFieldNames(TypeDeclarationSyntax type)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < type.Members.Count; i++)
            {
                if (type.Members[i] is not FieldDeclarationSyntax field)
                {
                    continue;
                }

                var variables = field.Declaration.Variables;
                for (var j = 0; j < variables.Count; j++)
                {
                    names.Add(variables[j].Identifier.ValueText);
                }
            }

            return names;
        }

        /// <summary>Threads the field-name filter and reference map through the descendant walk.</summary>
        private readonly record struct ReferenceCollectorState(
            HashSet<string> FieldNames,
            Dictionary<string, List<IdentifierNameSyntax>> ReferencesByName);
    }
}
