// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a compile-time SQL connection configuration that weakens transport security (SES1107). Two kinds of
/// weakening are reported: bypassing server-certificate validation (<c>TrustServerCertificate=true</c>) and
/// disabling or de-mandating transport encryption (<c>Encrypt=false</c>, or <c>Encrypt=Optional</c> on the
/// newer client). The rule reports two local, high-precision shapes. First, a string <b>literal</b> connection
/// string carrying one of those keyword settings (parsed with a cheap case-insensitive scan) that is passed to a
/// <c>Microsoft.Data.SqlClient.SqlConnection</c> / <c>System.Data.SqlClient.SqlConnection</c> or
/// <c>SqlConnectionStringBuilder</c> constructor, or assigned to a <c>ConnectionString</c> property on those
/// types. Second, a <c>SqlConnectionStringBuilder</c> object-initializer member or property assignment setting
/// <c>TrustServerCertificate = true</c>, <c>Encrypt = false</c>, or
/// <c>Encrypt = SqlConnectionEncryptOption.Optional</c>. The rule is gated on a <c>SqlConnection</c> type
/// resolving in the compilation; a project without either SQL client registers nothing and pays nothing. Only a
/// local configuration is examined -- a connection string built from a variable, interpolation, or configuration
/// is deliberately not tracked -- so the rule stays fast and free of false positives.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1107WeakenedSqlTransportSecurityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The <c>ConnectionString</c> property whose literal assignment is inspected.</summary>
    private const string ConnectionStringMemberName = "ConnectionString";

    /// <summary>The builder member that governs certificate trust.</summary>
    private const string TrustServerCertificateMemberName = "TrustServerCertificate";

    /// <summary>The builder member that governs transport encryption.</summary>
    private const string EncryptMemberName = "Encrypt";

    /// <summary>The <c>SqlConnectionEncryptOption</c> value that de-mandates encryption.</summary>
    private const string OptionalMemberName = "Optional";

    /// <summary>The connection-string keyword that governs certificate trust.</summary>
    private const string TrustServerCertificateKeyword = "TrustServerCertificate";

    /// <summary>The connection-string keyword that governs transport encryption.</summary>
    private const string EncryptKeyword = "Encrypt";

    /// <summary>The metadata name of the modern SQL connection type.</summary>
    private const string MicrosoftConnectionMetadataName = "Microsoft.Data.SqlClient.SqlConnection";

    /// <summary>The metadata name of the legacy SQL connection type.</summary>
    private const string SystemConnectionMetadataName = "System.Data.SqlClient.SqlConnection";

    /// <summary>The metadata name of the modern connection-string builder.</summary>
    private const string MicrosoftBuilderMetadataName = "Microsoft.Data.SqlClient.SqlConnectionStringBuilder";

    /// <summary>The metadata name of the legacy connection-string builder.</summary>
    private const string SystemBuilderMetadataName = "System.Data.SqlClient.SqlConnectionStringBuilder";

    /// <summary>The metadata name of the modern-client encryption option type.</summary>
    private const string EncryptOptionMetadataName = "Microsoft.Data.SqlClient.SqlConnectionEncryptOption";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.WeakenedSqlTransportSecurity);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var types = GetSqlTypes(start.Compilation);
            if (types is not { } sqlTypes)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeObjectCreation(nodeContext, sqlTypes),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, sqlTypes), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1107 for a weakening literal connection string passed to a SQL constructor.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The gated SQL types resolved for the compilation.</param>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, SqlTransportTypes types)
    {
        // Syntactic prefilter: an object creation carrying a string-literal argument whose text names a
        // weakening keyword. No semantic model is touched until this cheap scan matches.
        if (GetConstructorArguments(context.Node) is not { } arguments
            || GetWeakeningLiteral(arguments, out var setting) is not { } literal)
        {
            return;
        }

        // Semantic confirmation: the created type is a gated SqlConnection or SqlConnectionStringBuilder.
        if (context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken).Type is not INamedTypeSymbol createdType
            || !IsConnectionOrBuilderType(createdType, types))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.WeakenedSqlTransportSecurity, literal.SyntaxTree, literal.Span, setting));
    }

    /// <summary>Reports SES1107 for a weakening connection-string literal assignment or builder member assignment.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The gated SQL types resolved for the compilation.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, SqlTransportTypes types)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        var memberName = GetAssignedMemberName(assignment.Left);
        if (memberName is ConnectionStringMemberName)
        {
            AnalyzeConnectionStringAssignment(context, assignment, types);
        }
        else if (memberName is TrustServerCertificateMemberName or EncryptMemberName)
        {
            AnalyzeBuilderMemberAssignment(context, assignment, memberName, types);
        }
    }

    /// <summary>Reports SES1107 for a <c>ConnectionString = "…"</c> literal that carries a weakening setting.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="assignment">The assignment expression.</param>
    /// <param name="types">The gated SQL types resolved for the compilation.</param>
    private static void AnalyzeConnectionStringAssignment(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignment, SqlTransportTypes types)
    {
        // Syntactic prefilter: a string literal whose text carries a weakening keyword setting.
        if (GetWeakeningStringLiteral(assignment.Right, out var setting) is not { } literal)
        {
            return;
        }

        // Semantic confirmation: the assigned instance is a gated SqlConnection or SqlConnectionStringBuilder.
        if (GetAssignedInstanceType(context.SemanticModel, assignment, context.CancellationToken) is not { } instanceType
            || !IsConnectionOrBuilderType(instanceType, types))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.WeakenedSqlTransportSecurity, literal.SyntaxTree, literal.Span, setting));
    }

    /// <summary>Reports SES1107 for a builder <c>TrustServerCertificate</c>/<c>Encrypt</c> member set to a weakening value.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="assignment">The assignment expression.</param>
    /// <param name="memberName">The assigned member name.</param>
    /// <param name="types">The gated SQL types resolved for the compilation.</param>
    private static void AnalyzeBuilderMemberAssignment(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignment, string memberName, SqlTransportTypes types)
    {
        // Semantic confirmation: the assigned instance is a gated SqlConnectionStringBuilder.
        if (GetAssignedInstanceType(context.SemanticModel, assignment, context.CancellationToken) is not { } instanceType
            || !IsBuilderType(instanceType, types)
            || !IsWeakeningMemberValue(context.SemanticModel, memberName, assignment.Right, types, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.WeakenedSqlTransportSecurity,
            assignment.SyntaxTree,
            assignment.Span,
            memberName + " = " + assignment.Right));
    }

    /// <summary>Returns whether a builder member is being set to a value that weakens transport security.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="memberName">The assigned member name.</param>
    /// <param name="value">The assigned value expression.</param>
    /// <param name="types">The gated SQL types resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the value weakens transport security.</returns>
    private static bool IsWeakeningMemberValue(SemanticModel model, string memberName, ExpressionSyntax value, SqlTransportTypes types, CancellationToken cancellationToken)
    {
        var constant = model.GetConstantValue(value, cancellationToken);
        if (memberName is TrustServerCertificateMemberName)
        {
            // 'TrustServerCertificate = true' bypasses validation; a non-constant value is left alone to
            // avoid a false positive on 'TrustServerCertificate = isDevelopment'.
            return constant is { HasValue: true, Value: true };
        }

        // Encrypt: 'false' turns TLS off (both clients), and 'SqlConnectionEncryptOption.Optional' de-mandates it
        // on the modern client. A non-constant or 'Mandatory'/'Strict' value is treated as secure.
        return constant is { HasValue: true, Value: false } || IsEncryptOptionalReference(model, value, types, cancellationToken);
    }

    /// <summary>Returns whether an expression binds to the modern client's <c>SqlConnectionEncryptOption.Optional</c> value.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="value">The assigned value expression.</param>
    /// <param name="types">The gated SQL types resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the value is <c>SqlConnectionEncryptOption.Optional</c>.</returns>
    private static bool IsEncryptOptionalReference(SemanticModel model, ExpressionSyntax value, SqlTransportTypes types, CancellationToken cancellationToken)
    {
        if (types.EncryptOption is not { } encryptOption
            || GetTrailingName(value) is not OptionalMemberName)
        {
            return false;
        }

        var symbol = model.GetSymbolInfo(value, cancellationToken).Symbol;
        return symbol is { Name: OptionalMemberName } && SymbolEqualityComparer.Default.Equals(symbol.ContainingType, encryptOption);
    }

    /// <summary>Returns the argument list of an explicit or implicit object creation, if it has one.</summary>
    /// <param name="node">The object-creation node.</param>
    /// <returns>The argument list, or <see langword="null"/> when the creation has no arguments.</returns>
    private static ArgumentListSyntax? GetConstructorArguments(SyntaxNode node)
        => node switch
        {
            ObjectCreationExpressionSyntax { ArgumentList: { } arguments } => arguments,
            ImplicitObjectCreationExpressionSyntax { ArgumentList: { } arguments } => arguments,
            _ => null,
        };

    /// <summary>Returns the connection-string literal argument that carries a weakening setting, honouring a named argument.</summary>
    /// <param name="argumentList">The constructor argument list.</param>
    /// <param name="setting">When matched, the offending <c>keyword=value</c> setting text.</param>
    /// <returns>The weakening literal, or <see langword="null"/> when no argument matches.</returns>
    private static LiteralExpressionSyntax? GetWeakeningLiteral(ArgumentListSyntax argumentList, out string setting)
    {
        setting = string.Empty;
        var arguments = argumentList.Arguments;
        if (arguments.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { Name.Identifier.ValueText: "connectionString" })
            {
                return GetWeakeningStringLiteral(arguments[i].Expression, out setting);
            }
        }

        // The connection string is the first parameter of every guarded constructor, so a leading positional
        // argument is it.
        return arguments[0].NameColon is null ? GetWeakeningStringLiteral(arguments[0].Expression, out setting) : null;
    }

    /// <summary>Returns the string literal when its text carries a weakening connection-string setting.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="setting">When matched, the offending <c>keyword=value</c> setting text.</param>
    /// <returns>The weakening string literal, or <see langword="null"/> when it does not match.</returns>
    private static LiteralExpressionSyntax? GetWeakeningStringLiteral(ExpressionSyntax expression, out string setting)
    {
        setting = string.Empty;
        if (expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return null;
        }

        return TryFindWeakeningSetting(literal.Token.ValueText, out setting) ? literal : null;
    }

    /// <summary>Scans a connection-string text for the first setting that weakens transport security.</summary>
    /// <param name="text">The decoded connection-string text.</param>
    /// <param name="setting">When matched, the offending trimmed <c>keyword=value</c> setting text.</param>
    /// <returns><see langword="true"/> when a weakening setting is present.</returns>
    private static bool TryFindWeakeningSetting(string text, out string setting)
    {
        setting = string.Empty;

        // A connection string is a ';'-separated list of 'keyword=value' pairs. Without a '=' there is
        // nothing to inspect, so the clean path exits after one cheap scan and never allocates.
        if (text.IndexOf('=') < 0)
        {
            return false;
        }

        var index = 0;
        while (index < text.Length)
        {
            var segmentEnd = text.IndexOf(';', index);
            if (segmentEnd < 0)
            {
                segmentEnd = text.Length;
            }

            if (TryMatchPair(text, index, segmentEnd, out setting))
            {
                return true;
            }

            index = segmentEnd + 1;
        }

        return false;
    }

    /// <summary>Matches one <c>keyword=value</c> pair against the weakening settings.</summary>
    /// <param name="text">The decoded connection-string text.</param>
    /// <param name="start">The inclusive start of the pair segment.</param>
    /// <param name="end">The exclusive end of the pair segment.</param>
    /// <param name="setting">When matched, the offending trimmed <c>keyword=value</c> setting text.</param>
    /// <returns><see langword="true"/> when the pair is a weakening setting.</returns>
    private static bool TryMatchPair(string text, int start, int end, out string setting)
    {
        setting = string.Empty;
        var equals = IndexOf(text, '=', start, end);
        if (equals < 0)
        {
            return false;
        }

        var keyStart = start;
        var keyEnd = equals;
        Trim(text, ref keyStart, ref keyEnd);
        var valueStart = equals + 1;
        var valueEnd = end;
        Trim(text, ref valueStart, ref valueEnd);

        var isWeakening =
            (RegionEqualsIgnoreCase(text, keyStart, keyEnd, TrustServerCertificateKeyword) && IsTruthyValue(text, valueStart, valueEnd))
            || (RegionEqualsIgnoreCase(text, keyStart, keyEnd, EncryptKeyword) && IsInsecureEncryptValue(text, valueStart, valueEnd));
        if (!isWeakening)
        {
            return false;
        }

        setting = BuildSetting(text, keyStart, keyEnd, valueStart, valueEnd);
        return true;
    }

    /// <summary>Returns whether a value span is a truthy boolean (<c>true</c> or <c>yes</c>).</summary>
    /// <param name="text">The decoded connection-string text.</param>
    /// <param name="start">The inclusive start of the value span.</param>
    /// <param name="end">The exclusive end of the value span.</param>
    /// <returns><see langword="true"/> for a truthy value.</returns>
    private static bool IsTruthyValue(string text, int start, int end)
        => RegionEqualsIgnoreCase(text, start, end, "true") || RegionEqualsIgnoreCase(text, start, end, "yes");

    /// <summary>Returns whether an <c>Encrypt</c> value span disables or de-mandates encryption.</summary>
    /// <param name="text">The decoded connection-string text.</param>
    /// <param name="start">The inclusive start of the value span.</param>
    /// <param name="end">The exclusive end of the value span.</param>
    /// <returns><see langword="true"/> for <c>false</c>, <c>no</c>, or <c>optional</c>.</returns>
    private static bool IsInsecureEncryptValue(string text, int start, int end)
        => RegionEqualsIgnoreCase(text, start, end, "false")
            || RegionEqualsIgnoreCase(text, start, end, "no")
            || RegionEqualsIgnoreCase(text, start, end, "optional");

    /// <summary>Builds the trimmed <c>keyword=value</c> label for a matched pair.</summary>
    /// <param name="text">The decoded connection-string text.</param>
    /// <param name="keyStart">The inclusive start of the trimmed key.</param>
    /// <param name="keyEnd">The exclusive end of the trimmed key.</param>
    /// <param name="valueStart">The inclusive start of the trimmed value.</param>
    /// <param name="valueEnd">The exclusive end of the trimmed value.</param>
    /// <returns>The <c>keyword=value</c> label.</returns>
    private static string BuildSetting(string text, int keyStart, int keyEnd, int valueStart, int valueEnd)
        => text.Substring(keyStart, keyEnd - keyStart) + "=" + text.Substring(valueStart, valueEnd - valueStart);

    /// <summary>Returns the index of a character within a half-open range, or <c>-1</c>.</summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="value">The character to find.</param>
    /// <param name="start">The inclusive start of the range.</param>
    /// <param name="end">The exclusive end of the range.</param>
    /// <returns>The index of the character, or <c>-1</c> when absent.</returns>
    private static int IndexOf(string text, char value, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (text[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Advances a span's bounds past leading and trailing ASCII whitespace.</summary>
    /// <param name="text">The text the span indexes into.</param>
    /// <param name="start">The inclusive start, advanced past leading whitespace.</param>
    /// <param name="end">The exclusive end, retreated past trailing whitespace.</param>
    private static void Trim(string text, ref int start, ref int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }
    }

    /// <summary>Returns whether a text span equals a word, comparing ASCII case-insensitively.</summary>
    /// <param name="text">The text the span indexes into.</param>
    /// <param name="start">The inclusive start of the span.</param>
    /// <param name="end">The exclusive end of the span.</param>
    /// <param name="word">The word to compare against.</param>
    /// <returns><see langword="true"/> when the span equals the word.</returns>
    private static bool RegionEqualsIgnoreCase(string text, int start, int end, string word)
    {
        if (end - start != word.Length)
        {
            return false;
        }

        for (var i = 0; i < word.Length; i++)
        {
            if (ToLowerAscii(text[start + i]) != ToLowerAscii(word[i]))
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

    /// <summary>Returns the assigned member name of an assignment's left side (member access or initializer identifier).</summary>
    /// <param name="left">The assignment's left-hand side.</param>
    /// <returns>The member name, or <see langword="null"/> when the target is not a simple member reference.</returns>
    private static string? GetAssignedMemberName(ExpressionSyntax left)
        => left switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns the trailing simple name of a member access or identifier expression.</summary>
    /// <param name="expression">The value expression to read.</param>
    /// <returns>The trailing name, or <see langword="null"/> when it cannot be read syntactically.</returns>
    private static string? GetTrailingName(ExpressionSyntax expression)
        => expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns the type of the instance targeted by an assignment (a member receiver or an initializer's object).</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="assignment">The assignment expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The instance type, or <see langword="null"/> when it cannot be determined.</returns>
    private static INamedTypeSymbol? GetAssignedInstanceType(SemanticModel model, AssignmentExpressionSyntax assignment, CancellationToken cancellationToken)
    {
        // A member target ('instance.X = value') resolves through its receiver; an initializer member
        // ('X = value' inside 'new T { ... }') has a bare identifier target, so the instance is the
        // enclosing object creation.
        return assignment.Left switch
        {
            MemberAccessExpressionSyntax memberAccess => model.GetTypeInfo(memberAccess.Expression, cancellationToken).Type as INamedTypeSymbol,
            _ when assignment.Parent is InitializerExpressionSyntax { Parent: { } creation } => model.GetTypeInfo(creation, cancellationToken).Type as INamedTypeSymbol,
            _ => null,
        };
    }

    /// <summary>Returns whether a type is one of the gated SQL connection or connection-string builder types.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="types">The gated SQL types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the type is a gated connection or builder.</returns>
    private static bool IsConnectionOrBuilderType(INamedTypeSymbol type, SqlTransportTypes types)
        => IsConnectionType(type, types) || IsBuilderType(type, types);

    /// <summary>Returns whether a type is one of the gated SQL connection types.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="types">The gated SQL types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the type is a gated connection.</returns>
    private static bool IsConnectionType(INamedTypeSymbol type, SqlTransportTypes types)
        => Matches(type, types.MicrosoftConnection) || Matches(type, types.SystemConnection);

    /// <summary>Returns whether a type is one of the gated SQL connection-string builder types.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="types">The gated SQL types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the type is a gated builder.</returns>
    private static bool IsBuilderType(INamedTypeSymbol type, SqlTransportTypes types)
        => Matches(type, types.MicrosoftBuilder) || Matches(type, types.SystemBuilder);

    /// <summary>Returns whether a candidate type equals a resolved gated type.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="gated">The resolved gated type, if any.</param>
    /// <returns><see langword="true"/> when the candidate equals the gated type.</returns>
    private static bool Matches(INamedTypeSymbol type, INamedTypeSymbol? gated)
        => gated is not null && SymbolEqualityComparer.Default.Equals(type, gated);

    /// <summary>Resolves the SQL client types the rule gates on.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>The resolved types, or <see langword="null"/> when neither SQL connection type is present.</returns>
    private static SqlTransportTypes? GetSqlTypes(Compilation compilation)
    {
        var microsoftConnection = compilation.GetTypeByMetadataName(MicrosoftConnectionMetadataName);
        var systemConnection = compilation.GetTypeByMetadataName(SystemConnectionMetadataName);
        if (microsoftConnection is null && systemConnection is null)
        {
            return null;
        }

        return new SqlTransportTypes(
            microsoftConnection,
            systemConnection,
            compilation.GetTypeByMetadataName(MicrosoftBuilderMetadataName),
            compilation.GetTypeByMetadataName(SystemBuilderMetadataName),
            compilation.GetTypeByMetadataName(EncryptOptionMetadataName));
    }

    /// <summary>The SQL client types resolved once per compilation for SES1107.</summary>
    /// <param name="MicrosoftConnection">The resolved <c>Microsoft.Data.SqlClient.SqlConnection</c>, or <see langword="null"/>.</param>
    /// <param name="SystemConnection">The resolved <c>System.Data.SqlClient.SqlConnection</c>, or <see langword="null"/>.</param>
    /// <param name="MicrosoftBuilder">The resolved <c>Microsoft.Data.SqlClient.SqlConnectionStringBuilder</c>, or <see langword="null"/>.</param>
    /// <param name="SystemBuilder">The resolved <c>System.Data.SqlClient.SqlConnectionStringBuilder</c>, or <see langword="null"/>.</param>
    /// <param name="EncryptOption">The resolved <c>Microsoft.Data.SqlClient.SqlConnectionEncryptOption</c>, or <see langword="null"/>.</param>
    private readonly record struct SqlTransportTypes(
        INamedTypeSymbol? MicrosoftConnection,
        INamedTypeSymbol? SystemConnection,
        INamedTypeSymbol? MicrosoftBuilder,
        INamedTypeSymbol? SystemBuilder,
        INamedTypeSymbol? EncryptOption);
}
