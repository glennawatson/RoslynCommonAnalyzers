// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a sensitive framework diagnostic switch that is enabled without a development-environment guard
/// (SES1512). Three switches deliberately log values that are normally withheld and are safe only in
/// Development: a call to <c>EnableSensitiveDataLogging()</c> whose containing type is
/// <c>Microsoft.EntityFrameworkCore.DbContextOptionsBuilder</c> or the generic
/// <c>DbContextOptionsBuilder&lt;TContext&gt;</c> (logs the parameter values bound into SQL); and an assignment
/// of <c>true</c> to the static <c>ShowPII</c> (un-redacts personally identifiable information) or
/// <c>LogCompleteSecurityArtifact</c> (logs full security tokens) property of
/// <c>Microsoft.IdentityModel.Logging.IdentityModelEventSource</c>. The rule reports the enabling call or
/// assignment when no enclosing <c>if</c> statement or conditional whose condition calls a method named
/// <c>IsDevelopment</c> guards it -- a purely local ancestor scan, no data-flow. Each surface is independently
/// marker-gated: builder types and the event source are resolved only after the corresponding syntax filter
/// matches, so a compilation with no candidate calls or assignments performs no metadata lookup.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1512SensitiveFrameworkDiagnosticsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the Entity Framework Core method that enables sensitive parameter-value logging.</summary>
    private const string EnableSensitiveDataLoggingMethodName = "EnableSensitiveDataLogging";

    /// <summary>The name of the static property that un-redacts personally identifiable information in identity logs.</summary>
    private const string ShowPiiPropertyName = "ShowPII";

    /// <summary>The name of the static property that logs the complete security token.</summary>
    private const string LogCompleteSecurityArtifactPropertyName = "LogCompleteSecurityArtifact";

    /// <summary>The simple type name reported alongside a guarded identity-logging property.</summary>
    private const string IdentityModelEventSourceTypeName = "IdentityModelEventSource";

    /// <summary>The metadata name of the identity-logging event source whose sensitive properties are guarded.</summary>
    private const string IdentityModelEventSourceMetadataName = "Microsoft.IdentityModel.Logging.IdentityModelEventSource";

    /// <summary>The metadata names of the EF Core option builders whose <c>EnableSensitiveDataLogging</c> is guarded.</summary>
    private static readonly string[] DbContextOptionsBuilderMetadataNames =
    [
        "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder",
        "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder`1"
    ];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.SensitiveFrameworkDiagnosticsEnabled);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataTypeSet(compilation, DbContextOptionsBuilderMetadataNames),
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, IdentityModelEventSourceMetadataName),
            AnalyzeAssignment,
            SyntaxKind.SimpleAssignmentExpression);
    }

    /// <summary>Reports SES1512 for an unguarded <c>EnableSensitiveDataLogging</c> call on a gated builder type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="builderTypes">The EF Core option-builder types resolved on first demand for the compilation.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyMetadataTypeSet builderTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a call to a member named 'EnableSensitiveDataLogging'.
        if (InvokedName.Of(invocation.Expression) is not EnableSensitiveDataLoggingMethodName
            || DevelopmentGuard.Encloses(invocation))
        {
            return;
        }

        if (!IsUnconditionallyEnabled(invocation.ArgumentList, context.SemanticModel, context.CancellationToken)
            || builderTypes.Get() is not { Length: > 0 } resolvedBuilderTypes
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: EnableSensitiveDataLoggingMethodName } method
            || !TypeRelations.IsOneOf(method.ContainingType.OriginalDefinition, resolvedBuilderTypes))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.SensitiveFrameworkDiagnosticsEnabled,
            invocation.SyntaxTree,
            invocation.Span,
            EnableSensitiveDataLoggingMethodName));
    }

    /// <summary>Reports SES1512 for an unguarded <c>true</c> assignment to a gated identity-logging property.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="eventSourceTypes">The identity event-source type cache for this compilation.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context, LazyMetadataType eventSourceTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.ShowPII = true' / '<expr>.LogCompleteSecurityArtifact = true' or the
        // bare-identifier form under a 'using static'. Both bind the left member to the property below.
        if (!assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression)
            || GetSensitiveIdentityMember(assignment.Left) is not { } memberExpression
            || DevelopmentGuard.Encloses(assignment))
        {
            return;
        }

        if (eventSourceTypes.Get() is not { } eventSourceType
            || context.SemanticModel.GetSymbolInfo(memberExpression, context.CancellationToken).Symbol is not IPropertySymbol { IsStatic: true } property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, eventSourceType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.SensitiveFrameworkDiagnosticsEnabled,
            assignment.SyntaxTree,
            assignment.Span,
            $"{IdentityModelEventSourceTypeName}.{property.Name}"));
    }

    /// <summary>Returns whether an <c>EnableSensitiveDataLogging</c> call turns the switch on rather than off.</summary>
    /// <param name="argumentList">The invocation's argument list.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// <see langword="true"/> when the call has no argument (the parameter defaults to <c>true</c>) or its single
    /// argument is the compile-time constant <c>true</c>. A constant <c>false</c> disables the switch, and a
    /// runtime flag (including an inline environment guard) is the developer's own gate, so both are treated as safe.
    /// </returns>
    private static bool IsUnconditionallyEnabled(ArgumentListSyntax argumentList, SemanticModel model, CancellationToken cancellationToken)
    {
        var arguments = argumentList.Arguments;
        if (arguments.Count == 0)
        {
            return true;
        }

        return model.GetConstantValue(arguments[0].Expression, cancellationToken) is { HasValue: true, Value: true };
    }

    /// <summary>Returns the assignment's left expression when it names a gated identity-logging property.</summary>
    /// <param name="left">The assignment's left-hand expression.</param>
    /// <returns>The left expression to bind, or <see langword="null"/> when it is not a guarded member.</returns>
    private static ExpressionSyntax? GetSensitiveIdentityMember(ExpressionSyntax left) =>
        MemberReferenceName.Of(left) switch
        {
            ShowPiiPropertyName or LogCompleteSecurityArtifactPropertyName => left,
            _ => null,
        };
}
