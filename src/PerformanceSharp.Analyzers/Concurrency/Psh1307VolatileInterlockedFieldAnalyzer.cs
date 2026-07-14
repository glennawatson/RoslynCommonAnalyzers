// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags plain reads and writes of fields that are <c>Interlocked</c> targets elsewhere in
/// the same type (PSH1307), where <c>Volatile.Read</c> and <c>Volatile.Write</c> pair
/// correctly with the interlocked updates. Each type declaration is scanned once: a
/// syntax-only pass collects <c>Interlocked.*(ref field, ...)</c> calls — most types have
/// none and bail with no binding — and only then are the remaining references classified.
/// Constructor and initializer accesses, ref and out arguments, <c>nameof</c> operands, and
/// accesses inside lock statements are not reported. Instance-field reads reached through a
/// readonly <c>this</c> — inside a <c>readonly</c> member or a <c>readonly struct</c> — are
/// not reported either, because <c>Volatile.Read(ref field)</c> cannot take a writable ref
/// through a readonly receiver (CS1605). Every reported access is bound to a field of the
/// containing type whose type has Volatile overloads.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1307VolatileInterlockedFieldAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The receiver type name that seeds the scan.</summary>
    internal const string InterlockedTypeName = "Interlocked";

    /// <summary>The read spelling reported in messages.</summary>
    internal const string VolatileReadSpelling = "Volatile.Read";

    /// <summary>The write spelling reported in messages.</summary>
    internal const string VolatileWriteSpelling = "Volatile.Write";

    /// <summary>The metadata name of the interlocked type.</summary>
    private const string InterlockedMetadataName = "System.Threading.Interlocked";

    /// <summary>The metadata name of the volatile type.</summary>
    private const string VolatileMetadataName = "System.Threading.Volatile";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.VolatileInterlockedField);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var interlockedType = start.Compilation.GetTypeByMetadataName(InterlockedMetadataName);
            if (interlockedType is null || start.Compilation.GetTypeByMetadataName(VolatileMetadataName) is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeType(nodeContext, interlockedType),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration);
        });
    }

    /// <summary>Returns the ref-argument field name of an <c>Interlocked.*(ref x, ...)</c> call, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The targeted name text, or <see langword="null"/> when the shape does not match.</returns>
    internal static string? TryGetInterlockedTargetName(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Expression is not IdentifierNameSyntax { Identifier.ValueText: InterlockedTypeName })
        {
            return null;
        }

        var first = invocation.ArgumentList.Arguments[0];
        if (!first.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword))
        {
            return null;
        }

        return first.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => name.Identifier.ValueText,
            _ => null,
        };
    }

    /// <summary>Classifies a plain access as a read or write for the fix and message.</summary>
    /// <param name="usage">The full usage expression.</param>
    /// <returns><see langword="true"/> for assignment targets and increments.</returns>
    internal static bool IsWriteAccess(ExpressionSyntax usage)
        => usage.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == usage,
            PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax => true,
            _ => false,
        };

    /// <summary>Returns whether a node's nearest enclosing type declaration is the given one.</summary>
    /// <param name="node">The node to test.</param>
    /// <param name="type">The type declaration being scanned.</param>
    /// <returns><see langword="false"/> for nodes inside nested types, which get their own scan.</returns>
    private static bool IsDirectlyInType(SyntaxNode node, TypeDeclarationSyntax type)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is TypeDeclarationSyntax found)
            {
                return found == type;
            }
        }

        return false;
    }

    /// <summary>Scans one type for interlocked targets and reports their plain accesses.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="interlockedType">The interlocked type.</param>
    private static void AnalyzeType(SyntaxNodeAnalysisContext context, INamedTypeSymbol interlockedType)
    {
        var containingType = (TypeDeclarationSyntax)context.Node;
        var seeds = new SeedScan(containingType);
        DescendantTraversalHelper.VisitDescendantTokens(containingType, ref seeds, static (in SyntaxToken token, ref SeedScan state) => state.Visit(in token));
        if (seeds.Calls is not { } calls)
        {
            return;
        }

        // Both scans run on syntax alone before anything binds: a type whose accesses are all
        // Volatile or ref arguments — the common clean shape — exits here without ever
        // touching the semantic model.
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var call in calls)
        {
            candidates.Add(TryGetInterlockedTargetName(call)!);
        }

        var scan = new AccessScan(candidates, containingType);
        DescendantTraversalHelper.VisitDescendantTokens(containingType, ref scan, static (in SyntaxToken token, ref AccessScan state) => state.Visit(in token));
        if (scan.Usages is not { } usages || VerifyTargets(context, calls, interlockedType) is not { } targets)
        {
            return;
        }

        ReportPlainAccesses(context, containingType, usages, targets);
    }

    /// <summary>Binds the interlocked-shaped calls and returns the verified target names.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="calls">The interlocked-shaped calls.</param>
    /// <param name="interlockedType">The interlocked type.</param>
    /// <returns>The verified names, or <see langword="null"/> when no call binds to the runtime's Interlocked.</returns>
    private static HashSet<string>? VerifyTargets(SyntaxNodeAnalysisContext context, List<InvocationExpressionSyntax> calls, INamedTypeSymbol interlockedType)
    {
        HashSet<string>? targets = null;
        foreach (var call in calls)
        {
            var receiver = ((MemberAccessExpressionSyntax)call.Expression).Expression;
            if (context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol is INamedTypeSymbol bound
                && SymbolEqualityComparer.Default.Equals(bound, interlockedType))
            {
                targets ??= new HashSet<string>(StringComparer.Ordinal);
                targets.Add(TryGetInterlockedTargetName(call)!);
            }
        }

        return targets;
    }

    /// <summary>Returns the field name an access spells.</summary>
    /// <param name="usage">The usage expression.</param>
    /// <returns>The identifier text.</returns>
    private static string GetUsageName(ExpressionSyntax usage)
        => usage is MemberAccessExpressionSyntax qualified
            ? qualified.Name.Identifier.ValueText
            : ((IdentifierNameSyntax)usage).Identifier.ValueText;

    /// <summary>Returns whether an instance-field access is reached through a readonly <c>this</c>.</summary>
    /// <param name="containingType">The scanned type declaration.</param>
    /// <param name="usage">The usage expression, always an implicit or explicit <c>this</c> access here.</param>
    /// <returns>
    /// <see langword="true"/> when the enclosing member is <c>readonly</c> or the type is a
    /// <c>readonly struct</c>; in either case <c>ref field</c> — the rule's remedy — will not compile.
    /// </returns>
    private static bool IsThroughReadOnlyThis(TypeDeclarationSyntax containingType, ExpressionSyntax usage)
    {
        // A readonly struct makes 'this' readonly in every instance member; the modifier only
        // binds on a struct, so its bare presence is enough to identify the case.
        if (containingType.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            return true;
        }

        // Otherwise the receiver is readonly only when the specific enclosing member is. A block
        // accessor can carry readonly on the accessor or on the whole property/indexer, so keep
        // walking past a non-readonly member instead of stopping at the first one.
        for (SyntaxNode? current = usage.Parent; current is not null && current != containingType; current = current.Parent)
        {
            if (current is AccessorDeclarationSyntax accessor && accessor.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
            {
                return true;
            }

            if (current is MemberDeclarationSyntax member && member.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports every collected plain access whose field verifies.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="containingType">The scanned type declaration.</param>
    /// <param name="usages">The collected plain accesses.</param>
    /// <param name="targets">The verified interlocked-targeted field names.</param>
    private static void ReportPlainAccesses(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax containingType,
        List<ExpressionSyntax> usages,
        HashSet<string> targets)
    {
        if (context.SemanticModel.GetDeclaredSymbol(containingType, context.CancellationToken) is not { } typeSymbol)
        {
            return;
        }

        foreach (var usage in usages)
        {
            if (!targets.Contains(GetUsageName(usage))
                || context.SemanticModel.GetSymbolInfo(usage, context.CancellationToken).Symbol is not IFieldSymbol field
                || !SymbolEqualityComparer.Default.Equals(field.ContainingType, typeSymbol)
                || !HasVolatileOverload(field.Type)
                || (!field.IsStatic && IsThroughReadOnlyThis(containingType, usage)))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                ConcurrencyRules.VolatileInterlockedField,
                usage.SyntaxTree,
                usage.Span,
                field.Name,
                IsWriteAccess(usage) ? VolatileWriteSpelling : VolatileReadSpelling));
        }
    }

    /// <summary>Returns whether a field type has a matching Volatile.Read/Write overload.</summary>
    /// <param name="type">The field type.</param>
    /// <returns><see langword="true"/> for the primitive overload set and reference types.</returns>
    [SuppressMessage(
        "Critical Code Smell",
        "S1541:Methods and properties should not be too complex",
        Justification = "A flat SpecialType list mirrors the Volatile overload set explicitly instead of relying on enum-value adjacency.")]
    private static bool HasVolatileOverload(ITypeSymbol type)
        => type.IsReferenceType || type.SpecialType is SpecialType.System_Boolean
            or SpecialType.System_Byte or SpecialType.System_SByte
            or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64
            or SpecialType.System_Single or SpecialType.System_Double
            or SpecialType.System_IntPtr or SpecialType.System_UIntPtr;

    /// <summary>Token-visitor state that finds the interlocked-shaped calls of one type.</summary>
    private sealed class SeedScan
    {
        /// <summary>The type being scanned; nested types are excluded.</summary>
        private readonly TypeDeclarationSyntax _containingType;

        /// <summary>Initializes a new instance of the <see cref="SeedScan"/> class.</summary>
        /// <param name="containingType">The type being scanned.</param>
        public SeedScan(TypeDeclarationSyntax containingType) => _containingType = containingType;

        /// <summary>Gets the interlocked-shaped calls found, or <see langword="null"/> when there are none.</summary>
        public List<InvocationExpressionSyntax>? Calls { get; private set; }

        /// <summary>Collects one token.</summary>
        /// <param name="token">The token to inspect.</param>
        /// <returns><see langword="true"/> to keep walking.</returns>
        public bool Visit(in SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken)
                || token.ValueText != InterlockedTypeName
                || token.Parent is not IdentifierNameSyntax identifier
                || identifier.Parent is not MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax invocation } access
                || access.Expression != identifier
                || TryGetInterlockedTargetName(invocation) is null
                || !IsDirectlyInType(invocation, _containingType))
            {
                return true;
            }

            Calls ??= new List<InvocationExpressionSyntax>(capacity: 4);
            Calls.Add(invocation);
            return true;
        }
    }

    /// <summary>Token-visitor state that finds plain accesses of the targeted fields.</summary>
    private sealed class AccessScan
    {
        /// <summary>The interlocked-targeted field names.</summary>
        private readonly HashSet<string> _targets;

        /// <summary>The type bounding lock-ancestry walks; nested types are excluded.</summary>
        private readonly TypeDeclarationSyntax _containingType;

        /// <summary>Initializes a new instance of the <see cref="AccessScan"/> class.</summary>
        /// <param name="targets">The targeted field names.</param>
        /// <param name="containingType">The scanned type declaration.</param>
        public AccessScan(HashSet<string> targets, TypeDeclarationSyntax containingType)
        {
            _targets = targets;
            _containingType = containingType;
        }

        /// <summary>Gets the plain accesses to report, or <see langword="null"/> when there are none.</summary>
        public List<ExpressionSyntax>? Usages { get; private set; }

        /// <summary>Collects one token.</summary>
        /// <param name="token">The token to inspect.</param>
        /// <returns><see langword="true"/> to keep walking.</returns>
        public bool Visit(in SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken)
                || !_targets.Contains(token.ValueText)
                || token.Parent is not IdentifierNameSyntax identifier)
            {
                return true;
            }

            var usage = identifier.Parent is MemberAccessExpressionSyntax qualification && qualification.Name == identifier
                ? (ExpressionSyntax)qualification
                : identifier;
            if (!IsPlainFieldAccess(usage) || IsConstructionOrLockContext(usage) || !IsDirectlyInType(usage, _containingType))
            {
                return true;
            }

            Usages ??= new List<ExpressionSyntax>(capacity: 8);
            Usages.Add(usage);
            return true;
        }

        /// <summary>Returns whether a usage is a plain access rather than a ref argument or nameof operand.</summary>
        /// <param name="usage">The usage expression.</param>
        /// <returns><see langword="true"/> for plain reads and writes.</returns>
        private static bool IsPlainFieldAccess(ExpressionSyntax usage)
        {
            if (usage is MemberAccessExpressionSyntax { Expression: not ThisExpressionSyntax })
            {
                return false;
            }

            if (usage.Parent is ArgumentSyntax argument)
            {
                return argument.RefOrOutKeyword.IsKind(SyntaxKind.None) && !IsNameOfArgument(argument);
            }

            return usage.Parent is not MemberAccessExpressionSyntax;
        }

        /// <summary>Returns whether an argument feeds a <c>nameof</c> expression.</summary>
        /// <param name="argument">The argument to inspect.</param>
        /// <returns><see langword="true"/> for nameof operands.</returns>
        private static bool IsNameOfArgument(ArgumentSyntax argument)
            => argument.Parent is ArgumentListSyntax
            {
                Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } },
            };

        /// <summary>Returns whether a usage sits in construction code or inside a lock.</summary>
        /// <param name="usage">The usage expression.</param>
        /// <returns><see langword="true"/> for constructor, initializer, and locked contexts.</returns>
        private bool IsConstructionOrLockContext(ExpressionSyntax usage)
        {
            for (SyntaxNode? current = usage.Parent; current is not null && current != _containingType; current = current.Parent)
            {
                if (current is LockStatementSyntax or ConstructorDeclarationSyntax or EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax } })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
