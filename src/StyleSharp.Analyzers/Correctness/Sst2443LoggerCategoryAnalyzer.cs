// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a typed logger whose category is a type other than the one that logs through it (SST2443): a
/// field, property, or parameter of type <c>ILogger&lt;T&gt;</c>, or a <c>CreateLogger&lt;T&gt;()</c> /
/// <c>CreateLogger(typeof(T))</c> call, where <c>T</c> is not the enclosing type. The category is what
/// per-namespace level filters and sink routes match on, so a mismatched one makes a type's own logging
/// configuration silently inert.
/// </summary>
/// <remarks>
/// The rule is gated on the generic logger type resolving in the compilation. A base type or implemented
/// interface logging on behalf of the type, a dedicated category marker (an empty type, or one whose name
/// ends in <c>Category</c> or <c>Logs</c>), the enclosing type being nested inside the category, and a generic
/// type naming its own constructed self are all deliberate and stay silent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2443LoggerCategoryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The property carrying the enclosing type's name for the code fix.</summary>
    internal const string EnclosingTypeKey = "EnclosingType";

    /// <summary>The metadata name of the generic logger type.</summary>
    private const string GenericLoggerMetadataName = "Microsoft.Extensions.Logging.ILogger`1";

    /// <summary>The identifier a typed-logger type is written with.</summary>
    private const string LoggerIdentifier = "ILogger";

    /// <summary>The identifier a category-producing factory call is written with.</summary>
    private const string CreateLoggerIdentifier = "CreateLogger";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.WrongLoggerCategory);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var genericLogger = start.Compilation.GetTypeByMetadataName(GenericLoggerMetadataName);
            if (genericLogger is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeType(nodeContext, GetFieldType(nodeContext.Node), genericLogger), SyntaxKind.FieldDeclaration);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeType(nodeContext, ((PropertyDeclarationSyntax)nodeContext.Node).Type, genericLogger), SyntaxKind.PropertyDeclaration);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeType(nodeContext, ((ParameterSyntax)nodeContext.Node).Type, genericLogger), SyntaxKind.Parameter);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeCreateLogger(nodeContext, genericLogger), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Analyzes a written <c>ILogger&lt;T&gt;</c> type for a mismatched category.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="type">The declared type syntax, when present.</param>
    /// <param name="genericLogger">The generic logger type.</param>
    private static void AnalyzeType(SyntaxNodeAnalysisContext context, TypeSyntax? type, INamedTypeSymbol genericLogger)
    {
        if (type is not GenericNameSyntax { Identifier.ValueText: LoggerIdentifier } generic
            || generic.TypeArgumentList.Arguments.Count != 1)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(generic, context.CancellationToken).Symbol is not INamedTypeSymbol { TypeArguments: [{ } category] } constructed
            || !SymbolEqualityComparer.Default.Equals(constructed.OriginalDefinition, genericLogger))
        {
            return;
        }

        Report(context, generic.TypeArgumentList.Arguments[0], category);
    }

    /// <summary>Analyzes a <c>CreateLogger&lt;T&gt;()</c> or <c>CreateLogger(typeof(T))</c> call for a mismatched category.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="genericLogger">The generic logger type.</param>
    private static void AnalyzeCreateLogger(SyntaxNodeAnalysisContext context, INamedTypeSymbol genericLogger)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: SimpleNameSyntax { Identifier.ValueText: CreateLoggerIdentifier } name })
        {
            return;
        }

        if (name is GenericNameSyntax { TypeArgumentList.Arguments: [{ } typeArgument] })
        {
            AnalyzeGenericCreateLogger(context, invocation, typeArgument, genericLogger);
            return;
        }

        AnalyzeTypeofCreateLogger(context, invocation, genericLogger);
    }

    /// <summary>Analyzes a <c>CreateLogger&lt;T&gt;()</c> call for a mismatched category.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="invocation">The factory call.</param>
    /// <param name="typeArgument">The category type argument, which the fix rewrites.</param>
    /// <param name="genericLogger">The generic logger type.</param>
    private static void AnalyzeGenericCreateLogger(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, TypeSyntax typeArgument, INamedTypeSymbol genericLogger)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { TypeArguments: [{ } category] } method
            || !ReturnsLogger(method.ReturnType, genericLogger))
        {
            return;
        }

        Report(context, typeArgument, category);
    }

    /// <summary>Analyzes a <c>CreateLogger(typeof(T))</c> call for a mismatched category.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="invocation">The factory call.</param>
    /// <param name="genericLogger">The generic logger type.</param>
    private static void AnalyzeTypeofCreateLogger(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, INamedTypeSymbol genericLogger)
    {
        if (invocation.ArgumentList.Arguments is not [{ Expression: TypeOfExpressionSyntax typeOf }]
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol createLogger
            || !ReturnsLogger(createLogger.ReturnType, genericLogger)
            || context.SemanticModel.GetTypeInfo(typeOf.Type, context.CancellationToken).Type is not INamedTypeSymbol namedCategory)
        {
            return;
        }

        Report(context, typeOf.Type, namedCategory);
    }

    /// <summary>Returns whether a factory method's return type is a logger.</summary>
    /// <param name="returnType">The method's return type.</param>
    /// <param name="genericLogger">The generic logger type.</param>
    /// <returns><see langword="true"/> when the method returns a logger.</returns>
    private static bool ReturnsLogger(ITypeSymbol returnType, INamedTypeSymbol genericLogger)
    {
        if (returnType is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, genericLogger))
        {
            return true;
        }

        return returnType.Name == LoggerIdentifier && returnType.ContainingNamespace.ToDisplayString() == "Microsoft.Extensions.Logging";
    }

    /// <summary>Reports a mismatched category unless an exemption applies.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="categorySyntax">The syntax naming the category, which the fix rewrites.</param>
    /// <param name="category">The category type.</param>
    private static void Report(SyntaxNodeAnalysisContext context, SyntaxNode categorySyntax, ITypeSymbol category)
    {
        if (category is not INamedTypeSymbol namedCategory
            || FindEnclosingType(context.Node) is not { } enclosingSyntax
            || context.SemanticModel.GetDeclaredSymbol(enclosingSyntax, context.CancellationToken) is not { } enclosing)
        {
            return;
        }

        if (IsCorrectCategory(namedCategory, enclosing))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(EnclosingTypeKey, enclosing.Name);
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.WrongLoggerCategory,
            categorySyntax.SyntaxTree,
            categorySyntax.Span,
            properties,
            namedCategory.Name,
            enclosing.Name));
    }

    /// <summary>Returns whether a category is the enclosing type or a deliberate stand-in for it.</summary>
    /// <param name="category">The category type.</param>
    /// <param name="enclosing">The enclosing type.</param>
    /// <returns><see langword="true"/> when the category should not be reported.</returns>
    private static bool IsCorrectCategory(INamedTypeSymbol category, INamedTypeSymbol enclosing)
        => SymbolEqualityComparer.Default.Equals(category.OriginalDefinition, enclosing.OriginalDefinition)
            || IsBaseType(category, enclosing)
            || ImplementsInterface(category, enclosing)
            || IsNestedInside(category, enclosing)
            || IsCategoryMarker(category);

    /// <summary>Returns whether a category is a base type of the enclosing type.</summary>
    /// <param name="category">The category type.</param>
    /// <param name="enclosing">The enclosing type.</param>
    /// <returns><see langword="true"/> when the category is a base type.</returns>
    private static bool IsBaseType(INamedTypeSymbol category, INamedTypeSymbol enclosing)
    {
        for (var baseType = enclosing.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, category.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the enclosing type implements the category as an interface.</summary>
    /// <param name="category">The category type.</param>
    /// <param name="enclosing">The enclosing type.</param>
    /// <returns><see langword="true"/> when the category is an implemented interface.</returns>
    private static bool ImplementsInterface(INamedTypeSymbol category, INamedTypeSymbol enclosing)
    {
        if (category.TypeKind != TypeKind.Interface)
        {
            return false;
        }

        var interfaces = enclosing.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i].OriginalDefinition, category.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the enclosing type is nested inside the category.</summary>
    /// <param name="category">The category type.</param>
    /// <param name="enclosing">The enclosing type.</param>
    /// <returns><see langword="true"/> when the enclosing type is nested inside the category.</returns>
    private static bool IsNestedInside(INamedTypeSymbol category, INamedTypeSymbol enclosing)
    {
        for (var container = enclosing.ContainingType; container is not null; container = container.ContainingType)
        {
            if (SymbolEqualityComparer.Default.Equals(container.OriginalDefinition, category.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a category is a dedicated marker type rather than a real logging type.</summary>
    /// <param name="category">The category type.</param>
    /// <returns><see langword="true"/> when the category is a marker.</returns>
    private static bool IsCategoryMarker(INamedTypeSymbol category)
        => category.Name.EndsWith("Category", System.StringComparison.Ordinal)
            || category.Name.EndsWith("Logs", System.StringComparison.Ordinal)
            || IsEmptyType(category);

    /// <summary>Returns whether a type declares no members of its own.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> when the type is empty.</returns>
    private static bool IsEmptyType(INamedTypeSymbol type)
    {
        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (!members[i].IsImplicitlyDeclared)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns the declared type of a field.</summary>
    /// <param name="node">The field declaration.</param>
    /// <returns>The field's type syntax.</returns>
    private static TypeSyntax GetFieldType(SyntaxNode node) => ((FieldDeclarationSyntax)node).Declaration.Type;

    /// <summary>Finds the type declaration a node sits inside.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The enclosing type declaration, or <see langword="null"/>.</returns>
    private static TypeDeclarationSyntax? FindEnclosingType(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is TypeDeclarationSyntax type)
            {
                return type;
            }
        }

        return null;
    }
}
