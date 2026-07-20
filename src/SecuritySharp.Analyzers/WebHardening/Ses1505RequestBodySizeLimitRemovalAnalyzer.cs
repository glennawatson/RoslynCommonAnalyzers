// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags code that removes the incoming request body size limit, allowing an unbounded upload (SES1505).
/// Two shapes are reported. The attribute shape is <c>[DisableRequestSizeLimit]</c> applied to a controller
/// or action -- the applied attribute is bound to
/// <c>Microsoft.AspNetCore.Mvc.DisableRequestSizeLimitAttribute</c> and its span is reported. The assignment
/// shape is a member named <c>MaxRequestBodySize</c> set to the literal <c>null</c> (which means "no limit"),
/// written directly (<c>limits.MaxRequestBodySize = null</c>) or in an object initializer
/// (<c>new KestrelServerLimits { MaxRequestBodySize = null }</c>), when the assigned member's containing type
/// is <c>Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerLimits</c> or
/// <c>Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature</c>. The attribute type and the limits
/// types are probed once per compilation and each check is registered only when its type resolves, so a project
/// that references neither surface registers nothing and pays no analysis cost.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1505RequestBodySizeLimitRemovalAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the body-size property whose <c>null</c> assignment removes the limit.</summary>
    private const string MaxRequestBodySizePropertyName = "MaxRequestBodySize";

    /// <summary>The message argument used when the attribute shape removes the limit.</summary>
    private const string DisableAttributeDisplay = "[DisableRequestSizeLimit]";

    /// <summary>The message argument used when the assignment shape removes the limit.</summary>
    private const string NullAssignmentDisplay = "MaxRequestBodySize = null";

    /// <summary>The short attribute-name spelling used by the syntactic prefilter.</summary>
    private const string DisableAttributeShortName = "DisableRequestSizeLimit";

    /// <summary>The long attribute-name spelling used by the syntactic prefilter.</summary>
    private const string DisableAttributeLongName = "DisableRequestSizeLimitAttribute";

    /// <summary>The metadata name of the attribute whose application removes the limit.</summary>
    private const string DisableAttributeMetadataName = "Microsoft.AspNetCore.Mvc.DisableRequestSizeLimitAttribute";

    /// <summary>The metadata names of the types whose <c>MaxRequestBodySize</c> member is guarded.</summary>
    private static readonly string[] LimitsMetadataNames =
    [
        "Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerLimits",
        "Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature"
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.RequestBodySizeLimitRemoval);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(DisableAttributeMetadataName) is { } attributeType)
            {
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAttribute(nodeContext, attributeType), SyntaxKind.Attribute);
            }

            var limitsTypes = GetLimitsTypes(start.Compilation);
            if (limitsTypes is not null)
            {
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, limitsTypes), SyntaxKind.SimpleAssignmentExpression);
            }
        });
    }

    /// <summary>Reports SES1505 for a <c>[DisableRequestSizeLimit]</c> application.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attributeType">The gated <c>DisableRequestSizeLimitAttribute</c> type.</param>
    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, INamedTypeSymbol attributeType)
    {
        var attribute = (AttributeSyntax)context.Node;

        // Syntactic prefilter: the attribute is spelled 'DisableRequestSizeLimit' or 'DisableRequestSizeLimitAttribute'.
        if (GetAttributeSimpleName(attribute.Name) is not (DisableAttributeShortName or DisableAttributeLongName))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, attributeType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.RequestBodySizeLimitRemoval,
            attribute.SyntaxTree,
            attribute.Span,
            DisableAttributeDisplay));
    }

    /// <summary>Reports SES1505 for a <c>MaxRequestBodySize = null</c> assignment on a gated limits type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="limitsTypes">The gated body-size-limit types resolved for the compilation.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol?[] limitsTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.MaxRequestBodySize = null' or the initializer form 'MaxRequestBodySize = null'.
        if (!assignment.Right.IsKind(SyntaxKind.NullLiteralExpression)
            || GetMaxRequestBodySizeTarget(assignment.Left) is not { } memberExpression)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(memberExpression, context.CancellationToken).Symbol is not IPropertySymbol { Name: MaxRequestBodySizePropertyName } property
            || !IsGatedLimitsType(property.ContainingType, limitsTypes))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.RequestBodySizeLimitRemoval,
            assignment.SyntaxTree,
            assignment.Span,
            NullAssignmentDisplay));
    }

    /// <summary>Returns the simple identifier text of an attribute name, ignoring any qualifier or alias.</summary>
    /// <param name="name">The attribute's name syntax.</param>
    /// <returns>The rightmost simple name, or <see langword="null"/> when it cannot be read syntactically.</returns>
    private static string? GetAttributeSimpleName(NameSyntax name)
        => name switch
        {
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns the assignment's left expression when it names <c>MaxRequestBodySize</c>.</summary>
    /// <param name="left">The assignment's left-hand expression.</param>
    /// <returns>The left expression to bind, or <see langword="null"/> when it is not the guarded member.</returns>
    private static ExpressionSyntax? GetMaxRequestBodySizeTarget(ExpressionSyntax left)
        => left switch
        {
            // 'limits.MaxRequestBodySize = null'.
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: MaxRequestBodySizePropertyName } => left,

            // 'new KestrelServerLimits { MaxRequestBodySize = null }' (object-initializer member).
            IdentifierNameSyntax { Identifier.ValueText: MaxRequestBodySizePropertyName } => left,

            _ => null,
        };

    /// <summary>Returns whether a property's containing type is one of the gated limits types.</summary>
    /// <param name="containingType">The bound property's containing type.</param>
    /// <param name="limitsTypes">The gated body-size-limit types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the container is a gated limits type.</returns>
    private static bool IsGatedLimitsType(INamedTypeSymbol containingType, INamedTypeSymbol?[] limitsTypes)
    {
        for (var i = 0; i < limitsTypes.Length; i++)
        {
            if (limitsTypes[i] is { } limitsType && SymbolEqualityComparer.Default.Equals(limitsType, containingType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the body-size-limit types present in the compilation.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>An array whose slots hold each resolved limits type, or <see langword="null"/> when none resolve.</returns>
    private static INamedTypeSymbol?[]? GetLimitsTypes(Compilation compilation)
    {
        INamedTypeSymbol?[]? types = null;
        for (var i = 0; i < LimitsMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(LimitsMetadataNames[i]) is { } type)
            {
                types ??= new INamedTypeSymbol?[LimitsMetadataNames.Length];
                types[i] = type;
            }
        }

        return types;
    }
}
