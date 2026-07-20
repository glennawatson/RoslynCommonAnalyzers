// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a <c>System.DirectoryServices.DirectoryEntry</c> construction that binds to the directory without
/// authenticating (SES1310). Two explicit, local shapes are reported. First, an anonymous authentication type:
/// <c>AuthenticationTypes.Anonymous</c> passed as the <c>authenticationType</c> constructor argument or set on
/// the <c>AuthenticationType</c> object-initializer member. Second, an empty-credential bind: a
/// <c>new DirectoryEntry("LDAP://...", username, password)</c> whose <c>path</c> is an <c>LDAP://</c> string
/// literal and whose <c>username</c> and <c>password</c> are both empty-string or <see langword="null"/>
/// literals. Both anchor on the object-creation node, so no data-flow or interprocedural tracking is performed.
/// The clean path is syntactic: a creation is ignored unless its type name is <c>DirectoryEntry</c> (or, for a
/// target-typed <c>new(...)</c>, unless a candidate anonymous/empty-credential shape appears), and the type is
/// bound and the <c>Anonymous</c> field confirmed only after that screen passes. The rule is gated on
/// <c>DirectoryEntry</c> and <c>AuthenticationTypes</c> resolving in the compilation; a project without
/// <c>System.DirectoryServices</c> registers nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1310AnonymousLdapBindAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The simple type name of the guarded directory-bind sink.</summary>
    private const string DirectoryEntryTypeName = "DirectoryEntry";

    /// <summary>The <c>AuthenticationType</c> object-initializer member whose anonymous value is reported.</summary>
    private const string AuthenticationTypeMemberName = "AuthenticationType";

    /// <summary>The <c>AuthenticationTypes.Anonymous</c> enum member name that marks an anonymous bind.</summary>
    private const string AnonymousMemberName = "Anonymous";

    /// <summary>The constructor parameter name carrying the directory path.</summary>
    private const string PathParameterName = "path";

    /// <summary>The constructor parameter name carrying the bind user name.</summary>
    private const string UsernameParameterName = "username";

    /// <summary>The constructor parameter name carrying the bind password.</summary>
    private const string PasswordParameterName = "password";

    /// <summary>The URI scheme prefix that marks an LDAP directory path.</summary>
    private const string LdapPathPrefix = "LDAP:";

    /// <summary>The positional index of the constructor <c>path</c> argument.</summary>
    private const int PathArgumentPosition = 0;

    /// <summary>The positional index of the constructor <c>username</c> argument.</summary>
    private const int UsernameArgumentPosition = 1;

    /// <summary>The positional index of the constructor <c>password</c> argument.</summary>
    private const int PasswordArgumentPosition = 2;

    /// <summary>The message detail describing an anonymous authentication type.</summary>
    private const string AnonymousTypeDetail = "its authentication type is 'AuthenticationTypes.Anonymous'";

    /// <summary>The message detail describing an empty-credential bind.</summary>
    private const string EmptyCredentialDetail = "its username and password are both empty";

    /// <summary>The metadata name of the guarded directory-bind sink.</summary>
    private const string DirectoryEntryMetadataName = "System.DirectoryServices.DirectoryEntry";

    /// <summary>The metadata name of the authentication-type enum.</summary>
    private const string AuthenticationTypesMetadataName = "System.DirectoryServices.AuthenticationTypes";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.AnonymousLdapBind);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var directoryEntry = start.Compilation.GetTypeByMetadataName(DirectoryEntryMetadataName);
            var authenticationTypes = start.Compilation.GetTypeByMetadataName(AuthenticationTypesMetadataName);
            if (directoryEntry is null || authenticationTypes is null)
            {
                return;
            }

            var types = new DirectoryBindTypes(directoryEntry, authenticationTypes);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeObjectCreation(nodeContext, types),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Reports SES1310 for a <c>DirectoryEntry</c> construction that binds anonymously.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The gated directory types resolved for the compilation.</param>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, DirectoryBindTypes types)
    {
        var (argumentList, initializer) = Decompose(context.Node, out var explicitTypeName);

        // For an explicit 'new DirectoryEntry(...)' the type name is the cheapest, most selective screen. A
        // target-typed 'new(...)' has no type name, so it falls through to the candidate-shape screen below.
        if (explicitTypeName is not null and not DirectoryEntryTypeName)
        {
            return;
        }

        // Syntactic candidate detection: does the construction name 'AuthenticationTypes.Anonymous', or bind an
        // LDAP path with empty credentials? Neither branch touches the semantic model.
        var anonymousValue = GetAnonymousAuthenticationExpression(argumentList, initializer);
        var hasEmptyCredentialBind = anonymousValue is null && HasEmptyCredentialLdapBind(argumentList);
        if (anonymousValue is null && !hasEmptyCredentialBind)
        {
            return;
        }

        // Semantic confirmation: the created type is the gated 'DirectoryEntry'.
        if (context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken).Type is not INamedTypeSymbol createdType
            || !SymbolEqualityComparer.Default.Equals(createdType, types.DirectoryEntry))
        {
            return;
        }

        // For the anonymous shape, bind the value and confirm it is 'AuthenticationTypes.Anonymous' rather than a
        // same-named member on an unrelated type.
        if (anonymousValue is not null)
        {
            if (IsAnonymousAuthentication(context.SemanticModel, anonymousValue, types, context.CancellationToken))
            {
                Report(context, AnonymousTypeDetail);
            }

            return;
        }

        Report(context, EmptyCredentialDetail);
    }

    /// <summary>Reports SES1310 on the object-creation node with the given message detail.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="detail">The message detail describing the offending shape.</param>
    private static void Report(SyntaxNodeAnalysisContext context, string detail)
        => context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.AnonymousLdapBind,
            context.Node.SyntaxTree,
            context.Node.Span,
            detail));

    /// <summary>Splits an object-creation node into its argument list and initializer, and reads any explicit type name.</summary>
    /// <param name="node">The explicit or implicit object-creation node.</param>
    /// <param name="explicitTypeName">The simple type name of an explicit creation, or <see langword="null"/> for a target-typed <c>new(...)</c>.</param>
    /// <returns>The argument list and object initializer, either of which may be <see langword="null"/>.</returns>
    private static (ArgumentListSyntax? ArgumentList, InitializerExpressionSyntax? Initializer) Decompose(SyntaxNode node, out string? explicitTypeName)
    {
        if (node is ObjectCreationExpressionSyntax creation)
        {
            explicitTypeName = GetSimpleTypeName(creation.Type);
            return (creation.ArgumentList, creation.Initializer);
        }

        explicitTypeName = null;
        var implicitCreation = (ImplicitObjectCreationExpressionSyntax)node;
        return (implicitCreation.ArgumentList, implicitCreation.Initializer);
    }

    /// <summary>Returns the expression that syntactically names <c>AuthenticationTypes.Anonymous</c> for this creation.</summary>
    /// <param name="argumentList">The constructor argument list, if any.</param>
    /// <param name="initializer">The object initializer, if any.</param>
    /// <returns>The candidate anonymous value expression, or <see langword="null"/> when none appears.</returns>
    private static ExpressionSyntax? GetAnonymousAuthenticationExpression(ArgumentListSyntax? argumentList, InitializerExpressionSyntax? initializer)
    {
        // 'new DirectoryEntry(...) { AuthenticationType = AuthenticationTypes.Anonymous }'.
        if (initializer is not null)
        {
            var expressions = initializer.Expressions;
            for (var i = 0; i < expressions.Count; i++)
            {
                if (expressions[i] is AssignmentExpressionSyntax { Left: { } left, Right: { } right }
                    && GetTrailingName(left) is AuthenticationTypeMemberName
                    && GetTrailingName(right) is AnonymousMemberName)
                {
                    return right;
                }
            }
        }

        // 'new DirectoryEntry(..., AuthenticationTypes.Anonymous)'. The only 'AuthenticationTypes' parameter of a
        // 'DirectoryEntry' constructor is 'authenticationType', so an argument naming 'Anonymous' is that value.
        if (argumentList is not null)
        {
            var arguments = argumentList.Arguments;
            for (var i = 0; i < arguments.Count; i++)
            {
                if (GetTrailingName(arguments[i].Expression) is AnonymousMemberName)
                {
                    return arguments[i].Expression;
                }
            }
        }

        return null;
    }

    /// <summary>Returns whether the constructor binds an <c>LDAP://</c> path with both credential arguments empty.</summary>
    /// <param name="argumentList">The constructor argument list, if any.</param>
    /// <returns><see langword="true"/> when the path is an LDAP literal and both username and password are empty or null.</returns>
    private static bool HasEmptyCredentialLdapBind(ArgumentListSyntax? argumentList)
    {
        if (argumentList is null)
        {
            return false;
        }

        ResolveCredentialArguments(argumentList, out var path, out var username, out var password);
        return path is not null
            && username is not null
            && password is not null
            && IsLdapLiteralPath(path)
            && IsEmptyOrNullLiteral(username)
            && IsEmptyOrNullLiteral(password);
    }

    /// <summary>Resolves the <c>path</c>, <c>username</c>, and <c>password</c> arguments by name or position.</summary>
    /// <param name="argumentList">The constructor argument list.</param>
    /// <param name="path">The resolved path argument, or <see langword="null"/>.</param>
    /// <param name="username">The resolved username argument, or <see langword="null"/>.</param>
    /// <param name="password">The resolved password argument, or <see langword="null"/>.</param>
    private static void ResolveCredentialArguments(ArgumentListSyntax argumentList, out ExpressionSyntax? path, out ExpressionSyntax? username, out ExpressionSyntax? password)
    {
        path = null;
        username = null;
        password = null;
        var arguments = argumentList.Arguments;
        var positional = 0;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (argument.NameColon is { Name.Identifier.ValueText: { } name })
            {
                if (name is PathParameterName)
                {
                    path = argument.Expression;
                }
                else if (name is UsernameParameterName)
                {
                    username = argument.Expression;
                }
                else if (name is PasswordParameterName)
                {
                    password = argument.Expression;
                }

                continue;
            }

            if (positional is PathArgumentPosition)
            {
                path = argument.Expression;
            }
            else if (positional is UsernameArgumentPosition)
            {
                username = argument.Expression;
            }
            else if (positional is PasswordArgumentPosition)
            {
                password = argument.Expression;
            }

            positional++;
        }
    }

    /// <summary>Returns whether the created type binds to <c>AuthenticationTypes.Anonymous</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="value">The candidate anonymous value expression.</param>
    /// <param name="types">The gated directory types resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the value is the gated enum's <c>Anonymous</c> member.</returns>
    private static bool IsAnonymousAuthentication(SemanticModel model, ExpressionSyntax value, DirectoryBindTypes types, CancellationToken cancellationToken)
        => model.GetSymbolInfo(value, cancellationToken).Symbol is IFieldSymbol { Name: AnonymousMemberName } field
            && SymbolEqualityComparer.Default.Equals(field.ContainingType, types.AuthenticationTypes);

    /// <summary>Returns whether an expression is an <c>LDAP://</c> string literal.</summary>
    /// <param name="expression">The candidate path expression.</param>
    /// <returns><see langword="true"/> when the expression is a string literal whose value begins with the LDAP scheme.</returns>
    private static bool IsLdapLiteralPath(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
            && StartsWithIgnoreCase(literal.Token.ValueText, LdapPathPrefix);

    /// <summary>Returns whether an expression is an empty-string literal or a <see langword="null"/> literal.</summary>
    /// <param name="expression">The candidate credential expression.</param>
    /// <returns><see langword="true"/> when the expression is <c>""</c> or <see langword="null"/>.</returns>
    private static bool IsEmptyOrNullLiteral(ExpressionSyntax expression)
        => expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NullLiteralExpression) => true,
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText.Length == 0,
            _ => false,
        };

    /// <summary>Returns whether a string starts with a prefix, comparing ASCII case-insensitively.</summary>
    /// <param name="text">The text to test.</param>
    /// <param name="prefix">The prefix to look for.</param>
    /// <returns><see langword="true"/> when the text starts with the prefix.</returns>
    private static bool StartsWithIgnoreCase(string text, string prefix)
    {
        if (text.Length < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (ToLowerAscii(text[i]) != ToLowerAscii(prefix[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Lower-cases an ASCII letter, leaving every other character untouched.</summary>
    /// <param name="c">The character to fold.</param>
    /// <returns>The lower-cased character.</returns>
    private static char ToLowerAscii(char c)
        => c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c;

    /// <summary>Returns the right-most simple identifier of a member access or bare identifier expression.</summary>
    /// <param name="expression">The expression to read.</param>
    /// <returns>The trailing simple name, or <see langword="null"/> when it cannot be read syntactically.</returns>
    private static string? GetTrailingName(ExpressionSyntax expression)
        => expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns the right-most simple identifier of a type name.</summary>
    /// <param name="type">The constructed type syntax.</param>
    /// <returns>The simple type name, or <see langword="null"/> when it cannot be read syntactically.</returns>
    private static string? GetSimpleTypeName(TypeSyntax type)
        => type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            _ => null,
        };

    /// <summary>The directory types resolved once per compilation for SES1310.</summary>
    /// <param name="DirectoryEntry">The resolved <c>System.DirectoryServices.DirectoryEntry</c> sink type.</param>
    /// <param name="AuthenticationTypes">The resolved <c>System.DirectoryServices.AuthenticationTypes</c> enum type.</param>
    private readonly record struct DirectoryBindTypes(
        INamedTypeSymbol DirectoryEntry,
        INamedTypeSymbol AuthenticationTypes);
}
