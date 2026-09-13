// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags private static readonly <c>Dictionary&lt;K,V&gt;</c>/<c>HashSet&lt;T&gt;</c> fields that
/// are initialized inline and only ever read by their containing type (PSH1114), where
/// <c>FrozenDictionary</c>/<c>FrozenSet</c> trade one-time construction cost for faster
/// lookups. The whole rule is gated on <c>System.Collections.Frozen</c> existing in the
/// compilation (.NET 8+). The usage scan is a whitelist — reads through <c>ContainsKey</c>,
/// <c>TryGetValue</c>, <c>Contains</c>, <c>Count</c>, an element read, or a foreach — and any
/// other mention (mutation, escape as an argument or return value, <c>Keys</c>/<c>Values</c>)
/// keeps the field clean. Partial types are skipped because another part could mutate the field.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1114FreezeStaticLookupsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The dictionary simple type name the syntax gate accepts.</summary>
    internal const string DictionaryTypeName = "Dictionary";

    /// <summary>The hash set simple type name the syntax gate accepts.</summary>
    internal const string HashSetTypeName = "HashSet";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CollectionRules.FreezeStaticLookups);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var types = new LookupTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeField(nodeContext, types), SyntaxKind.FieldDeclaration);
        });
    }

    /// <summary>Returns the rightmost generic name of a type syntax when it is Dictionary or HashSet.</summary>
    /// <param name="type">The declared field type syntax.</param>
    /// <returns>The matching generic name, or <see langword="null"/>.</returns>
    internal static GenericNameSyntax? TryGetLookupTypeName(TypeSyntax type)
    {
        var name = type;
        while (true)
        {
            switch (name)
            {
                case QualifiedNameSyntax qualified:
                {
                    name = qualified.Right;
                    continue;
                }

                case AliasQualifiedNameSyntax aliasQualified:
                {
                    name = aliasQualified.Name;
                    continue;
                }

                case GenericNameSyntax generic:
                    return generic.Identifier.ValueText is DictionaryTypeName or HashSetTypeName ? generic : null;
                default:
                    return null;
            }
        }
    }

    /// <summary>Reports PSH1114 for a private static readonly lookup field that is only ever read.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The lookup definitions resolved only after a syntax match.</param>
    private static void AnalyzeField(in SyntaxNodeAnalysisContext context, LookupTypes types)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        if (!HasCandidateShape(field)
            || TryGetLookupTypeName(field.Declaration.Type) is not { } typeName
            || field.Parent is not TypeDeclarationSyntax containingType
            || containingType.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return;
        }

        // The syntax-only usage scan runs before the type bind: a mutated or escaping field —
        // the common clean shape — bails here without ever touching the semantic model.
        var variable = field.Declaration.Variables[0];
        var scan = new UsageScan(variable.Identifier.ValueText, variable.Identifier.SpanStart);
        _ = DescendantTraversalHelper.VisitDescendantTokens(containingType, ref scan, static (in SyntaxToken token, ref UsageScan state) => state.Visit(in token));
        if (!scan.OnlyReads)
        {
            return;
        }

        var isDictionary = typeName.Identifier.ValueText == DictionaryTypeName;
        if (!MatchesLookupType(context, field.Declaration.Type, isDictionary, types))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.FreezeStaticLookups,
            variable.Identifier.GetLocation(),
            isDictionary ? "Dictionary" : "Set",
            variable.Identifier.ValueText));
    }

    /// <summary>Checks framework availability and binds the lookup after its syntax-only usage scan succeeds.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="type">The declared field type.</param>
    /// <param name="isDictionary">Whether the syntax names a dictionary rather than a hash set.</param>
    /// <param name="types">The cached framework lookup definitions.</param>
    /// <returns>Whether the field binds to the expected lookup and frozen collections are available.</returns>
    private static bool MatchesLookupType(in SyntaxNodeAnalysisContext context, TypeSyntax type, bool isDictionary, LookupTypes types)
    {
        var resolved = types.Get();
        if (resolved[0] is null || resolved[1] is not { } dictionaryType || resolved[2] is not { } hashSetType)
        {
            return false;
        }

        var expectedType = isDictionary ? dictionaryType : hashSetType;
        return context.SemanticModel.GetTypeInfo(type, context.CancellationToken).Type is INamedTypeSymbol namedType
            && SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, expectedType);
    }

    /// <summary>Returns whether a field is a private static readonly single variable with an initializer.</summary>
    /// <param name="field">The field declaration.</param>
    /// <returns><see langword="true"/> when the candidate shape matches.</returns>
    private static bool HasCandidateShape(FieldDeclarationSyntax field) =>
        field.Declaration.Variables.Count == 1
            && field.Declaration.Variables[0].Initializer is not null
            && HasPrivateStaticReadonlyShape(field);

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

    /// <summary>Resolves lookup definitions on demand and caches missing references too.</summary>
    /// <param name="compilation">The compilation whose symbols are cached.</param>
    private sealed class LookupTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the frozen dictionary factory class the rule is gated on.</summary>
        private const string FrozenDictionaryMetadataName = "System.Collections.Frozen.FrozenDictionary";

        /// <summary>The metadata name of the dictionary type.</summary>
        private const string DictionaryMetadataName = "System.Collections.Generic.Dictionary`2";

        /// <summary>The metadata name of the hash set type.</summary>
        private const string HashSetMetadataName = "System.Collections.Generic.HashSet`1";

        /// <summary>The published result; null until a candidate needs the types.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the definitions, allowing equivalent concurrent first resolutions.</summary>
        /// <returns>The frozen dictionary factory, dictionary, and hash set types, each null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol?[] Get() => _resolved ??=
        [
            compilation.GetTypeByMetadataName(FrozenDictionaryMetadataName),
            compilation.GetTypeByMetadataName(DictionaryMetadataName),
            compilation.GetTypeByMetadataName(HashSetMetadataName),
        ];
    }

    /// <summary>Token-visitor state that whitelists read-only usages of one field name.</summary>
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
            OnlyReads = true;
        }

        /// <summary>Gets a value indicating whether every usage seen so far is a whitelisted read.</summary>
        public bool OnlyReads { get; private set; }

        /// <summary>Classifies one token; stops the walk on the first non-read usage.</summary>
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

            if (IsWhitelistedRead(identifier))
            {
                return true;
            }

            OnlyReads = false;
            return false;
        }

        /// <summary>Returns whether an identifier occurrence is a whitelisted read of the lookup.</summary>
        /// <param name="identifier">The identifier occurrence.</param>
        /// <returns><see langword="true"/> for known read-only member calls, element reads, and foreach sources.</returns>
        private static bool IsWhitelistedRead(IdentifierNameSyntax identifier)
        {
            // A qualified reference (Type.Field) puts the field on the right of the inner
            // member access; the usage to classify is then that whole access.
            var usage = identifier.Parent is MemberAccessExpressionSyntax qualification && qualification.Name == identifier
                ? qualification
                : (SyntaxNode)identifier;

            return usage.Parent switch
            {
                MemberAccessExpressionSyntax member => IsWhitelistedMemberAccess(member, usage),
                ElementAccessExpressionSyntax elementAccess when elementAccess.Expression == usage
                    => elementAccess.Parent is not AssignmentExpressionSyntax assignment || assignment.Left != elementAccess,
                ForEachStatementSyntax forEach => forEach.Expression == usage,
                ForEachVariableStatementSyntax forEachVariable => forEachVariable.Expression == usage,
                _ => false,
            };
        }

        /// <summary>Returns whether a member access on the lookup is a whitelisted read member.</summary>
        /// <param name="member">The member access whose receiver is the lookup.</param>
        /// <param name="usage">The lookup usage node.</param>
        /// <returns><see langword="true"/> for known read-only members.</returns>
        private static bool IsWhitelistedMemberAccess(MemberAccessExpressionSyntax member, SyntaxNode usage)
        {
            if (member.Expression != usage)
            {
                return false;
            }

            return member.Parent is InvocationExpressionSyntax
                ? member.Name.Identifier.ValueText is "ContainsKey" or "TryGetValue" or "Contains" or "GetEnumerator"
                : member.Name.Identifier.ValueText == "Count";
        }
    }
}
