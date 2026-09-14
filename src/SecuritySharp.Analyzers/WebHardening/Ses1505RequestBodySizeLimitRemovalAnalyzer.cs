// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
/// types are resolved independently on first demand per compilation, after the corresponding syntax
/// filter passes, so a project without candidates pays no metadata resolution cost.
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

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.RequestBodySizeLimitRemoval);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeActions(
            context,
            static compilation => new RequestLimitTypes(compilation),
            new(AnalyzeAttribute, [SyntaxKind.Attribute]),
            new(AnalyzeAssignment, [SyntaxKind.SimpleAssignmentExpression]));
    }

    /// <summary>Reports SES1505 for a <c>[DisableRequestSizeLimit]</c> application.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="markers">The request-limit types resolved on first demand.</param>
    private static void AnalyzeAttribute(in SyntaxNodeAnalysisContext context, RequestLimitTypes markers)
    {
        var attribute = (AttributeSyntax)context.Node;

        // Syntactic prefilter: the attribute is spelled 'DisableRequestSizeLimit' or 'DisableRequestSizeLimitAttribute'.
        if (SyntaxNames.GetSimpleName(attribute.Name) is not (DisableAttributeShortName or DisableAttributeLongName))
        {
            return;
        }

        if (markers.GetAttribute() is not { } attributeType
            || context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
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
    /// <param name="markers">The request-limit types resolved on first demand.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context, RequestLimitTypes markers)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.MaxRequestBodySize = null' or the initializer form 'MaxRequestBodySize = null'.
        if (!assignment.Right.IsKind(SyntaxKind.NullLiteralExpression)
            || GetMaxRequestBodySizeTarget(assignment.Left) is not { } memberExpression)
        {
            return;
        }

        var limitsTypes = markers.GetLimits();
        if (limitsTypes.Length == 0
            || context.SemanticModel.GetSymbolInfo(memberExpression, context.CancellationToken).Symbol is not IPropertySymbol { Name: MaxRequestBodySizePropertyName } property
            || !TypeRelations.IsOneOf(property.ContainingType, limitsTypes))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.RequestBodySizeLimitRemoval,
            assignment.SyntaxTree,
            assignment.Span,
            NullAssignmentDisplay));
    }

    /// <summary>Returns the assignment's left expression when it names <c>MaxRequestBodySize</c>.</summary>
    /// <param name="left">The assignment's left-hand expression.</param>
    /// <returns>The left expression to bind, or <see langword="null"/> when it is not the guarded member.</returns>
    private static ExpressionSyntax? GetMaxRequestBodySizeTarget(ExpressionSyntax left) =>
        left switch
        {
            // 'limits.MaxRequestBodySize = null'.
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: MaxRequestBodySizePropertyName } or IdentifierNameSyntax { Identifier.ValueText: MaxRequestBodySizePropertyName } => left,

            _ => null,
        };

    /// <summary>Resolves each request-limit surface only when its syntax filter passes.</summary>
    /// <param name="compilation">The compilation whose request-limit types are resolved.</param>
    private sealed class RequestLimitTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the attribute whose application removes the limit.</summary>
        private const string DisableAttributeMetadataName = "Microsoft.AspNetCore.Mvc.DisableRequestSizeLimitAttribute";

        /// <summary>The metadata names of the types whose <c>MaxRequestBodySize</c> member is guarded.</summary>
        private static readonly string[] LimitsMetadataNames =
        [
            "Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerLimits",
            "Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature"
        ];

        /// <summary>The cached attribute type, including a null slot when it is absent.</summary>
        private INamedTypeSymbol?[]? _attribute;

        /// <summary>The cached limits types the compilation can see, including an empty result.</summary>
        private INamedTypeSymbol[]? _limits;

        /// <summary>Gets the request-limit attribute, caching its absence too.</summary>
        /// <returns>The attribute type, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetAttribute() => (_attribute ??= [compilation.GetTypeByMetadataName(DisableAttributeMetadataName)])[0];

        /// <summary>Gets the limits types, caching an empty result too.</summary>
        /// <returns>The limits types the compilation can see, or an empty array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol[] GetLimits() => _limits ??= MetadataTypeLookup.ResolveAll(compilation, LimitsMetadataNames);
    }
}
