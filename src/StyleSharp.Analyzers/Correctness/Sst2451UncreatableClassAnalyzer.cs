// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a class whose constructors only the type itself can call when no member of the type ever
/// creates an instance (SST2451).
/// </summary>
/// <remarks>
/// <para>
/// A class with only private constructors has taken sole responsibility for creating its instances.
/// The rule looks for the member that discharges that responsibility — a <c>new</c> expression anywhere
/// in the type's declaration, including static initializers, methods, lambdas, local functions, and
/// the members of nested types — and reports the class when there is none. A nested type deriving from
/// the class also counts: instances of the subclass chain the private constructor.
/// </para>
/// <para>Several shapes mean "created elsewhere" rather than "never created", and none of them is reported:</para>
/// <list type="bullet">
/// <item><description>A <b>static or abstract class</b> — neither expects direct instantiation.</description></item>
/// <item><description>A <b>partial class</b> — the creation may live in a part this declaration cannot see.</description></item>
/// <item><description>An <b>open class with private-protected constructors</b> — a derived class anywhere
/// in the assembly can chain them. On a sealed class, private protected collapses to private and the
/// class is analyzed.</description></item>
/// <item><description>A constructor carrying an <b>attribute whose name ends in <c>Constructor</c></b>
/// (a deserializer's designated entry point), or one with the
/// <c>(SerializationInfo, StreamingContext)</c> deserialization shape — the type is materialized by a
/// framework, not by a <c>new</c> expression it could contain.</description></item>
/// </list>
/// <para>
/// Creation by reflection from outside the type is invisible to this analysis and out of scope: a type
/// built that way should either mark the constructor it exposes (see above) or keep an accessible one.
/// </para>
/// <para>
/// The clean path binds nothing. Constructor accessibility is decided from modifiers alone, and most
/// classes leave at the first non-private constructor. Only an all-private-constructor class walks its
/// declaration, and only a <c>new</c> expression or base-list entry that could name the type is bound
/// to confirm the match.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2451UncreatableClassAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The suffix of an attribute name that designates a constructor for a framework.</summary>
    private const string ConstructorAttributeSuffix = "Constructor";

    /// <summary>The written-out form of the designated-constructor attribute suffix.</summary>
    private const string ConstructorAttributeFullSuffix = "ConstructorAttribute";

    /// <summary>The parameter type that marks a deserialization constructor.</summary>
    private const string SerializationInfoTypeName = "SerializationInfo";

    /// <summary>The second parameter type of a deserialization constructor.</summary>
    private const string StreamingContextTypeName = "StreamingContext";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.UncreatableClass);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    /// <summary>Analyzes one class declaration for constructors nobody can ever run.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        if (!TryGetSealed(declaration, out var isSealed)
            || !HasOnlySelfCallableConstructors(declaration, isSealed))
        {
            return;
        }

        var scan = new CreationScan(context, declaration, declaration.Identifier.ValueText);
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, CreationScan>(declaration, ref scan, VisitCandidate);
        if (scan.Found)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.UncreatableClass,
            declaration.Identifier.GetLocation(),
            declaration.Identifier.ValueText));
    }

    /// <summary>Reads the class modifiers that decide whether the class is analyzed at all.</summary>
    /// <param name="declaration">The class declaration.</param>
    /// <param name="isSealed">Set to whether the class is sealed.</param>
    /// <returns><see langword="false"/> when the class is static, abstract, or partial, which ends the analysis.</returns>
    /// <remarks>
    /// A static class has no instances to create; an abstract class is instantiated through its
    /// subclasses; and a partial class may keep its creation in a part this declaration cannot see.
    /// </remarks>
    private static bool TryGetSealed(ClassDeclarationSyntax declaration, out bool isSealed)
    {
        isSealed = false;
        var modifiers = declaration.Modifiers;
        for (var i = 0; i < modifiers.Count; i++)
        {
            var kind = modifiers[i].Kind();
            if (kind is SyntaxKind.StaticKeyword or SyntaxKind.AbstractKeyword or SyntaxKind.PartialKeyword)
            {
                return false;
            }

            if (kind == SyntaxKind.SealedKeyword)
            {
                isSealed = true;
            }
        }

        return true;
    }

    /// <summary>Returns whether every declared instance constructor is callable only from inside the type.</summary>
    /// <param name="declaration">The class declaration.</param>
    /// <param name="isSealed">Whether the class is sealed.</param>
    /// <returns><see langword="true"/> when at least one instance constructor exists and none is reachable from outside.</returns>
    /// <remarks>
    /// One pass over the direct members: constructors are never nested deeper. A class with no declared
    /// instance constructor keeps its implicit public one and is not a candidate.
    /// </remarks>
    private static bool HasOnlySelfCallableConstructors(ClassDeclarationSyntax declaration, bool isSealed)
    {
        var found = false;
        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is not ConstructorDeclarationSyntax constructor
                || ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword))
            {
                continue;
            }

            if (!IsSelfOnlyAccessibility(constructor.Modifiers, isSealed) || IsFrameworkCreated(constructor))
            {
                return false;
            }

            found = true;
        }

        return found;
    }

    /// <summary>Returns whether a constructor's accessibility limits its callers to the type itself.</summary>
    /// <param name="modifiers">The constructor's modifiers.</param>
    /// <param name="isSealed">Whether the declaring class is sealed.</param>
    /// <returns><see langword="true"/> for private — explicit or by default — and for private protected on a sealed class.</returns>
    /// <remarks>
    /// A private-protected constructor can also be chained by a derived class in the same assembly, which
    /// may live in any file; only a sealed class, which forbids the derived class, reduces it to private.
    /// </remarks>
    private static bool IsSelfOnlyAccessibility(SyntaxTokenList modifiers, bool isSealed)
    {
        var hasPrivate = false;
        var hasProtected = false;
        for (var i = 0; i < modifiers.Count; i++)
        {
            var kind = modifiers[i].Kind();
            if (kind is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword)
            {
                return false;
            }

            if (kind == SyntaxKind.ProtectedKeyword)
            {
                hasProtected = true;
            }
            else if (kind == SyntaxKind.PrivateKeyword)
            {
                hasPrivate = true;
            }
        }

        return !hasProtected || (hasPrivate && isSealed);
    }

    /// <summary>Returns whether a constructor is shaped for a framework to call by reflection.</summary>
    /// <param name="constructor">The constructor.</param>
    /// <returns><see langword="true"/> for a designated-constructor attribute or a deserialization signature.</returns>
    /// <remarks>
    /// Both checks are deliberately name-based and generous: a type a deserializer materializes is created,
    /// just not by a <c>new</c> expression this analysis could find, so staying silent is the safe reading.
    /// </remarks>
    private static bool IsFrameworkCreated(ConstructorDeclarationSyntax constructor)
    {
        var attributeLists = constructor.AttributeLists;
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (GetRightmostIdentifier(attributes[j].Name) is { } name
                    && (name.EndsWith(ConstructorAttributeSuffix, StringComparison.Ordinal)
                        || name.EndsWith(ConstructorAttributeFullSuffix, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }

        var parameters = constructor.ParameterList.Parameters;
        return parameters.Count == 2
            && GetRightmostIdentifier(parameters[0].Type) == SerializationInfoTypeName
            && GetRightmostIdentifier(parameters[1].Type) == StreamingContextTypeName;
    }

    /// <summary>Inspects one descendant for evidence that the type gets created.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once creation is proven, which stops the walk.</returns>
    private static bool VisitCandidate(SyntaxNode node, ref CreationScan state)
    {
        if (node is BaseObjectCreationExpressionSyntax creation)
        {
            if (!IsSelfCreation(creation, ref state))
            {
                return true;
            }
        }
        else if (node is not SimpleBaseTypeSyntax baseType || !IsSelfReference(baseType.Type, ref state))
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Returns whether a <c>new</c> expression creates an instance of the class under analysis.</summary>
    /// <param name="creation">The object creation.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="true"/> when the created type is the class itself.</returns>
    /// <remarks>
    /// A target-typed <c>new</c> names no type, so it always binds. An explicit creation binds when its
    /// type could be the class: a matching rightmost name, or any single identifier — which may be an
    /// alias for the class no name comparison would recognize.
    /// </remarks>
    private static bool IsSelfCreation(BaseObjectCreationExpressionSyntax creation, ref CreationScan state)
    {
        if (creation is ObjectCreationExpressionSyntax explicitCreation
            && !CouldNameTheClass(explicitCreation.Type, state.TypeName))
        {
            return false;
        }

        return state.Context.SemanticModel.GetSymbolInfo(creation, state.Context.CancellationToken).Symbol
                is IMethodSymbol method
            && state.ResolveDeclaredType() is { } declared
            && SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, declared);
    }

    /// <summary>Returns whether a base-list entry derives a nested type from the class under analysis.</summary>
    /// <param name="type">The base-list entry's type.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="true"/> when the entry names the class itself.</returns>
    private static bool IsSelfReference(TypeSyntax type, ref CreationScan state)
        => CouldNameTheClass(type, state.TypeName)
            && state.Context.SemanticModel.GetSymbolInfo(type, state.Context.CancellationToken).Symbol
                is INamedTypeSymbol named
            && state.ResolveDeclaredType() is { } declared
            && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, declared);

    /// <summary>Returns whether a type syntax is worth binding as a possible mention of the class.</summary>
    /// <param name="type">The written type.</param>
    /// <param name="typeName">The class's name.</param>
    /// <returns><see langword="true"/> for a matching rightmost name, or for a bare identifier that may be an alias.</returns>
    private static bool CouldNameTheClass(TypeSyntax type, string typeName)
        => type is IdentifierNameSyntax || GetRightmostIdentifier(type) == typeName;

    /// <summary>Gets the rightmost identifier of a written type name.</summary>
    /// <param name="type">The written type, or <see langword="null"/>.</param>
    /// <returns>The identifier text, or <see langword="null"/> for a type no simple name ends.</returns>
    private static string? GetRightmostIdentifier(TypeSyntax? type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetRightmostIdentifier(qualified.Right),
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>The state threaded through a class's creation scan.</summary>
    /// <param name="Context">The syntax node context.</param>
    /// <param name="Declaration">The class being analyzed.</param>
    /// <param name="TypeName">The class's name.</param>
    private record struct CreationScan(SyntaxNodeAnalysisContext Context, ClassDeclarationSyntax Declaration, string TypeName)
    {
        /// <summary>Gets or sets a value indicating whether creation of the class was found.</summary>
        public bool Found { get; set; }

        /// <summary>Gets or sets a value indicating whether the class symbol has been resolved yet.</summary>
        private bool Resolved { get; set; }

        /// <summary>Gets or sets the resolved class symbol.</summary>
        private INamedTypeSymbol? DeclaredType { get; set; }

        /// <summary>Resolves the class's symbol, binding it at most once per class.</summary>
        /// <returns>The class symbol, or <see langword="null"/> when it does not bind.</returns>
        public INamedTypeSymbol? ResolveDeclaredType()
        {
            if (Resolved)
            {
                return DeclaredType;
            }

            Resolved = true;
            DeclaredType = Context.SemanticModel.GetDeclaredSymbol(Declaration, Context.CancellationToken);
            return DeclaredType;
        }
    }
}
