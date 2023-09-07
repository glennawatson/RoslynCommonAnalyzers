// Copyright (c) 2023 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Blazor.Common.Analyzers;

#pragma warning disable SA1518
#pragma warning disable SA1402
#pragma warning disable SA1649

/// <summary>
/// Analyzer that makes sure that Parameters are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RCGS0001ConstructorDeclarationParameterMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic code for the analyzer.
    /// </summary>
    public const string DiagnosticId = "RCGS0001";

    private const string Category = "Readability";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConstructorDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node as ConstructorDeclarationSyntax;

        if (node is null)
        {
            return;
        }

        context.HandleParameterListSyntax(node.ParameterList, Rule);
    }
}

/// <summary>
/// Analyzer that makes sure that Parameters are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic code for the analyzer.
    /// </summary>
    public const string DiagnosticId = "RCGS0002";

    private const string Category = "Readability";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node as MethodDeclarationSyntax;

        if (node is null)
        {
            return;
        }

        context.HandleParameterListSyntax(node.ParameterList, Rule);
    }
}

/// <summary>
/// Analyzer that makes sure that Parameters are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RCGS0003DelegateDeclarationParameterMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic code for the analyzer.
    /// </summary>
    public const string DiagnosticId = "RCGS0003";

    private const string Category = "Readability";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.DelegateDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node as DelegateDeclarationSyntax;

        if (node is null)
        {
            return;
        }

        context.HandleParameterListSyntax(node.ParameterList, Rule);
    }
}

/// <summary>
/// Analyzer that makes sure that Parameters are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RCGS0004IndexerDeclarationParameterMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic code for the analyzer.
    /// </summary>
    public const string DiagnosticId = "RCGS0004";

    private const string Category = "Readability";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IndexerDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node as IndexerDeclarationSyntax;

        if (node is null)
        {
            return;
        }

        context.HandleParameterListSyntax(node.ParameterList, Rule);
    }
}

/// <summary>
/// Analyzer that makes sure that Arguments are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RCGS0005InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic code for the analyzer.
    /// </summary>
    public const string DiagnosticId = "RCGS0005";

    private const string Category = "Readability";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node as InvocationExpressionSyntax;

        if (node is null)
        {
            return;
        }

        context.HandleArgumentListSyntax(node.ArgumentList, Rule);
    }
}

/// <summary>
/// Analyzer that makes sure that Arguments are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RCGS0006ObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic code for the analyzer.
    /// </summary>
    public const string DiagnosticId = "RCGS0006";

    private const string Category = "Readability";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ObjectCreationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node as ObjectCreationExpressionSyntax;

        if (node is null)
        {
            return;
        }

        context.HandleArgumentListSyntax(node.ArgumentList, Rule);
    }
}

/// <summary>
/// Analyzer that makes sure that Arguments are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RCGS0007ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic code for the analyzer.
    /// </summary>
    public const string DiagnosticId = "RCGS0007";

    private const string Category = "Readability";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ElementAccessExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node as ElementAccessExpressionSyntax;

        if (node is null)
        {
            return;
        }

        context.HandleArgumentListSyntax(node.ArgumentList, Rule);
    }
}

/// <summary>
/// Analyzer that makes sure that Arguments are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RCGS0008AttributeArgumentMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic code for the analyzer.
    /// </summary>
    public const string DiagnosticId = "RCGS0008";

    private const string Category = "Readability";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node as AttributeSyntax;

        if (node is null)
        {
            return;
        }

        context.HandleArgumentListSyntax(node.ArgumentList, Rule);
    }
}

/// <summary>
/// Analyzer that makes sure that Parameters are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RCGS0009AnonymousMethodExpressionParameterMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic code for the analyzer.
    /// </summary>
    public const string DiagnosticId = "RCGS0009";

    private const string Category = "Readability";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AnonymousMethodExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node as AnonymousMethodExpressionSyntax;

        if (node is null)
        {
            return;
        }

        context.HandleParameterListSyntax(node.ParameterList, Rule);
    }
}

/// <summary>
/// Analyzer that makes sure that Parameters are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RCGS0010ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic code for the analyzer.
    /// </summary>
    public const string DiagnosticId = "RCGS0010";

    private const string Category = "Readability";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ParameterAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ParenthesizedLambdaExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node as ParenthesizedLambdaExpressionSyntax;

        if (node is null)
        {
            return;
        }

        context.HandleParameterListSyntax(node.ParameterList, Rule);
    }
}

