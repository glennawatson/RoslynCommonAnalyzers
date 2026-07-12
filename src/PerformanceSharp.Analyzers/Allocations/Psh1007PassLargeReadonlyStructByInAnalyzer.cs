// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a large readonly struct passed by value where an <c>in</c> reference would copy less
/// (PSH1007). The size threshold defaults to 32 estimated bytes and is configured with
/// <c>performancesharp.PSH1007.in_parameter_minimum_size</c>.
/// </summary>
/// <remarks>
/// <para>
/// The rule is deliberately hard to trigger, because most "pass this by <c>in</c>" advice is wrong. A
/// struct of 16 bytes or less rides in registers, so an <c>in</c> makes the caller spill it to the stack
/// and pass a pointer instead — strictly worse. A struct that is not <c>readonly</c> is worse still: the
/// compiler cannot prove a member access leaves it alone, so it copies the whole thing defensively before
/// each one, which is what PSH1003 reports. And the SIMD types are excluded outright: the entire BCL
/// vector surface, up to the 64-byte <c>Matrix4x4</c>, passes them by value.
/// </para>
/// <para>
/// The signature must also be one the author can actually change to <c>in</c> without breaking the build.
/// An override or interface implementation must match its base, an iterator and an async method cannot
/// take <c>in</c> at all, a captured parameter cannot become a reference, and neither can one the body
/// assigns. Each of those is a compiler error, so reporting them would be advice that does not compile.
/// </para>
/// <para>
/// Ordered so the clean path stays syntactic. A parameter that already has a modifier, or whose type is
/// spelled with a built-in keyword, is rejected on syntax alone — which is nearly every parameter in a
/// codebase. Only a parameter that survives that is bound, and only one whose type is a large readonly
/// struct pays for the size estimate and the body walk.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1007PassLargeReadonlyStructByInAnalyzer : DiagnosticAnalyzer
{
    /// <summary>How the struct's name is written into the message.</summary>
    /// <remarks>
    /// Deliberately not <c>ToMinimalDisplayString</c>. Minimal qualification has to ask the semantic model
    /// what names are in scope at the parameter's position, and an EventPipe trace put that one call at half
    /// of this analyzer's total CPU on a violating corpus. The name and its type arguments are all the
    /// message needs, and this format produces them without binding anything.
    /// </remarks>
    private static readonly SymbolDisplayFormat TypeNameFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.PassLargeReadonlyStructByIn);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Sets up the per-compilation caches, then analyzes every parameter.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var optionsByTree = new ConcurrentDictionary<SyntaxTree, InParameterOptions>();
        var sizeByType = new ConcurrentDictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeParameter(nodeContext, optionsByTree, sizeByType),
            SyntaxKind.Parameter);
    }

    /// <summary>Reports one by-value parameter that would copy less as an <c>in</c> reference.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <param name="sizeByType">The per-compilation struct-size cache.</param>
    private static void AnalyzeParameter(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, InParameterOptions> optionsByTree,
        ConcurrentDictionary<ITypeSymbol, int> sizeByType)
    {
        var parameter = (ParameterSyntax)context.Node;

        // 'in', 'ref', 'out', 'params', 'this' and 'scoped' all mean this is not a plain by-value
        // parameter. A built-in type keyword names something that is never a large struct.
        if (parameter.Modifiers.Count > 0 || parameter.Type is null or PredefinedTypeSyntax)
        {
            return;
        }

        if (GetChangeableContainer(parameter) is not { } container)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is not { Type: INamedTypeSymbol type } symbol
            || !IsLargeReadonlyStruct(type))
        {
            return;
        }

        var size = GetReportableSize(context, container, symbol, type, optionsByTree, sizeByType);
        if (size == StructSizeEstimator.Unknown)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.PassLargeReadonlyStructByIn,
            parameter.Identifier.GetLocation(),
            parameter.Identifier.ValueText,
            type.ToDisplayString(TypeNameFormat),
            size.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Gets the size to report, or <see cref="StructSizeEstimator.Unknown"/> when nothing should be.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="container">The containing declaration.</param>
    /// <param name="symbol">The parameter symbol.</param>
    /// <param name="type">The parameter's struct type.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <param name="sizeByType">The per-compilation struct-size cache.</param>
    /// <returns>The estimated size, or <see cref="StructSizeEstimator.Unknown"/>.</returns>
    /// <remarks>
    /// The gates run cheapest first: an excluded type costs a name compare, the size a cached estimate, and
    /// only a parameter that clears both pays for the interface walk and the body scan.
    /// </remarks>
    private static int GetReportableSize(
        SyntaxNodeAnalysisContext context,
        SyntaxNode container,
        IParameterSymbol symbol,
        INamedTypeSymbol type,
        ConcurrentDictionary<SyntaxTree, InParameterOptions> optionsByTree,
        ConcurrentDictionary<ITypeSymbol, int> sizeByType)
    {
        var options = GetOptions(context, optionsByTree);
        if (InParameterOptions.IsExcluded(type, options.ExcludedTypes))
        {
            return StructSizeEstimator.Unknown;
        }

        var size = StructSizeEstimator.Estimate(type, sizeByType);
        if (size == StructSizeEstimator.Unknown || size < options.MinimumSize)
        {
            return StructSizeEstimator.Unknown;
        }

        if (!options.IncludePublicApi && IsExternallyVisible(symbol.ContainingSymbol))
        {
            return StructSizeEstimator.Unknown;
        }

        return IsSignatureChangeable(symbol, context) && CanBodyTakeReadonlyReference(container, symbol, context)
            ? size
            : StructSizeEstimator.Unknown;
    }

    /// <summary>Reads the settings for the parameter's tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns>The resolved settings.</returns>
    private static InParameterOptions GetOptions(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, InParameterOptions> optionsByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (optionsByTree.TryGetValue(tree, out var options))
        {
            return options;
        }

        options = InParameterOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        optionsByTree.TryAdd(tree, options);
        return options;
    }

    /// <summary>Gets the declaration whose parameter list could be changed, if there is one.</summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>The containing declaration, or <see langword="null"/> when it may not be changed.</returns>
    /// <remarks>
    /// A lambda and an anonymous method take their shape from the delegate they are assigned to, and a
    /// delegate declaration's own shape is its identity. A primary constructor's parameter cannot be
    /// <c>in</c> and still be read from an instance member (CS9109), which is the whole point of one.
    /// Operators are left alone because changing them is a binary break for no measurable gain.
    /// </remarks>
    private static SyntaxNode? GetChangeableContainer(ParameterSyntax parameter)
    {
        var container = parameter.Parent?.Parent;
        return container switch
        {
            MethodDeclarationSyntax or ConstructorDeclarationSyntax or LocalFunctionStatementSyntax or IndexerDeclarationSyntax
                when !IsFixedShape(container) => container,
            _ => null,
        };
    }

    /// <summary>Returns whether a declaration's shape is dictated by something other than itself.</summary>
    /// <param name="container">The containing declaration.</param>
    /// <returns><see langword="true"/> when the parameter list may not be changed.</returns>
    /// <remarks>
    /// <c>async</c> cannot take <c>in</c> (CS1988). An <c>override</c> or an interface implementation must
    /// keep its base signature. A <c>virtual</c> or <c>abstract</c> member is a contract any assembly may
    /// derive from. A <c>partial</c> member must match its other half, and an <c>extern</c> one matches a
    /// native API. An interface's members are all implicitly virtual, so the whole interface is left alone.
    /// </remarks>
    private static bool IsFixedShape(SyntaxNode container)
    {
        var modifiers = GetModifiers(container);
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].Kind() is SyntaxKind.AsyncKeyword
                or SyntaxKind.OverrideKeyword
                or SyntaxKind.VirtualKeyword
                or SyntaxKind.AbstractKeyword
                or SyntaxKind.PartialKeyword
                or SyntaxKind.ExternKeyword)
            {
                return true;
            }
        }

        return HasExplicitInterfaceSpecifier(container) || container.Parent is InterfaceDeclarationSyntax;
    }

    /// <summary>Gets a declaration's modifiers.</summary>
    /// <param name="container">The containing declaration.</param>
    /// <returns>The modifier list, or an empty list.</returns>
    private static SyntaxTokenList GetModifiers(SyntaxNode container) => container switch
    {
        MemberDeclarationSyntax member => member.Modifiers,
        LocalFunctionStatementSyntax local => local.Modifiers,
        _ => default,
    };

    /// <summary>Returns whether a declaration explicitly implements an interface member.</summary>
    /// <param name="container">The containing declaration.</param>
    /// <returns><see langword="true"/> when an interface dictates the signature.</returns>
    private static bool HasExplicitInterfaceSpecifier(SyntaxNode container) => container switch
    {
        MethodDeclarationSyntax method => method.ExplicitInterfaceSpecifier is not null,
        IndexerDeclarationSyntax indexer => indexer.ExplicitInterfaceSpecifier is not null,
        _ => false,
    };

    /// <summary>Returns whether the type is a readonly struct that could benefit from an <c>in</c>.</summary>
    /// <param name="type">The parameter's type.</param>
    /// <returns><see langword="true"/> when the type is a candidate on its shape alone.</returns>
    /// <remarks>
    /// A ref struct is stack-only and is itself already a reference to somewhere else; wrapping it in
    /// another reference costs an indirection and can break a caller's ref-safety. A struct that is not
    /// <c>readonly</c> would take a defensive copy at every member access, which is PSH1003's complaint,
    /// not an improvement.
    /// </remarks>
    private static bool IsLargeReadonlyStruct(INamedTypeSymbol type)
        => type is { TypeKind: TypeKind.Struct, SpecialType: SpecialType.None, IsReadOnly: true, IsRefLikeType: false };

    /// <summary>Returns whether a symbol can be seen from outside the assembly that declares it.</summary>
    /// <param name="symbol">The member that declares the parameter.</param>
    /// <returns><see langword="true"/> when changing its signature is a binary break for consumers.</returns>
    private static bool IsExternallyVisible(ISymbol? symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current is INamespaceSymbol)
            {
                break;
            }

            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether the declaring member's signature is free of an inherited contract.</summary>
    /// <param name="parameter">The parameter symbol.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when nothing outside the member fixes its shape.</returns>
    /// <remarks>
    /// An attribute's constructor is excluded because every use of the attribute would stop compiling
    /// (CS8358), which the constructor's own declaration gives no hint of.
    /// </remarks>
    private static bool IsSignatureChangeable(IParameterSymbol parameter, SyntaxNodeAnalysisContext context)
    {
        if (parameter.ContainingSymbol is not IMethodSymbol method)
        {
            return false;
        }

        if (method.ContainingType is { } containingType
            && (IsAttributeType(containingType) || ImplementsInterfaceMember(method, containingType)))
        {
            return false;
        }

        return !HasUnmanagedCallersOnlyAttribute(method, context.Compilation);
    }

    /// <summary>Returns whether a type derives from <see cref="Attribute"/>.</summary>
    /// <param name="type">The containing type.</param>
    /// <returns><see langword="true"/> for an attribute class.</returns>
    private static bool IsAttributeType(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current is { Name: "Attribute", ContainingNamespace.Name: "System" })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a method implicitly implements an interface member.</summary>
    /// <param name="method">The declaring method.</param>
    /// <param name="containingType">The method's containing type.</param>
    /// <returns><see langword="true"/> when an interface dictates the signature.</returns>
    private static bool ImplementsInterfaceMember(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidates = interfaces[i].GetMembers(method.Name);
            for (var j = 0; j < candidates.Length; j++)
            {
                if (SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(candidates[j]), method))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a method is a native callback whose signature the runtime fixes.</summary>
    /// <param name="method">The declaring method.</param>
    /// <param name="compilation">The compilation.</param>
    /// <returns><see langword="true"/> when the method carries <c>[UnmanagedCallersOnly]</c>.</returns>
    private static bool HasUnmanagedCallersOnlyAttribute(IMethodSymbol method, Compilation compilation)
    {
        var attributes = method.GetAttributes();
        if (attributes.Length == 0)
        {
            return false;
        }

        var marker = compilation.GetTypeByMetadataName("System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute");
        if (marker is null)
        {
            return false;
        }

        for (var i = 0; i < attributes.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(attributes[i].AttributeClass, marker))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the member's body would still compile with an <c>in</c> parameter.</summary>
    /// <param name="container">The containing declaration.</param>
    /// <param name="parameter">The parameter symbol.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when the body neither captures nor writes the parameter.</returns>
    private static bool CanBodyTakeReadonlyReference(SyntaxNode container, IParameterSymbol parameter, SyntaxNodeAnalysisContext context)
    {
        var body = GetBody(container);
        return InParameterBodyScan.CanBecomeReadonlyReference(body, parameter, context.SemanticModel, context.CancellationToken);
    }

    /// <summary>Gets the node holding a declaration's executable body.</summary>
    /// <param name="container">The containing declaration.</param>
    /// <returns>The body, or <see langword="null"/> when the declaration has none.</returns>
    private static SyntaxNode? GetBody(SyntaxNode container) => container switch
    {
        MethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody,
        ConstructorDeclarationSyntax constructor => (SyntaxNode?)constructor.Body ?? constructor.ExpressionBody,
        LocalFunctionStatementSyntax local => (SyntaxNode?)local.Body ?? local.ExpressionBody,
        IndexerDeclarationSyntax indexer => (SyntaxNode?)indexer.AccessorList ?? indexer.ExpressionBody,
        _ => null,
    };
}
