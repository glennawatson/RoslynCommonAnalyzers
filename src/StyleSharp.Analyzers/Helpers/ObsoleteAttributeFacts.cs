// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The facts about an <c>[Obsolete]</c> attribute that SST2308, SST2310 and SST2314 all ask for:
/// whether the attribute really is the framework's, what it says, and what it is written on.
/// </summary>
/// <remarks>
/// The order the questions are asked in is the point. The attribute's simple name is matched first,
/// on syntax alone, which rejects every other attribute in the file for free; the message is read off
/// a string literal's token where it is one; and only an attribute that is about to be reported is
/// bound at all, which is what confirms it is <c>System.ObsoleteAttribute</c> and not a type of the
/// same name.
/// </remarks>
internal static class ObsoleteAttributeFacts
{
    /// <summary>The name of the attribute's <c>DiagnosticId</c> property.</summary>
    public const string DiagnosticIdPropertyName = "DiagnosticId";

    /// <summary>The metadata name of the framework's obsolete attribute.</summary>
    public const string ObsoleteAttributeMetadataName = "ObsoleteAttribute";

    /// <summary>The fully qualified metadata name of the framework's obsolete attribute.</summary>
    public const string ObsoleteAttributeFullMetadataName = "System.ObsoleteAttribute";

    /// <summary>The name of the attribute's message parameter, as written at a named-argument call site.</summary>
    private const string MessageParameterName = "message";

    /// <summary>The <c>System</c> namespace name.</summary>
    private const string SystemNamespaceName = "System";

    /// <summary>Returns whether an attribute's written name is the obsolete attribute's.</summary>
    /// <param name="name">The attribute name as written.</param>
    /// <returns><see langword="true"/> for <c>Obsolete</c> and <c>ObsoleteAttribute</c>, qualified or not.</returns>
    public static bool IsObsoleteName(NameSyntax name) => GetSimpleName(name) is "Obsolete" or ObsoleteAttributeMetadataName;

    /// <summary>Returns whether the attribute binds to the framework's obsolete attribute.</summary>
    /// <param name="semanticModel">The semantic model for the attribute's tree.</param>
    /// <param name="attribute">The obsolete attribute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> for <c>System.ObsoleteAttribute</c>.</returns>
    public static bool IsFrameworkObsoleteAttribute(SemanticModel semanticModel, AttributeSyntax attribute, CancellationToken cancellationToken)
    {
        var type = semanticModel.GetSymbolInfo(attribute, cancellationToken).Symbol?.ContainingType
            ?? semanticModel.GetTypeInfo(attribute, cancellationToken).Type as INamedTypeSymbol;
        return type is
        {
            Name: ObsoleteAttributeMetadataName,
            ContainingNamespace: { Name: SystemNamespaceName, ContainingNamespace.IsGlobalNamespace: true },
        };
    }

    /// <summary>Returns whether the attribute leaves the reader without a message.</summary>
    /// <param name="semanticModel">The semantic model for the attribute's tree.</param>
    /// <param name="attribute">The obsolete attribute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when no message is supplied, or the one supplied says nothing.</returns>
    public static bool HasNoUsableMessage(SemanticModel semanticModel, AttributeSyntax attribute, CancellationToken cancellationToken)
    {
        if (FindMessageArgument(attribute) is not { } message)
        {
            return true;
        }

        if (message is LiteralExpressionSyntax literal)
        {
            return literal.IsKind(SyntaxKind.NullLiteralExpression) || IsBlank(literal.Token.ValueText);
        }

        // A message built from a constant is still a message; only a constant that turns out to be
        // blank — or null — is treated as absent, and a non-constant expression is left alone entirely.
        var constant = semanticModel.GetConstantValue(message, cancellationToken);
        return constant.HasValue && (constant.Value is null || (constant.Value is string text && IsBlank(text)));
    }

    /// <summary>Returns whether the attribute sets a property by name.</summary>
    /// <param name="attribute">The obsolete attribute.</param>
    /// <param name="propertyName">The property name to look for.</param>
    /// <returns><see langword="true"/> when a <c>Name = value</c> initializer sets the property.</returns>
    public static bool HasPropertyInitializer(AttributeSyntax attribute, string propertyName)
    {
        if (attribute.ArgumentList is not { } argumentList)
        {
            return false;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameEquals is { } nameEquals
                && string.Equals(nameEquals.Name.Identifier.ValueText, propertyName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the name of the declaration the attribute is written on.</summary>
    /// <param name="declaration">The declaration owning the attribute list.</param>
    /// <returns>The declaration's name, or an empty string when it has none to report.</returns>
    public static string GetAnnotatedName(SyntaxNode? declaration) => declaration switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        BaseFieldDeclarationSyntax field => GetFirstVariableName(field),
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        EventDeclarationSyntax @event => @event.Identifier.ValueText,
        _ => GetOtherAnnotatedName(declaration),
    };

    /// <summary>Finds the argument that supplies the attribute's message.</summary>
    /// <param name="attribute">The obsolete attribute.</param>
    /// <returns>The message expression, or <see langword="null"/> when no argument supplies one.</returns>
    /// <remarks>
    /// The message is the attribute's first positional argument, or the one named <c>message</c>. A
    /// property initializer (<c>DiagnosticId = "…"</c>, <c>UrlFormat = "…"</c>) never is one, and neither
    /// is the <c>error</c> flag, so <c>[Obsolete(error: true)]</c> supplies nothing.
    /// </remarks>
    private static ExpressionSyntax? FindMessageArgument(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is not { } argumentList)
        {
            return null;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (argument.NameEquals is not null)
            {
                continue;
            }

            if (argument.NameColon is not { } nameColon)
            {
                return argument.Expression;
            }

            if (string.Equals(nameColon.Name.Identifier.ValueText, MessageParameterName, StringComparison.Ordinal))
            {
                return argument.Expression;
            }
        }

        return null;
    }

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased name.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>Returns whether a message says anything at all.</summary>
    /// <param name="text">The message text.</param>
    /// <returns><see langword="true"/> when the text is empty or only whitespace.</returns>
    private static bool IsBlank(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the name of the less common declarations the attribute can be written on.</summary>
    /// <param name="declaration">The declaration owning the attribute list.</param>
    /// <returns>The declaration's name, or an empty string when it has none to report.</returns>
    private static string GetOtherAnnotatedName(SyntaxNode? declaration) => declaration switch
    {
        IndexerDeclarationSyntax indexer => indexer.ThisKeyword.ValueText,
        EnumMemberDeclarationSyntax member => member.Identifier.ValueText,
        LocalFunctionStatementSyntax localFunction => localFunction.Identifier.ValueText,
        OperatorDeclarationSyntax @operator => @operator.OperatorToken.ValueText,
        ParameterSyntax parameter => parameter.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>Gets the first name a field or event-field declaration introduces.</summary>
    /// <param name="field">The field or event-field declaration.</param>
    /// <returns>The first declared name, or an empty string.</returns>
    private static string GetFirstVariableName(BaseFieldDeclarationSyntax field)
    {
        var variables = field.Declaration.Variables;
        return variables.Count > 0 ? variables[0].Identifier.ValueText : string.Empty;
    }
}
