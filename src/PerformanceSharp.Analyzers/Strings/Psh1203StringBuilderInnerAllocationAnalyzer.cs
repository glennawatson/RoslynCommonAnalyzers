// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests letting <c>System.Text.StringBuilder</c> do the formatting work instead of
/// appending an intermediate string (PSH1203). Reports <c>Append(string.Format(...))</c>
/// (use <c>AppendFormat</c>), <c>Append(x.ToString())</c> where a typed <c>Append</c>
/// overload takes the value directly, and <c>Append(s.Substring(...))</c> on a simple
/// receiver (use <c>Append(string, int, int)</c>). The <c>StringBuilder</c> type and the
/// overloads each shape rewrites to are probed once per compilation, so the rule costs
/// nothing where they are missing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1203StringBuilderInnerAllocationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the string builder type.</summary>
    private const string StringBuilderMetadataName = "System.Text.StringBuilder";

    /// <summary>The message argument suggesting <c>AppendFormat</c>.</summary>
    private const string AppendFormatSuggestion = "AppendFormat";

    /// <summary>The message argument suggesting a typed <c>Append</c> overload.</summary>
    private const string TypedAppendSuggestion = "the typed Append overload";

    /// <summary>The message argument suggesting the string-segment <c>Append</c> overload.</summary>
    private const string AppendSegmentSuggestion = "Append(string, int, int)";

    /// <summary>The argument count of the <c>Substring(startIndex)</c> shape.</summary>
    private const int SubstringStartOnlyArgumentCount = 1;

    /// <summary>The argument count of the <c>Substring(startIndex, length)</c> shape.</summary>
    private const int SubstringStartAndLengthArgumentCount = 2;

    /// <summary>The syntactic shape of the call nested inside the Append argument.</summary>
    private enum InnerCallShape
    {
        /// <summary>The argument is not a rewritable inner call.</summary>
        None,

        /// <summary>The argument is a <c>string.Format(...)</c> call.</summary>
        Format,

        /// <summary>The argument is a parameterless <c>x.ToString()</c> call.</summary>
        ToString,

        /// <summary>The argument is an <c>s.Substring(...)</c> call on a simple receiver.</summary>
        Substring,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.StringBuilderInnerAllocation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (!StringBuilderAppendSurface.TryResolve(start.Compilation, out var surface))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, surface), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>
    /// Returns whether a receiver expression is simple enough for a rewrite to repeat:
    /// an identifier, <c>this</c>, or a member-access chain over those. Anything else
    /// (calls, element accesses) risks duplicating work or side effects.
    /// </summary>
    /// <param name="expression">The receiver expression to probe.</param>
    /// <returns><see langword="true"/> when the receiver can be duplicated safely.</returns>
    internal static bool IsSimpleReceiver(ExpressionSyntax expression)
    {
        var current = expression;
        while (true)
        {
            switch (current)
            {
                case IdentifierNameSyntax:
                case ThisExpressionSyntax:
                    return true;
                case MemberAccessExpressionSyntax member when member.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                {
                    current = member.Expression;
                    continue;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>Reports PSH1203 for an Append argument the builder could format itself.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="surface">The string builder overloads available in this compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, in StringBuilderAppendSurface surface)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var shape = ClassifyShape(invocation, surface, out var inner, out var innerAccess, out var name);
        if (shape == InnerCallShape.None
            || !IsStringBuilderAppendString(context.SemanticModel, invocation, surface.BuilderType, context.CancellationToken))
        {
            return;
        }

        var suggestion = shape switch
        {
            InnerCallShape.Format when IsStringFormat(context.SemanticModel, inner!, context.CancellationToken) => AppendFormatSuggestion,
            InnerCallShape.ToString when IsTypedAppendToString(context.SemanticModel, inner!, innerAccess!, surface, context.CancellationToken) => TypedAppendSuggestion,
            InnerCallShape.Substring when IsStringSubstring(context.SemanticModel, inner!, context.CancellationToken) => AppendSegmentSuggestion,
            _ => null,
        };

        if (suggestion is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.StringBuilderInnerAllocation,
            name!.SyntaxTree,
            name.Span,
            suggestion));
    }

    /// <summary>Runs the syntax-only checks: member name, argument count, and inner call shape.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="surface">The string builder overloads available in this compilation.</param>
    /// <param name="inner">The inner call passed as the Append argument.</param>
    /// <param name="innerAccess">The inner call's member access.</param>
    /// <param name="name">The outer <c>Append</c> identifier the diagnostic reports on.</param>
    /// <returns>The syntactic shape of the inner call, or <see cref="InnerCallShape.None"/>.</returns>
    private static InnerCallShape ClassifyShape(
        InvocationExpressionSyntax invocation,
        in StringBuilderAppendSurface surface,
        out InvocationExpressionSyntax? inner,
        out MemberAccessExpressionSyntax? innerAccess,
        out IdentifierNameSyntax? name)
    {
        inner = null;
        innerAccess = null;
        name = null;

        if (!TryGetSingleArgumentAppend(invocation, out name, out var argument)
            || !TryGetInnerMemberCall(argument!, out inner, out innerAccess, out var innerName))
        {
            return InnerCallShape.None;
        }

        var innerArgumentCount = inner!.ArgumentList.Arguments.Count;
        return innerName!.Identifier.ValueText switch
        {
            "Format" when surface.HasAppendFormat => InnerCallShape.Format,
            "ToString" when innerArgumentCount == 0 => InnerCallShape.ToString,
            "Substring" when surface.HasAppendSegment
                && innerArgumentCount is SubstringStartOnlyArgumentCount or SubstringStartAndLengthArgumentCount
                && IsSimpleReceiver(innerAccess!.Expression) => InnerCallShape.Substring,
            _ => InnerCallShape.None,
        };
    }

    /// <summary>Returns whether an invocation is a single-argument <c>Append</c> member call.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="outerName">The outer <c>Append</c> identifier the diagnostic reports on.</param>
    /// <param name="argument">The single argument expression when the shape matches.</param>
    /// <returns><see langword="true"/> when the invocation is <c>receiver.Append(arg)</c>.</returns>
    private static bool TryGetSingleArgumentAppend(InvocationExpressionSyntax invocation, out IdentifierNameSyntax? outerName, out ExpressionSyntax? argument)
    {
        outerName = null;
        argument = null;

        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.ValueText: "Append" } outer } access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        outerName = outer;
        argument = invocation.ArgumentList.Arguments[0].Expression;
        return true;
    }

    /// <summary>Returns whether an expression is a member-access invocation such as <c>x.ToString()</c>.</summary>
    /// <param name="argument">The Append argument to inspect.</param>
    /// <param name="inner">The inner call when the shape matches.</param>
    /// <param name="innerAccess">The inner call's member access when the shape matches.</param>
    /// <param name="innerName">The inner call's member name when the shape matches.</param>
    /// <returns><see langword="true"/> when the argument is a simple member-access call.</returns>
    private static bool TryGetInnerMemberCall(
        ExpressionSyntax argument,
        out InvocationExpressionSyntax? inner,
        out MemberAccessExpressionSyntax? innerAccess,
        out IdentifierNameSyntax? innerName)
    {
        inner = null;
        innerAccess = null;
        innerName = null;

        if (argument is not InvocationExpressionSyntax innerCandidate
            || innerCandidate.Expression is not MemberAccessExpressionSyntax accessCandidate
            || !accessCandidate.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || accessCandidate.Name is not IdentifierNameSyntax candidateName)
        {
            return false;
        }

        inner = innerCandidate;
        innerAccess = accessCandidate;
        innerName = candidateName;
        return true;
    }

    /// <summary>Runs the outer semantic check: the invocation must bind to <c>StringBuilder.Append(string)</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="builderType">The resolved string builder type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the invocation binds to the string Append overload.</returns>
    private static bool IsStringBuilderAppendString(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol builderType,
        CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { IsStatic: false, Parameters: [{ Type.SpecialType: SpecialType.System_String }] } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType);

    /// <summary>Returns whether the inner call binds to a static <c>string.Format</c> overload.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="inner">The inner call passed as the Append argument.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the inner call is <c>string.Format</c>.</returns>
    private static bool IsStringFormat(SemanticModel model, InvocationExpressionSyntax inner, CancellationToken cancellationToken)
        => model.GetSymbolInfo(inner, cancellationToken).Symbol is IMethodSymbol
        {
            IsStatic: true,
            Name: "Format",
            ContainingType.SpecialType: SpecialType.System_String,
        };

    /// <summary>
    /// Returns whether the inner call is a parameterless instance <c>ToString</c> whose receiver
    /// either has a typed <c>Append</c> overload or is already a string (identity conversion).
    /// </summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="inner">The inner call passed as the Append argument.</param>
    /// <param name="innerAccess">The inner call's member access.</param>
    /// <param name="surface">The string builder overloads available in this compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when dropping the <c>ToString</c> call keeps the appended text identical.</returns>
    private static bool IsTypedAppendToString(
        SemanticModel model,
        InvocationExpressionSyntax inner,
        MemberAccessExpressionSyntax innerAccess,
        in StringBuilderAppendSurface surface,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(inner, cancellationToken).Symbol is not IMethodSymbol { IsStatic: false, Name: "ToString", Parameters.Length: 0 })
        {
            return false;
        }

        if (model.GetTypeInfo(innerAccess.Expression, cancellationToken).Type is not { } receiverType)
        {
            return false;
        }

        return receiverType.SpecialType == SpecialType.System_String || surface.HasTypedAppend(receiverType.SpecialType);
    }

    /// <summary>Returns whether the inner call binds to an instance <c>string.Substring</c> overload.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="inner">The inner call passed as the Append argument.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the inner call is <c>string.Substring</c>.</returns>
    private static bool IsStringSubstring(SemanticModel model, InvocationExpressionSyntax inner, CancellationToken cancellationToken)
        => model.GetSymbolInfo(inner, cancellationToken).Symbol is IMethodSymbol
        {
            IsStatic: false,
            Name: "Substring",
            ContainingType.SpecialType: SpecialType.System_String,
        };

    /// <summary>The <c>StringBuilder</c> type and the Append overload availability for one compilation.</summary>
    /// <param name="BuilderType">The resolved string builder type.</param>
    /// <param name="TypedAppendMask">A bitmask over <see cref="SpecialType"/> of the available typed Append overloads.</param>
    /// <param name="HasAppendFormat">Whether any <c>AppendFormat</c> overload exists.</param>
    /// <param name="HasAppendSegment">Whether <c>Append(string, int, int)</c> exists.</param>
    internal readonly record struct StringBuilderAppendSurface(
        INamedTypeSymbol BuilderType,
        ulong TypedAppendMask,
        bool HasAppendFormat,
        bool HasAppendSegment)
    {
        /// <summary>Probes the compilation once for <c>StringBuilder</c> and the Append overloads the rule rewrites to.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <param name="surface">The resolved overload availability.</param>
        /// <returns><see langword="true"/> when the string builder type exists.</returns>
        public static bool TryResolve(Compilation compilation, out StringBuilderAppendSurface surface)
        {
            if (compilation.GetTypeByMetadataName(StringBuilderMetadataName) is not { } builderType)
            {
                surface = default;
                return false;
            }

            var typedAppendMask = 0UL;
            var hasAppendSegment = false;
            foreach (var member in builderType.GetMembers("Append"))
            {
                if (member is not IMethodSymbol { IsStatic: false } method)
                {
                    continue;
                }

                if (method.Parameters is [{ } single])
                {
                    if (IsTypedAppendParameter(single.Type.SpecialType))
                    {
                        typedAppendMask |= 1UL << (int)single.Type.SpecialType;
                    }
                }
                else if (IsAppendSegmentSignature(method.Parameters))
                {
                    hasAppendSegment = true;
                }
            }

            surface = new(builderType, typedAppendMask, HasAppendFormatOverload(builderType), hasAppendSegment);
            return true;
        }

        /// <summary>Returns whether a typed <c>Append</c> overload exists for a receiver's special type.</summary>
        /// <param name="specialType">The receiver's special type.</param>
        /// <returns><see langword="true"/> when the overload exists.</returns>
        public bool HasTypedAppend(SpecialType specialType)
            => (TypedAppendMask & (1UL << (int)specialType)) != 0;

        /// <summary>Returns whether any instance <c>AppendFormat</c> overload exists on the string builder type.</summary>
        /// <param name="builderType">The string builder type.</param>
        /// <returns><see langword="true"/> when an overload exists.</returns>
        private static bool HasAppendFormatOverload(INamedTypeSymbol builderType)
        {
            foreach (var member in builderType.GetMembers("AppendFormat"))
            {
                if (member is IMethodSymbol { IsStatic: false })
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether a parameter list matches the <c>Append(string, int, int)</c> segment overload.</summary>
        /// <param name="parameters">The candidate parameter list.</param>
        /// <returns><see langword="true"/> for the three-parameter string-segment overload.</returns>
        private static bool IsAppendSegmentSignature(ImmutableArray<IParameterSymbol> parameters)
            => parameters is [{ Type.SpecialType: SpecialType.System_String }, { Type.SpecialType: SpecialType.System_Int32 }, { Type.SpecialType: SpecialType.System_Int32 }];

        /// <summary>Returns whether a special type has a dedicated typed <c>Append</c> overload worth probing for.</summary>
        /// <param name="specialType">The candidate parameter special type.</param>
        /// <returns><see langword="true"/> for the bool, char, and numeric primitives.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "the rule", Justification = "Explicit primitive list is clearer than a range check over SpecialType ordering.")]
        private static bool IsTypedAppendParameter(SpecialType specialType)
            => specialType is SpecialType.System_Boolean
                or SpecialType.System_Char
                or SpecialType.System_Decimal
                or SpecialType.System_Double
                or SpecialType.System_Single
                or SpecialType.System_Byte
                or SpecialType.System_SByte
                or SpecialType.System_Int16
                or SpecialType.System_UInt16
                or SpecialType.System_Int32
                or SpecialType.System_UInt32
                or SpecialType.System_Int64
                or SpecialType.System_UInt64;
    }
}
