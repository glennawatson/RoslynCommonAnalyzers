// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an Entity Framework entity whose primary key property is typed <c>DateTime</c> or
/// <c>DateTimeOffset</c> (SST2475). A temporal primary key is a correctness and robustness defect: two rows
/// created in the same tick collide on the key, the value is not a stable identifier, a temporal clustered key
/// orders the table by insertion time rather than identity, and the value round-trips imprecisely across
/// providers that store <c>DateTime</c> and <c>DateTimeOffset</c> at different resolutions.
/// </summary>
/// <remarks>
/// <para>
/// The primary key is found by one of two signals. The explicit one is a property carrying the
/// data-annotations <c>KeyAttribute</c>; it is preferred because it states the intent outright. The
/// conventional one is the framework's naming rule — a public read-write property named <c>Id</c> or
/// <c>&lt;TypeName&gt;Id</c> — and it is applied only to a type that is actually used as an entity, meaning it
/// appears as the element type of a <c>DbSet&lt;T&gt;</c> on a <c>DbContext</c>-derived type. That membership
/// requirement keeps the convention path at near-zero false positives: a stray timestamp property named
/// <c>Id</c> on an ordinary class is never reported.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on the framework being referenced: it registers nothing unless
/// the <c>KeyAttribute</c> resolves (for the explicit path) or Entity Framework's <c>DbContext</c>/<c>DbSet</c>
/// resolves (for the convention path). A project that references neither pays nothing. The entity set is walked
/// once per compilation over the source assembly's own types; the per-property callback then does a cheap type
/// check and returns before any allocation for every non-temporal property.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2475TemporalPrimaryKeyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the explicit key attribute.</summary>
    private const string KeyAttributeMetadataName = "System.ComponentModel.DataAnnotations.KeyAttribute";

    /// <summary>The metadata name of the Entity Framework context base type.</summary>
    private const string DbContextMetadataName = "Microsoft.EntityFrameworkCore.DbContext";

    /// <summary>The metadata name of the Entity Framework entity-set type.</summary>
    private const string DbSetMetadataName = "Microsoft.EntityFrameworkCore.DbSet`1";

    /// <summary>The metadata name of the offset-bearing temporal type.</summary>
    private const string DateTimeOffsetMetadataName = "System.DateTimeOffset";

    /// <summary>The conventional bare key name, and the suffix of the <c>&lt;TypeName&gt;Id</c> form.</summary>
    private const string ConventionKeyName = "Id";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.TemporalPrimaryKey);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the per-property check only when the key or the framework is referenced.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var compilation = context.Compilation;
        var keyAttribute = compilation.GetTypeByMetadataName(KeyAttributeMetadataName);
        var dbContext = compilation.GetTypeByMetadataName(DbContextMetadataName);
        var dbSet = compilation.GetTypeByMetadataName(DbSetMetadataName);
        var frameworkReferenced = dbContext is not null || dbSet is not null;

        if (keyAttribute is null && !frameworkReferenced)
        {
            return;
        }

        var dateTimeOffset = compilation.GetTypeByMetadataName(DateTimeOffsetMetadataName);
        var entityTypes = dbContext is not null && dbSet is not null
            ? CollectEntityTypes(compilation, dbContext, dbSet)
            : null;

        var facts = new TemporalKeyFacts(keyAttribute, dateTimeOffset, entityTypes);
        context.RegisterSymbolAction(symbolContext => AnalyzeProperty(symbolContext, facts), SymbolKind.Property);
    }

    /// <summary>Reports a temporal primary key found by the explicit attribute or the entity convention.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="facts">The resolved key attribute, offset type, and entity set.</param>
    private static void AnalyzeProperty(SymbolAnalysisContext context, TemporalKeyFacts facts)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (!TryGetTemporalTypeName(property.Type, facts.DateTimeOffset, out var temporalName))
        {
            return;
        }

        if (facts.KeyAttribute is not null && CarriesKeyAttribute(property, facts.KeyAttribute))
        {
            Report(context, property, temporalName);
            return;
        }

        if (facts.EntityTypes is null
            || !IsConventionKey(property)
            || !facts.EntityTypes.Contains(property.ContainingType))
        {
            return;
        }

        Report(context, property, temporalName);
    }

    /// <summary>Reports the temporal primary key at its declaration.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="property">The reported key property.</param>
    /// <param name="temporalName">The temporal type's display name.</param>
    private static void Report(SymbolAnalysisContext context, IPropertySymbol property, string temporalName)
        => context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.TemporalPrimaryKey,
            property.Locations[0],
            property.Name,
            temporalName));

    /// <summary>Returns whether a property's type is a temporal type, unwrapping <see cref="Nullable{T}"/>.</summary>
    /// <param name="type">The property's type.</param>
    /// <param name="dateTimeOffset">The resolved offset type, or <see langword="null"/> when absent.</param>
    /// <param name="temporalName">The matched temporal type's display name when the method returns true.</param>
    /// <returns><see langword="true"/> when the type is <c>DateTime</c>, <c>DateTimeOffset</c>, or their nullable forms.</returns>
    private static bool TryGetTemporalTypeName(ITypeSymbol type, INamedTypeSymbol? dateTimeOffset, out string temporalName)
    {
        var underlying = type;
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: { Length: 1 } arguments })
        {
            underlying = arguments[0];
        }

        if (underlying.SpecialType == SpecialType.System_DateTime
            || (dateTimeOffset is not null && SymbolEqualityComparer.Default.Equals(underlying, dateTimeOffset)))
        {
            temporalName = underlying.Name;
            return true;
        }

        temporalName = string.Empty;
        return false;
    }

    /// <summary>Returns whether a property carries the explicit key attribute.</summary>
    /// <param name="property">The candidate key property.</param>
    /// <param name="keyAttribute">The resolved key attribute type.</param>
    /// <returns><see langword="true"/> when one of the property's attributes is the key attribute.</returns>
    private static bool CarriesKeyAttribute(IPropertySymbol property, INamedTypeSymbol keyAttribute)
    {
        var attributes = property.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(attributes[i].AttributeClass, keyAttribute))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a property matches the framework's public read-write key-naming convention.</summary>
    /// <param name="property">The candidate key property.</param>
    /// <returns><see langword="true"/> for a public, instance, read-write property named <c>Id</c> or <c>&lt;TypeName&gt;Id</c>.</returns>
    private static bool IsConventionKey(IPropertySymbol property)
    {
        if (property.DeclaredAccessibility != Accessibility.Public
            || property.IsStatic
            || property.GetMethod is null
            || property.SetMethod is null)
        {
            return false;
        }

        var name = property.Name;
        if (string.Equals(name, ConventionKeyName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var typeName = property.ContainingType.Name;
        return name.Length == typeName.Length + ConventionKeyName.Length
            && name.StartsWith(typeName, StringComparison.Ordinal)
            && name.EndsWith(ConventionKeyName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Collects the source types used as entities through a <c>DbSet&lt;T&gt;</c> on a context.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <param name="dbContext">The resolved context base type.</param>
    /// <param name="dbSet">The resolved entity-set type.</param>
    /// <returns>The set of entity types, compared by symbol identity.</returns>
    private static HashSet<INamedTypeSymbol> CollectEntityTypes(Compilation compilation, INamedTypeSymbol dbContext, INamedTypeSymbol dbSet)
    {
        var entities = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var namespaces = new Stack<INamespaceSymbol>();
        namespaces.Push(compilation.Assembly.GlobalNamespace);

        while (namespaces.Count > 0)
        {
            foreach (var member in namespaces.Pop().GetMembers())
            {
                if (member is INamespaceSymbol childNamespace)
                {
                    namespaces.Push(childNamespace);
                }
                else if (member is INamedTypeSymbol type)
                {
                    CollectFromType(type, dbContext, dbSet, entities);
                }
            }
        }

        return entities;
    }

    /// <summary>Adds a context type's <c>DbSet&lt;T&gt;</c> entities, then recurses into nested types.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="dbContext">The resolved context base type.</param>
    /// <param name="dbSet">The resolved entity-set type.</param>
    /// <param name="entities">The accumulating entity set.</param>
    private static void CollectFromType(INamedTypeSymbol type, INamedTypeSymbol dbContext, INamedTypeSymbol dbSet, HashSet<INamedTypeSymbol> entities)
    {
        if (DerivesFrom(type, dbContext))
        {
            AddDbSetEntities(type, dbSet, entities);
        }

        var nested = type.GetTypeMembers();
        for (var i = 0; i < nested.Length; i++)
        {
            CollectFromType(nested[i], dbContext, dbSet, entities);
        }
    }

    /// <summary>Adds every <c>DbSet&lt;T&gt;</c> element type declared on a context to the entity set.</summary>
    /// <param name="contextType">The context type whose members expose the entity sets.</param>
    /// <param name="dbSet">The resolved entity-set type.</param>
    /// <param name="entities">The accumulating entity set.</param>
    private static void AddDbSetEntities(INamedTypeSymbol contextType, INamedTypeSymbol dbSet, HashSet<INamedTypeSymbol> entities)
    {
        var members = contextType.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            var memberType = members[i] switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                _ => null,
            };

            if (memberType is INamedTypeSymbol { IsGenericType: true, TypeArguments: { Length: 1 } arguments } named
                && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, dbSet)
                && arguments[0] is INamedTypeSymbol entity)
            {
                entities.Add(entity);
            }
        }
    }

    /// <summary>Returns whether a type derives from the given base type.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="baseType">The base type to look for.</param>
    /// <returns><see langword="true"/> when <paramref name="baseType"/> appears in the base chain.</returns>
    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The resolved facts one compilation needs to find a temporal primary key.</summary>
    /// <param name="KeyAttribute">The explicit key attribute, or <see langword="null"/> when it is not referenced.</param>
    /// <param name="DateTimeOffset">The offset type, or <see langword="null"/> when it is not referenced.</param>
    /// <param name="EntityTypes">The convention entity set, or <see langword="null"/> when the framework is absent.</param>
    private readonly record struct TemporalKeyFacts(
        INamedTypeSymbol? KeyAttribute,
        INamedTypeSymbol? DateTimeOffset,
        HashSet<INamedTypeSymbol>? EntityTypes);
}
