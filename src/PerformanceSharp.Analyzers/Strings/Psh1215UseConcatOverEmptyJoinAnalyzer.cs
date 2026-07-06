// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports static <c>string.Join</c> calls whose separator is the empty string — the
/// literal <c>""</c> or <see cref="string.Empty"/> — when the remaining arguments have a
/// matching <c>string.Concat</c> overload (PSH1215). Covered shapes:
/// <c>Join(string, params object[])</c>, <c>Join(string, params string[])</c> (including the
/// expanded params call form), <c>Join(string, IEnumerable&lt;string&gt;)</c>,
/// <c>Join&lt;T&gt;(string, IEnumerable&lt;T&gt;)</c>, and the params-span overloads where the
/// host framework has them. <c>Join(string, string[], int, int)</c> has no <c>Concat</c>
/// equivalent and char-separator overloads can never have an empty separator, so neither is
/// reported. Each shape is gated once per compilation on the corresponding <c>Concat</c>
/// overload existing, so the fix always compiles.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1215UseConcatOverEmptyJoinAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The parameter count of the Join overloads that have a Concat equivalent (the separator plus one values parameter).</summary>
    private const int JoinParameterCount = 2;

    /// <summary>The minimum argument count of a candidate call (the separator plus at least one value).</summary>
    private const int MinimumArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseConcatOverEmptyJoin);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var overloads = ConcatOverloads.Resolve(start.Compilation);
            if (!overloads.HasAny)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, overloads), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Runs the syntax-only checks: a <c>string.Join</c>/<c>String.Join</c> member access whose first argument looks like the empty string.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="separator">The separator argument expression when the invocation is a candidate.</param>
    /// <param name="separatorIsLiteral"><see langword="true"/> when the separator is the literal <c>""</c>; a <c>.Empty</c> access still needs the semantic field check.</param>
    /// <returns><see langword="true"/> when the invocation is a syntactic candidate.</returns>
    internal static bool IsCandidate(InvocationExpressionSyntax invocation, out ExpressionSyntax? separator, out bool separatorIsLiteral)
    {
        separator = null;
        separatorIsLiteral = false;

        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || access.Name.Identifier.ValueText is not "Join"
            || !IsStringReceiver(access.Expression))
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < MinimumArgumentCount || arguments[0].NameColon is not null)
        {
            return false;
        }

        var expression = arguments[0].Expression;
        if (IsEmptyStringLiteral(expression))
        {
            separator = expression;
            separatorIsLiteral = true;
            return true;
        }

        if (!IsEmptyMemberAccess(expression))
        {
            return false;
        }

        separator = expression;
        return true;
    }

    /// <summary>Reports PSH1215 for an empty-separator Join call whose values have a Concat overload.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="overloads">The Concat overloads available in this compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, ConcatOverloads overloads)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsCandidate(invocation, out var separator, out var separatorIsLiteral))
        {
            return;
        }

        var model = context.SemanticModel;
        if ((!separatorIsLiteral && !IsStringEmptyField(model, separator!, context.CancellationToken))
            || !IsReportableJoin(model, invocation, overloads, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseConcatOverEmptyJoin,
            invocation.SyntaxTree,
            invocation.Span));
    }

    /// <summary>Returns whether the receiver is the <c>string</c> keyword or the <c>String</c> identifier, syntactically.</summary>
    /// <param name="expression">The member access receiver.</param>
    /// <returns><see langword="true"/> for a <c>string.Join</c> or <c>String.Join</c> spelling.</returns>
    private static bool IsStringReceiver(ExpressionSyntax expression)
    {
        if (expression is PredefinedTypeSyntax predefined)
        {
            return predefined.Keyword.IsKind(SyntaxKind.StringKeyword);
        }

        return expression is IdentifierNameSyntax { Identifier.ValueText: "String" };
    }

    /// <summary>Returns whether an expression is the literal <c>""</c>.</summary>
    /// <param name="expression">The candidate separator expression.</param>
    /// <returns><see langword="true"/> for a string literal whose value is empty.</returns>
    private static bool IsEmptyStringLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
            && literal.Token.ValueText.Length == 0;

    /// <summary>Returns whether an expression is a member access ending in <c>.Empty</c>, syntactically.</summary>
    /// <param name="expression">The candidate separator expression.</param>
    /// <returns><see langword="true"/> for a simple member access named <c>Empty</c>.</returns>
    private static bool IsEmptyMemberAccess(ExpressionSyntax expression)
        => expression is MemberAccessExpressionSyntax access
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && access.Name is IdentifierNameSyntax { Identifier.ValueText: "Empty" };

    /// <summary>Returns whether a <c>.Empty</c> access binds to the <see cref="string.Empty"/> field.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="separator">The <c>.Empty</c> member access to bind.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the access is the static <c>Empty</c> field on <see cref="string"/>.</returns>
    private static bool IsStringEmptyField(SemanticModel model, ExpressionSyntax separator, CancellationToken cancellationToken)
        => model.GetSymbolInfo(separator, cancellationToken).Symbol is IFieldSymbol
        {
            IsStatic: true,
            ContainingType.SpecialType: SpecialType.System_String
        };

    /// <summary>Runs the semantic checks: the invocation binds to a two-parameter static <c>string.Join</c> with a gated Concat equivalent.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="overloads">The Concat overloads available in this compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the invocation binds to a reportable Join overload.</returns>
    private static bool IsReportableJoin(SemanticModel model, InvocationExpressionSyntax invocation, in ConcatOverloads overloads, CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
            && IsTwoParameterStaticStringMethod(method)
            && method.Parameters[0].Type.SpecialType == SpecialType.System_String
            && HasMatchingConcatOverload(method, overloads);

    /// <summary>Returns whether a bound method is a two-parameter static member of <see cref="string"/>.</summary>
    /// <param name="method">The bound Join method.</param>
    /// <returns><see langword="true"/> for the Join overload arity that can have a Concat equivalent.</returns>
    private static bool IsTwoParameterStaticStringMethod(IMethodSymbol method)
        => method is { IsStatic: true, ContainingType.SpecialType: SpecialType.System_String, Parameters.Length: JoinParameterCount };

    /// <summary>Maps the bound Join overload's values parameter to its Concat overload gate.</summary>
    /// <param name="method">The bound Join method.</param>
    /// <param name="overloads">The Concat overloads available in this compilation.</param>
    /// <returns><see langword="true"/> when the matching Concat overload exists.</returns>
    private static bool HasMatchingConcatOverload(IMethodSymbol method, in ConcatOverloads overloads)
    {
        var valuesType = method.Parameters[1].Type;
        if (valuesType is IArrayTypeSymbol array)
        {
            return HasArrayConcat(array, overloads);
        }

        if (valuesType is not INamedTypeSymbol { TypeArguments.Length: 1 } named)
        {
            return false;
        }

        if (named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            return HasEnumerableConcat(method, named, overloads);
        }

        return IsReadOnlySpan(named) && HasSpanConcat(named, overloads);
    }

    /// <summary>Maps an array-typed values parameter to its Concat overload gate.</summary>
    /// <param name="array">The values parameter's array type.</param>
    /// <param name="overloads">The Concat overloads available in this compilation.</param>
    /// <returns><see langword="true"/> when the matching array Concat overload exists.</returns>
    private static bool HasArrayConcat(IArrayTypeSymbol array, in ConcatOverloads overloads)
        => array.ElementType.SpecialType switch
        {
            SpecialType.System_Object => overloads.ObjectArray,
            SpecialType.System_String => overloads.StringArray,
            _ => false,
        };

    /// <summary>Maps an <c>IEnumerable</c>-typed values parameter to its Concat overload gate.</summary>
    /// <param name="method">The bound Join method.</param>
    /// <param name="named">The values parameter's constructed type.</param>
    /// <param name="overloads">The Concat overloads available in this compilation.</param>
    /// <returns><see langword="true"/> when the matching enumerable Concat overload exists.</returns>
    private static bool HasEnumerableConcat(IMethodSymbol method, INamedTypeSymbol named, in ConcatOverloads overloads)
        => method.Arity == 1
            ? overloads.GenericEnumerable
            : named.TypeArguments[0].SpecialType == SpecialType.System_String && overloads.StringEnumerable;

    /// <summary>Maps a <c>ReadOnlySpan</c>-typed values parameter to its Concat overload gate.</summary>
    /// <param name="named">The values parameter's constructed type.</param>
    /// <param name="overloads">The Concat overloads available in this compilation.</param>
    /// <returns><see langword="true"/> when the matching span Concat overload exists.</returns>
    private static bool HasSpanConcat(INamedTypeSymbol named, in ConcatOverloads overloads)
        => named.TypeArguments[0].SpecialType switch
        {
            SpecialType.System_String => overloads.SpanOfString,
            SpecialType.System_Object => overloads.SpanOfObject,
            _ => false,
        };

    /// <summary>Returns whether a type is the framework's <c>System.ReadOnlySpan&lt;T&gt;</c>.</summary>
    /// <param name="type">The candidate type.</param>
    /// <returns><see langword="true"/> for <c>ReadOnlySpan&lt;T&gt;</c> in the <c>System</c> namespace.</returns>
    private static bool IsReadOnlySpan(INamedTypeSymbol type)
        => type is { Name: "ReadOnlySpan", Arity: 1, ContainingType: null }
            && type.ContainingNamespace is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };

    /// <summary>The single-values-argument <c>string.Concat</c> overloads available in the current compilation.</summary>
    /// <param name="ObjectArray">Whether <c>Concat(params object[])</c> exists.</param>
    /// <param name="StringArray">Whether <c>Concat(params string[])</c> exists.</param>
    /// <param name="StringEnumerable">Whether <c>Concat(IEnumerable&lt;string&gt;)</c> exists.</param>
    /// <param name="GenericEnumerable">Whether <c>Concat&lt;T&gt;(IEnumerable&lt;T&gt;)</c> exists.</param>
    /// <param name="SpanOfString">Whether <c>Concat(params ReadOnlySpan&lt;string&gt;)</c> exists.</param>
    /// <param name="SpanOfObject">Whether <c>Concat(params ReadOnlySpan&lt;object&gt;)</c> exists.</param>
    internal readonly record struct ConcatOverloads(
        bool ObjectArray,
        bool StringArray,
        bool StringEnumerable,
        bool GenericEnumerable,
        bool SpanOfString,
        bool SpanOfObject)
    {
        /// <summary>Gets a value indicating whether at least one gated shape can ever be reported.</summary>
        public bool HasAny => ObjectArray || StringArray || StringEnumerable || GenericEnumerable || SpanOfString || SpanOfObject;

        /// <summary>Probes the compilation's <see cref="string"/> member list once for the Concat overloads.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The available Concat overloads.</returns>
        public static ConcatOverloads Resolve(Compilation compilation)
        {
            var stringType = compilation.GetSpecialType(SpecialType.System_String);
            var objectArray = false;
            var stringArray = false;
            var stringEnumerable = false;
            var genericEnumerable = false;
            var spanOfString = false;
            var spanOfObject = false;

            foreach (var member in stringType.GetMembers("Concat"))
            {
                if (member is not IMethodSymbol { IsStatic: true, Parameters.Length: 1 } method)
                {
                    continue;
                }

                var parameterType = method.Parameters[0].Type;
                if (method.Arity == 1)
                {
                    genericEnumerable |= IsGenericEnumerable(parameterType);
                }
                else if (method.Arity == 0)
                {
                    ClassifyNonGenericConcat(parameterType, ref objectArray, ref stringArray, ref stringEnumerable, ref spanOfString, ref spanOfObject);
                }
            }

            return new(objectArray, stringArray, stringEnumerable, genericEnumerable, spanOfString, spanOfObject);
        }

        /// <summary>Returns whether a parameter type is a constructed <c>IEnumerable&lt;T&gt;</c>.</summary>
        /// <param name="parameterType">The overload's single parameter type.</param>
        /// <returns><see langword="true"/> for the generic enumerable Concat shape.</returns>
        private static bool IsGenericEnumerable(ITypeSymbol parameterType)
            => parameterType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T };

        /// <summary>Classifies one arity-zero single-parameter Concat overload into the availability flags.</summary>
        /// <param name="parameterType">The overload's single parameter type.</param>
        /// <param name="objectArray">Set when the overload is <c>Concat(params object[])</c>.</param>
        /// <param name="stringArray">Set when the overload is <c>Concat(params string[])</c>.</param>
        /// <param name="stringEnumerable">Set when the overload is <c>Concat(IEnumerable&lt;string&gt;)</c>.</param>
        /// <param name="spanOfString">Set when the overload is <c>Concat(params ReadOnlySpan&lt;string&gt;)</c>.</param>
        /// <param name="spanOfObject">Set when the overload is <c>Concat(params ReadOnlySpan&lt;object&gt;)</c>.</param>
        private static void ClassifyNonGenericConcat(
            ITypeSymbol parameterType,
            ref bool objectArray,
            ref bool stringArray,
            ref bool stringEnumerable,
            ref bool spanOfString,
            ref bool spanOfObject)
        {
            if (parameterType is IArrayTypeSymbol array)
            {
                objectArray |= array.ElementType.SpecialType == SpecialType.System_Object;
                stringArray |= array.ElementType.SpecialType == SpecialType.System_String;
                return;
            }

            if (parameterType is not INamedTypeSymbol { TypeArguments.Length: 1 } named)
            {
                return;
            }

            var elementType = named.TypeArguments[0].SpecialType;
            if (named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                stringEnumerable |= elementType == SpecialType.System_String;
            }
            else if (IsReadOnlySpan(named))
            {
                spanOfString |= elementType == SpecialType.System_String;
                spanOfObject |= elementType == SpecialType.System_Object;
            }
        }
    }
}
