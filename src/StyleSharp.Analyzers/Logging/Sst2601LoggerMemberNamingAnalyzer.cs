// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>ILogger</c>/<c>ILogger&lt;T&gt;</c> field or property whose name breaks the logger naming
/// convention (SST2601), so loggers read consistently across a codebase. A private instance logger is expected
/// to be named <c>_logger</c> (or <c>_log</c>); any non-private or static logger is expected to be named
/// <c>Logger</c>. The accepted private-instance names are configurable with <c>stylesharp.SST2601.fieldname</c>.
/// </summary>
/// <remarks>
/// <para>
/// The whole rule is gated at compilation start on <c>Microsoft.Extensions.Logging.ILogger</c> resolving; a
/// project that references no such abstraction registers no per-node work and pays nothing. The generic
/// <c>ILogger&lt;T&gt;</c> is recognised through the non-generic interface it derives from, so no second type
/// needs resolving.
/// </para>
/// <para>
/// The clean path is syntax first: a field or property is skipped unless the simple name of its declared type
/// is <c>ILogger</c>, so nothing binds for the overwhelming majority of members. Only a name match binds the
/// member to confirm the type really is a logger. A field's accessibility and static-ness are read from its
/// modifiers; a property's are read from its symbol, so an interface property (public by default) is judged
/// correctly.
/// </para>
/// <para>
/// A field with several declarators is checked declarator by declarator, so only the mis-named ones are
/// reported. An explicit-interface property is left alone: its name is fixed by the interface it implements and
/// cannot be renamed independently. There is no code fix — a rename ripples across every reference and is a
/// judgement the author should make.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2601LoggerMemberNamingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the logger abstraction the rule gates on.</summary>
    private const string LoggerTypeMetadataName = "Microsoft.Extensions.Logging.ILogger";

    /// <summary>The simple type name a candidate member's declared type must carry.</summary>
    private const string LoggerTypeSimpleName = "ILogger";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArrays.Of(LoggingRules.LoggerMemberNaming);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Gates the rule on the logger abstraction being available, then analyzes each member.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(LoggerTypeMetadataName) is not { } loggerType)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, loggerType),
            SyntaxKind.FieldDeclaration,
            SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Dispatches a field or property declaration to the matching check.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="loggerType">The resolved logger type.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol loggerType)
    {
        if (context.Node is FieldDeclarationSyntax field)
        {
            AnalyzeField(context, field, loggerType);
        }
        else if (context.Node is PropertyDeclarationSyntax property)
        {
            AnalyzeProperty(context, property, loggerType);
        }
    }

    /// <summary>Checks each declarator of a logger-typed field against the convention for its modifiers.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="field">The field declaration.</param>
    /// <param name="loggerType">The resolved logger type.</param>
    private static void AnalyzeField(SyntaxNodeAnalysisContext context, FieldDeclarationSyntax field, INamedTypeSymbol loggerType)
    {
        if (!IsLoggerTypeName(field.Declaration.Type))
        {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(field.Declaration.Type, context.CancellationToken).Type;
        if (!IsLoggerType(type, loggerType))
        {
            return;
        }

        var isPrivateInstance = IsPrivateInstance(field.Modifiers);
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(field.SyntaxTree);
        foreach (var declarator in field.Declaration.Variables)
        {
            Evaluate(context, isPrivateInstance, declarator.Identifier, options);
        }
    }

    /// <summary>Checks a logger-typed property against the convention for its accessibility and static-ness.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="property">The property declaration.</param>
    /// <param name="loggerType">The resolved logger type.</param>
    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax property, INamedTypeSymbol loggerType)
    {
        if (property.ExplicitInterfaceSpecifier is not null || !IsLoggerTypeName(property.Type))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken) is not { } symbol
            || !IsLoggerType(symbol.Type, loggerType))
        {
            return;
        }

        var isPrivateInstance = symbol.DeclaredAccessibility == Accessibility.Private && !symbol.IsStatic;
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(property.SyntaxTree);
        Evaluate(context, isPrivateInstance, property.Identifier, options);
    }

    /// <summary>Reports a member whose name breaks the convention that applies to it.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="isPrivateInstance">Whether the member is a private instance member.</param>
    /// <param name="identifier">The member's identifier token, where the diagnostic is reported.</param>
    /// <param name="options">The analyzer config options for the tree.</param>
    private static void Evaluate(
        SyntaxNodeAnalysisContext context,
        bool isPrivateInstance,
        SyntaxToken identifier,
        AnalyzerConfigOptions options)
    {
        var name = identifier.ValueText;
        if (isPrivateInstance)
        {
            if (LoggerMemberNamingOptions.IsAcceptedInstanceName(options, name))
            {
                return;
            }

            Report(context, identifier, name, LoggerMemberNamingOptions.PreferredInstanceName(options));
            return;
        }

        if (string.Equals(name, LoggerMemberNamingOptions.PublicName, StringComparison.Ordinal))
        {
            return;
        }

        Report(context, identifier, name, LoggerMemberNamingOptions.PublicName);
    }

    /// <summary>Reports the naming diagnostic on a member identifier.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="identifier">The member's identifier token.</param>
    /// <param name="name">The member's current name.</param>
    /// <param name="expected">The expected name for the member's convention.</param>
    private static void Report(SyntaxNodeAnalysisContext context, SyntaxToken identifier, string name, string expected)
        => context.ReportDiagnostic(DiagnosticHelper.Create(
            LoggingRules.LoggerMemberNaming,
            identifier.GetLocation(),
            name,
            expected));

    /// <summary>Returns whether a field's modifiers describe a private, non-static field.</summary>
    /// <param name="modifiers">The field's modifiers.</param>
    /// <returns><see langword="true"/> when no non-private accessibility and no <c>static</c>/<c>const</c> is present.</returns>
    private static bool IsPrivateInstance(SyntaxTokenList modifiers)
    {
        var isPrivate = true;
        var isStatic = false;
        foreach (var modifier in modifiers)
        {
            var kind = modifier.Kind();
            if (kind is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword or SyntaxKind.ProtectedKeyword)
            {
                isPrivate = false;
            }
            else if (kind is SyntaxKind.StaticKeyword or SyntaxKind.ConstKeyword)
            {
                isStatic = true;
            }
        }

        return isPrivate && !isStatic;
    }

    /// <summary>Returns whether the simple name of a declared type is <c>ILogger</c>.</summary>
    /// <param name="type">The declared type syntax.</param>
    /// <returns><see langword="true"/> when the rightmost simple name is <c>ILogger</c>.</returns>
    private static bool IsLoggerTypeName(TypeSyntax type)
    {
        var current = type;
        while (true)
        {
            switch (current)
            {
                case NullableTypeSyntax nullable:
                {
                    current = nullable.ElementType;
                    continue;
                }

                case IdentifierNameSyntax identifier:
                    return identifier.Identifier.ValueText == LoggerTypeSimpleName;
                case GenericNameSyntax generic:
                    return generic.Identifier.ValueText == LoggerTypeSimpleName;
                case QualifiedNameSyntax qualified:
                {
                    current = qualified.Right;
                    continue;
                }

                case AliasQualifiedNameSyntax alias:
                {
                    current = alias.Name;
                    continue;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>Returns whether a type is the logger abstraction or a constructed logger derived from it.</summary>
    /// <param name="type">The candidate type symbol.</param>
    /// <param name="loggerType">The resolved logger type.</param>
    /// <returns><see langword="true"/> when the type is <c>ILogger</c> or an <c>ILogger&lt;T&gt;</c> that derives from it.</returns>
    private static bool IsLoggerType(ITypeSymbol? type, INamedTypeSymbol loggerType)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(named, loggerType))
        {
            return true;
        }

        if (!named.IsGenericType)
        {
            return false;
        }

        foreach (var contract in named.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(contract, loggerType))
            {
                return true;
            }
        }

        return false;
    }
}
