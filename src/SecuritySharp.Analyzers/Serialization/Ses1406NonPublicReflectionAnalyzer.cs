// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.CodeAnalysis.Operations;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a reflection member lookup that reaches non-public members (SES1406, opt-in). The rule reports a
/// call to one of <c>System.Type</c>'s member-lookup methods -- <c>GetMethod</c>, <c>GetMethods</c>,
/// <c>GetField</c>, <c>GetFields</c>, <c>GetProperty</c>, <c>GetProperties</c>, <c>GetMember</c>,
/// <c>GetMembers</c>, <c>GetConstructor</c>, <c>GetConstructors</c>, <c>GetEvent</c>, <c>GetEvents</c>, or
/// <c>InvokeMember</c> -- whose <c>System.Reflection.BindingFlags</c> argument is a compile-time constant that
/// includes the <c>NonPublic</c> bit. The bit is recognized whether it is written as the literal
/// <c>BindingFlags.NonPublic</c>, folded inside an <c>|</c> expression such as
/// <c>BindingFlags.NonPublic | BindingFlags.Instance</c>, or referenced through a <c>const</c> local or field
/// whose constant value carries it. Reaching private or internal members through reflection defeats the
/// accessibility their author chose and couples the caller to internals that can change without notice.
/// Because a <c>TypeInfo</c> receiver inherits these methods from <c>System.Type</c>, calls on a
/// <c>TypeInfo</c> are covered as well; a lookup on an unrelated type, one without a <c>NonPublic</c> constant
/// in its flags, or one with no <c>BindingFlags</c> argument at all is left silent. A flags value assembled at
/// run time (for example a non-<c>const</c> field) is out of scope because confirming it would require
/// data-flow tracking. The keyed <c>System.Reflection.BindingFlags</c> and <c>System.Type</c> symbols anchor
/// the match; when a symbol is absent the comparison simply never matches and the rule stays silent, avoiding
/// a dead early-return.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1406NonPublicReflectionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the type that hosts the reflection member-lookup methods.</summary>
    private const string TypeMetadataName = "System.Type";

    /// <summary>The metadata name of the flags enum that selects which members a lookup reaches.</summary>
    private const string BindingFlagsMetadataName = "System.Reflection.BindingFlags";

    /// <summary>The name of the <c>BindingFlags</c> member whose bit selects non-public members.</summary>
    private const string NonPublicFieldName = "NonPublic";

    /// <summary>The <c>System.Type</c> member-lookup method names that accept a <c>BindingFlags</c> argument.</summary>
    private static readonly HashSet<string> MemberLookupNames = new(StringComparer.Ordinal)
    {
        "GetMethod",
        "GetMethods",
        "GetField",
        "GetFields",
        "GetProperty",
        "GetProperties",
        "GetMember",
        "GetMembers",
        "GetConstructor",
        "GetConstructors",
        "GetEvent",
        "GetEvents",
        "InvokeMember",
    };

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.NonPublicReflection);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // The keyed types anchor the match rather than a suggested API, and BindingFlags is present on
            // every target framework, so they are resolved once and passed through: when a symbol is absent
            // the comparisons below simply never match and the rule stays silent, avoiding a dead early-return.
            var bindingFlagsType = start.Compilation.GetTypeByMetadataName(BindingFlagsMetadataName);
            var typeType = start.Compilation.GetTypeByMetadataName(TypeMetadataName);

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, typeType, bindingFlagsType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1406 when a <c>System.Type</c> member lookup passes a constant <c>BindingFlags</c> that includes <c>NonPublic</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="typeType">The resolved <c>System.Type</c> type, or <see langword="null"/> when absent.</param>
    /// <param name="bindingFlagsType">The resolved <c>System.Reflection.BindingFlags</c> type, or <see langword="null"/> when absent.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol? typeType, INamedTypeSymbol? bindingFlagsType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.GetMethod/.GetField/.../.InvokeMember(...)' call carrying at least
        // one argument. Anything else is rejected before the semantic model is touched.
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !IsMemberLookupName(memberAccess.Name.Identifier.ValueText)
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation call
            || !SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, typeType)
            || !HasNonPublicFlagsArgument(context.SemanticModel, call, bindingFlagsType, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.NonPublicReflection,
            invocation.SyntaxTree,
            invocation.Span,
            "Type." + call.TargetMethod.Name));
    }

    /// <summary>Returns whether a simple method name is one of <c>System.Type</c>'s member-lookup methods.</summary>
    /// <param name="name">The invoked simple method name.</param>
    /// <returns><see langword="true"/> when the name is a candidate lookup for binding.</returns>
    private static bool IsMemberLookupName(string name) => MemberLookupNames.Contains(name);

    /// <summary>Returns whether the call's <c>BindingFlags</c> argument is a constant that includes the <c>NonPublic</c> bit.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="call">The bound member-lookup call.</param>
    /// <param name="bindingFlagsType">The resolved <c>BindingFlags</c> type, or <see langword="null"/> when absent.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a constant <c>BindingFlags</c> argument carries <c>NonPublic</c>.</returns>
    private static bool HasNonPublicFlagsArgument(SemanticModel model, IInvocationOperation call, INamedTypeSymbol? bindingFlagsType, CancellationToken cancellationToken)
    {
        var arguments = call.Arguments;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];

            // A null parameter (an argument that binds to nothing) folds into the type-mismatch path.
            if (!SymbolEqualityComparer.Default.Equals(argument.Parameter?.Type, bindingFlagsType))
            {
                continue;
            }

            // A flags value assembled at run time has no constant and is out of scope; Convert.ToInt64 treats
            // a null constant as zero, so a matched BindingFlags argument never needs a separate null guard.
            var constant = model.GetConstantValue(argument.Value.Syntax, cancellationToken);
            if (constant.HasValue
                && (Convert.ToInt64(constant.Value, CultureInfo.InvariantCulture) & GetNonPublicBit(bindingFlagsType!)) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads the numeric value of the <c>BindingFlags.NonPublic</c> member from the resolved enum.</summary>
    /// <param name="bindingFlagsType">The resolved <c>BindingFlags</c> type (never <see langword="null"/> at this point).</param>
    /// <returns>The <c>NonPublic</c> bit as a mask.</returns>
    private static long GetNonPublicBit(INamedTypeSymbol bindingFlagsType)
    {
        // NonPublic is a permanent public member of the enum, so it resolves whenever the enum itself does.
        var nonPublicField = (IFieldSymbol)bindingFlagsType.GetMembers(NonPublicFieldName)[0];
        return Convert.ToInt64(nonPublicField.ConstantValue, CultureInfo.InvariantCulture);
    }
}
