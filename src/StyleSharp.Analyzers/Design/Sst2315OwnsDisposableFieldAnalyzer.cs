// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a type that creates and keeps a disposable but is not <c>IDisposable</c>/<c>IAsyncDisposable</c>
/// (SST2315). The type owns a resource whose only release point is a disposal method it does not have, so
/// nothing — no owner, no container, no <c>using</c> — ever cleans it up.
/// </summary>
/// <remarks>
/// <para>
/// This reports only the ownership shapes a plain <c>new</c> on a field does not cover: a field or
/// auto-property whose initializer is a static factory call (<c>File.OpenRead</c>, a <c>Create</c> call, an
/// extension such as <c>CreateScope</c>); an auto-property initialized with <c>new</c>; and a collection
/// the type creates and then fills with newly created disposable elements. A member assigned from a
/// constructor parameter is injected and left alone — the caller owns it. A field initialized directly with
/// <c>new</c>, and an instance call on an injected dependency, are left alone too.
/// </para>
/// <para>
/// The prepass, in order and each free before the next: the type is a class or struct that does not already
/// implement a disposal interface. Only then are its members' initializers examined, and the collection
/// proof scans the declaration only for a type that reached it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2315OwnsDisposableFieldAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key naming the sync-disposable members a code fix should release.</summary>
    internal const string MembersToDisposeKey = "MembersToDispose";

    /// <summary>How a member owns a disposable, for reporting and fix eligibility.</summary>
    private enum Ownership
    {
        /// <summary>The member does not own a disposable this rule reports.</summary>
        None,

        /// <summary>The member owns a sync-disposable a generated <c>Dispose()</c> can release.</summary>
        Fixable,

        /// <summary>The member owns a disposable, but the fix is a design decision (a collection, or async-only).</summary>
        NotFixable,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.OwnsDisposableField);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (DisposableTypes.Create(start.Compilation) is not { } types)
            {
                return;
            }

            var collectionInterface = start.Compilation.GetTypeByMetadataName("System.Collections.Generic.ICollection`1");
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, types, collectionInterface),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration);
        });
    }

    /// <summary>Analyzes one type declaration for unadvertised ownership of a disposable.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The disposal types resolved for this compilation.</param>
    /// <param name="collectionInterface">The unbound <c>ICollection&lt;T&gt;</c> interface, if resolved.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, in DisposableTypes types, INamedTypeSymbol? collectionInterface)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } type
            || type.IsRefLikeType
            || types.ImplementsDisposable(type))
        {
            return;
        }

        var scan = new OwnershipScan(context, types, collectionInterface, declaration);
        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            CollectMember(ref scan, members[i]);
        }

        if (scan.FirstOwned is null)
        {
            return;
        }

        var members2 = scan.AllFixable && scan.DisposeMembers is { Count: > 0 } ? string.Join(",", scan.DisposeMembers) : string.Empty;
        var properties = ImmutableDictionary<string, string?>.Empty.Add(MembersToDisposeKey, members2);
        context.ReportDiagnostic(Diagnostic.Create(DesignRules.OwnsDisposableField, declaration.Identifier.GetLocation(), properties, type.Name, scan.FirstOwned));
    }

    /// <summary>Classifies one type member and records any disposable it owns.</summary>
    /// <param name="scan">The ownership scan state.</param>
    /// <param name="member">The member declaration.</param>
    private static void CollectMember(ref OwnershipScan scan, MemberDeclarationSyntax member)
    {
        switch (member)
        {
            case FieldDeclarationSyntax field when !field.Modifiers.Any(SyntaxKind.StaticKeyword) && !field.Modifiers.Any(SyntaxKind.ConstKeyword):
            {
                var variables = field.Declaration.Variables;
                for (var i = 0; i < variables.Count; i++)
                {
                    RecordField(ref scan, variables[i]);
                }

                break;
            }

            case PropertyDeclarationSyntax property when !property.Modifiers.Any(SyntaxKind.StaticKeyword):
            {
                RecordProperty(ref scan, property);
                break;
            }
        }
    }

    /// <summary>Records ownership through a field declarator.</summary>
    /// <param name="scan">The ownership scan state.</param>
    /// <param name="variable">The field declarator.</param>
    private static void RecordField(ref OwnershipScan scan, VariableDeclaratorSyntax variable)
    {
        if (variable.Initializer?.Value is not { } initializer
            || scan.Context.SemanticModel.GetDeclaredSymbol(variable, scan.Context.CancellationToken) is not IFieldSymbol field)
        {
            return;
        }

        var ownership = IsOwnedDisposableCollection(ref scan, field, initializer)
            ? Ownership.NotFixable
            : ClassifyInitializer(ref scan, field.Type, initializer, allowNewObject: false);

        scan.Record(field.Name, ownership);
    }

    /// <summary>Records ownership through an auto-property.</summary>
    /// <param name="scan">The ownership scan state.</param>
    /// <param name="property">The property declaration.</param>
    private static void RecordProperty(ref OwnershipScan scan, PropertyDeclarationSyntax property)
    {
        if (property.Initializer?.Value is not { } initializer
            || !IsAutoProperty(property)
            || scan.Context.SemanticModel.GetDeclaredSymbol(property, scan.Context.CancellationToken) is not { } symbol)
        {
            return;
        }

        scan.Record(symbol.Name, ClassifyInitializer(ref scan, symbol.Type, initializer, allowNewObject: true));
    }

    /// <summary>Classifies a member initializer that hands the member a disposable.</summary>
    /// <param name="scan">The ownership scan state.</param>
    /// <param name="memberType">The member's type.</param>
    /// <param name="initializer">The initializer expression.</param>
    /// <param name="allowNewObject">Whether a direct <c>new</c> counts as ownership (auto-properties) or not (fields).</param>
    /// <returns>How the initializer owns a disposable.</returns>
    private static Ownership ClassifyInitializer(ref OwnershipScan scan, ITypeSymbol memberType, ExpressionSyntax initializer, bool allowNewObject)
    {
        if (!scan.Types.IsOwnedDisposable(memberType))
        {
            return Ownership.None;
        }

        return initializer switch
        {
            ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax when allowNewObject => OwnershipFor(scan.Types, memberType),
            InvocationExpressionSyntax invocation when IsStaticFactory(scan.Context, invocation) => OwnershipFor(scan.Types, memberType),
            _ => Ownership.None,
        };
    }

    /// <summary>Returns whether an invocation resolves to a static factory method.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The initializer invocation.</param>
    /// <returns><see langword="true"/> when the called method is static (including a reduced extension).</returns>
    private static bool IsStaticFactory(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        => context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol { IsStatic: true };

    /// <summary>Returns whether a field is a type-owned collection filled with newly created disposables.</summary>
    /// <param name="scan">The ownership scan state.</param>
    /// <param name="field">The field.</param>
    /// <param name="initializer">The field initializer.</param>
    /// <returns><see langword="true"/> when the type creates the collection and adds a <c>new</c> disposable to it.</returns>
    private static bool IsOwnedDisposableCollection(ref OwnershipScan scan, IFieldSymbol field, ExpressionSyntax initializer)
    {
        if (scan.CollectionInterface is null
            || initializer is not (ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
            || !HasDisposableElement(scan.Types, scan.CollectionInterface, field.Type))
        {
            return false;
        }

        var addScan = new AddNewScan(scan.Context, field);
        DescendantTraversalHelper.VisitDescendants<InvocationExpressionSyntax, AddNewScan>(scan.Declaration, ref addScan, VisitAddNew);
        return addScan.Found;
    }

    /// <summary>Returns whether a collection type's element is an owned disposable.</summary>
    /// <param name="types">The disposal types resolved for this compilation.</param>
    /// <param name="collectionInterface">The unbound <c>ICollection&lt;T&gt;</c> interface.</param>
    /// <param name="collectionType">The collection field type.</param>
    /// <returns><see langword="true"/> when the collection's element type is a disposable.</returns>
    private static bool HasDisposableElement(in DisposableTypes types, INamedTypeSymbol collectionInterface, ITypeSymbol collectionType)
    {
        var interfaces = collectionType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i].OriginalDefinition, collectionInterface)
                && interfaces[i].TypeArguments is [var element]
                && types.IsOwnedDisposable(element))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Records a <c>field.Add(new ...)</c> call, stopping the walk once found.</summary>
    /// <param name="invocation">The invocation being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once the add-of-new is found.</returns>
    private static bool VisitAddNew(InvocationExpressionSyntax invocation, ref AddNewScan state)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Add", Expression: { } receiver }
            || invocation.ArgumentList.Arguments is not [{ Expression: ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax }]
            || !SymbolEqualityComparer.Default.Equals(state.Context.SemanticModel.GetSymbolInfo(receiver, state.Context.CancellationToken).Symbol, state.Field))
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Returns whether a property is an auto-property.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns><see langword="true"/> when every accessor is a bodyless auto-accessor.</returns>
    private static bool IsAutoProperty(PropertyDeclarationSyntax property)
    {
        if (property.AccessorList is not { } accessors)
        {
            return false;
        }

        for (var i = 0; i < accessors.Accessors.Count; i++)
        {
            var accessor = accessors.Accessors[i];
            if (accessor.Body is not null || accessor.ExpressionBody is not null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an owned member is synchronously disposable, so a generated <c>Dispose()</c> can release it.</summary>
    /// <param name="types">The disposal types resolved for this compilation.</param>
    /// <param name="memberType">The member's type.</param>
    /// <returns><see cref="Ownership.Fixable"/> for a sync-disposable member, otherwise <see cref="Ownership.NotFixable"/>.</returns>
    private static Ownership OwnershipFor(in DisposableTypes types, ITypeSymbol memberType)
        => types.ImplementsSyncDisposable(memberType) ? Ownership.Fixable : Ownership.NotFixable;

    /// <summary>The state threaded through one type's ownership scan.</summary>
    /// <param name="Context">The syntax node analysis context.</param>
    /// <param name="Types">The disposal types resolved for this compilation.</param>
    /// <param name="CollectionInterface">The unbound <c>ICollection&lt;T&gt;</c> interface, if resolved.</param>
    /// <param name="Declaration">The type declaration being analyzed.</param>
    private record struct OwnershipScan(
        SyntaxNodeAnalysisContext Context,
        DisposableTypes Types,
        INamedTypeSymbol? CollectionInterface,
        TypeDeclarationSyntax Declaration)
    {
        /// <summary>Gets the first owned member's name, for the message.</summary>
        public string? FirstOwned { get; private set; }

        /// <summary>Gets a value indicating whether every owned member can be disposed by a generated method.</summary>
        public bool AllFixable { get; private set; } = true;

        /// <summary>Gets the sync-disposable members a fix should release.</summary>
        public List<string>? DisposeMembers { get; private set; }

        /// <summary>Records one member's ownership classification.</summary>
        /// <param name="memberName">The member name.</param>
        /// <param name="ownership">Its ownership classification.</param>
        public void Record(string memberName, Ownership ownership)
        {
            if (ownership == Ownership.None)
            {
                return;
            }

            FirstOwned ??= memberName;
            if (ownership == Ownership.Fixable)
            {
                (DisposeMembers ??= []).Add(memberName);
            }
            else
            {
                AllFixable = false;
            }
        }
    }

    /// <summary>The state threaded through the collection add-of-new scan.</summary>
    /// <param name="Context">The syntax node analysis context.</param>
    /// <param name="Field">The collection field.</param>
    private record struct AddNewScan(SyntaxNodeAnalysisContext Context, IFieldSymbol Field)
    {
        /// <summary>Gets or sets a value indicating whether a matching add-of-new was found.</summary>
        public bool Found { get; set; }
    }
}
