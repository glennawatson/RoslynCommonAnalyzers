// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags private static readonly byte arrays built from a u8 literal — directly through
/// <c>"..."u8.ToArray()</c> or a spread like <c>[.. "..."u8]</c> — whose every use reads the
/// data like a span (PSH1013). A <c>static ReadOnlySpan&lt;byte&gt;</c> property returning
/// the literal compiles to a direct pointer into the assembly's data section: no startup
/// allocation and no field indirection. The usage whitelist is <c>Length</c>, element reads,
/// foreach, and arguments bound to <c>ReadOnlySpan&lt;byte&gt;</c> parameters; any other
/// mention keeps the field as is. Partial types are skipped because another part could
/// mutate or escape the array.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1013Utf8SpanPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the span type the property returns.</summary>
    private const string ReadOnlySpanMetadataName = "System.ReadOnlySpan`1";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.UseUtf8SpanProperty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(ReadOnlySpanMetadataName) is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
        });
    }

    /// <summary>Returns the u8 literal a byte-array field initializer is built from, before any binding.</summary>
    /// <param name="initializer">The field initializer value.</param>
    /// <returns>The u8 literal, or <see langword="null"/> when the shape does not match.</returns>
    internal static LiteralExpressionSyntax? TryGetUtf8Source(ExpressionSyntax initializer)
    {
        var candidate = initializer switch
        {
            InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } invocation
                when invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToArray" } access
                => access.Expression,
            CollectionExpressionSyntax { Elements: [SpreadElementSyntax spread] } => spread.Expression,
            _ => null,
        };

        return candidate is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.Utf8StringLiteralExpression)
            ? literal
            : null;
    }

    /// <summary>Returns whether a field declaration is a private static readonly single-variable byte array.</summary>
    /// <param name="field">The field declaration.</param>
    /// <returns><see langword="true"/> when the candidate shape matches.</returns>
    internal static bool HasCandidateShape(FieldDeclarationSyntax field)
        => field.Declaration.Variables.Count == 1
            && field.Declaration.Variables[0].Initializer is not null
            && IsByteArrayType(field.Declaration.Type)
            && HasPrivateStaticReadonlyShape(field);

    /// <summary>Returns whether a type syntax is a single-dimensional byte array.</summary>
    /// <param name="type">The declared field type.</param>
    /// <returns><see langword="true"/> for <c>byte[]</c>.</returns>
    private static bool IsByteArrayType(TypeSyntax type)
        => type is ArrayTypeSyntax { RankSpecifiers: [{ Rank: 1 }] } array
            && array.ElementType is PredefinedTypeSyntax predefined
            && predefined.Keyword.IsKind(SyntaxKind.ByteKeyword);

    /// <summary>Returns whether a field is private (explicitly or by default), static, and readonly.</summary>
    /// <param name="field">The field declaration.</param>
    /// <returns><see langword="true"/> when the modifier shape matches.</returns>
    private static bool HasPrivateStaticReadonlyShape(FieldDeclarationSyntax field)
    {
        var modifiers = field.Modifiers;
        if (!modifiers.Any(SyntaxKind.StaticKeyword) || !modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            return false;
        }

        for (var i = 0; i < modifiers.Count; i++)
        {
            var kind = modifiers[i].Kind();
            if (kind is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword or SyntaxKind.ProtectedKeyword)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reports PSH1013 for a u8-built array field whose uses all read like a span.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        if (!HasCandidateShape(field)
            || TryGetUtf8Source(field.Declaration.Variables[0].Initializer!.Value) is null
            || field.Parent is not TypeDeclarationSyntax containingType
            || containingType.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return;
        }

        var variable = field.Declaration.Variables[0];
        var scan = new UsageScan(variable.Identifier.ValueText, variable.Identifier.SpanStart);
        DescendantTraversalHelper.VisitDescendantTokens(containingType, ref scan, static (in SyntaxToken token, ref UsageScan state) => state.Visit(in token));
        if (!scan.OnlySpanReads || !ArgumentsBindToSpans(context, scan.ArgumentUsages))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.UseUtf8SpanProperty,
            variable.Identifier.GetLocation(),
            variable.Identifier.ValueText));
    }

    /// <summary>Returns whether every collected argument usage binds to a <c>ReadOnlySpan&lt;byte&gt;</c> parameter.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="argumentUsages">The argument usages to verify.</param>
    /// <returns><see langword="true"/> when the span property would still compile at every argument.</returns>
    private static bool ArgumentsBindToSpans(SyntaxNodeAnalysisContext context, List<ArgumentSyntax>? argumentUsages)
    {
        if (argumentUsages is null)
        {
            return true;
        }

        foreach (var argument in argumentUsages)
        {
            if (argument.Parent is not ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } argumentList
                || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
            {
                return false;
            }

            var index = argumentList.Arguments.IndexOf(argument);
            if (index >= method.Parameters.Length || !IsReadOnlyByteSpan(method.Parameters[index].Type))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a type is <c>ReadOnlySpan&lt;byte&gt;</c>.</summary>
    /// <param name="type">The parameter type.</param>
    /// <returns><see langword="true"/> for the read-only byte span.</returns>
    private static bool IsReadOnlyByteSpan(ITypeSymbol type)
        => type is INamedTypeSymbol
        {
            Name: "ReadOnlySpan",
            IsGenericType: true,
            TypeArguments: [{ SpecialType: SpecialType.System_Byte }],
            ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
        };

    /// <summary>Token-visitor state that whitelists span-compatible reads of one field name.</summary>
    private sealed class UsageScan
    {
        /// <summary>The field name being tracked.</summary>
        private readonly string _name;

        /// <summary>The declarator identifier's position, excluded from the scan.</summary>
        private readonly int _declaratorStart;

        /// <summary>Initializes a new instance of the <see cref="UsageScan"/> class.</summary>
        /// <param name="name">The field name to track.</param>
        /// <param name="declaratorStart">The declarator identifier's position.</param>
        public UsageScan(string name, int declaratorStart)
        {
            _name = name;
            _declaratorStart = declaratorStart;
            OnlySpanReads = true;
        }

        /// <summary>Gets a value indicating whether every usage seen so far reads like a span.</summary>
        public bool OnlySpanReads { get; private set; }

        /// <summary>Gets the argument usages needing semantic verification, when any.</summary>
        public List<ArgumentSyntax>? ArgumentUsages { get; private set; }

        /// <summary>Classifies one token; stops the walk on the first incompatible usage.</summary>
        /// <param name="token">The token to inspect.</param>
        /// <returns><see langword="true"/> to keep walking.</returns>
        public bool Visit(in SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken)
                || token.SpanStart == _declaratorStart
                || token.ValueText != _name
                || token.Parent is not IdentifierNameSyntax identifier)
            {
                return true;
            }

            var usage = identifier.Parent is MemberAccessExpressionSyntax qualification && qualification.Name == identifier
                ? (SyntaxNode)qualification
                : identifier;
            if (IsWhitelistedRead(usage, out var argument))
            {
                if (argument is not null)
                {
                    ArgumentUsages ??= new List<ArgumentSyntax>(capacity: 4);
                    ArgumentUsages.Add(argument);
                }

                return true;
            }

            OnlySpanReads = false;
            return false;
        }

        /// <summary>Returns whether a usage reads the field in a span-compatible way.</summary>
        /// <param name="usage">The usage node.</param>
        /// <param name="argument">The argument usage needing semantic verification, when applicable.</param>
        /// <returns><see langword="true"/> for Length reads, element reads, foreach sources, and plain arguments.</returns>
        private static bool IsWhitelistedRead(SyntaxNode usage, out ArgumentSyntax? argument)
        {
            argument = null;
            switch (usage.Parent)
            {
                case MemberAccessExpressionSyntax member when member.Expression == usage:
                    return member.Name.Identifier.ValueText == "Length";
                case ElementAccessExpressionSyntax element when element.Expression == usage:
                    return element.Parent is not AssignmentExpressionSyntax assignment || assignment.Left != element;
                case ForEachStatementSyntax forEach:
                    return forEach.Expression == usage;
                case ArgumentSyntax { RefOrOutKeyword.RawKind: (int)SyntaxKind.None } candidate:
                {
                    argument = candidate;
                    return true;
                }

                default:
                    return false;
            }
        }
    }
}
