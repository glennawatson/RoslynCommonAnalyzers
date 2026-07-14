// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an optional parameter on an externally visible method or constructor (SST2309). The default is
/// not kept in the callee: the compiler copies it into every call site that omits the argument. A caller
/// compiled against version 1 therefore keeps passing version 1's default forever, even after the library
/// changes it — and nothing anywhere says so.
/// </summary>
/// <remarks>
/// <para>
/// A caller-info parameter is never reported. <c>[CallerMemberName]</c>, <c>[CallerFilePath]</c>,
/// <c>[CallerLineNumber]</c> and <c>[CallerArgumentExpression]</c> only work on an optional parameter — the
/// compiler substitutes the value exactly where the default would have gone — so the rule would be asking
/// for something the language does not allow.
/// </para>
/// <para>
/// A member whose signature its author cannot change is not reported either: an <c>override</c>, an
/// explicit interface implementation, and a method implementing an interface member all take their shape
/// from elsewhere. The interface and abstract declarations that set that shape are reported instead, which
/// is the one place a change does anything.
/// </para>
/// <para>
/// A positional record's primary constructor is not reported. Its parameter list is the record's
/// definition rather than an ordinary signature, and there is no overload to write it as.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2309OptionalParameterAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.OptionalParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    /// <summary>Reports each optional parameter a caller would compile a default for.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <remarks>
    /// A member with no optional parameter at all — nearly every member — is rejected before the
    /// interface-implementation walk is ever paid for.
    /// </remarks>
    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!IsCandidate(method) || ImplementsAnInterfaceMember(method))
        {
            return;
        }

        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (!IsReportable(parameter))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DesignRules.OptionalParameter,
                parameter.Locations[0],
                parameter.Name,
                method.MethodKind == MethodKind.Constructor ? method.ContainingType.Name : method.Name));
        }
    }

    /// <summary>Returns whether a member is one this rule has anything to say about.</summary>
    /// <param name="method">The method to test.</param>
    /// <returns><see langword="true"/> when the member is visible, its own, and carries a reportable default.</returns>
    /// <remarks>
    /// The cheap tests come first, so a member with no optional parameter — nearly every member — never
    /// reaches the interface walk.
    /// </remarks>
    private static bool IsCandidate(IMethodSymbol method)
        => method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor
            && !method.IsOverride
            && method.ExplicitInterfaceImplementations.IsEmpty
            && !IsPrimaryConstructor(method)
            && IsExternallyVisible(method)
            && HasReportableOptionalParameter(method.Parameters);

    /// <summary>Returns whether any parameter carries a default the rule would report.</summary>
    /// <param name="parameters">The member's parameters.</param>
    /// <returns><see langword="true"/> when at least one optional parameter is reportable.</returns>
    private static bool HasReportableOptionalParameter(ImmutableArray<IParameterSymbol> parameters)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (IsReportable(parameters[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether one parameter is an optional parameter the author could remove.</summary>
    /// <param name="parameter">The parameter to test.</param>
    /// <returns><see langword="true"/> for an optional parameter that is not caller-info.</returns>
    /// <remarks>
    /// A <c>params</c> array has no default value, so it never reaches this rule; omitting its arguments is
    /// the language building an empty array, not a constant baked into the call site.
    /// </remarks>
    private static bool IsReportable(IParameterSymbol parameter)
        => parameter.HasExplicitDefaultValue && !IsCallerInfo(parameter);

    /// <summary>Returns whether a parameter is filled in by the compiler from the call site.</summary>
    /// <param name="parameter">The parameter to test.</param>
    /// <returns><see langword="true"/> when a caller-info attribute is applied.</returns>
    /// <remarks>
    /// These attributes require the parameter to be optional — the default is the slot the compiler writes
    /// the caller's details into — so demanding an overload here would demand code that does not compile.
    /// </remarks>
    private static bool IsCallerInfo(IParameterSymbol parameter)
    {
        var attributes = parameter.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            var name = attributes[i].AttributeClass?.Name;
            if (name is "CallerMemberNameAttribute"
                or "CallerFilePathAttribute"
                or "CallerLineNumberAttribute"
                or "CallerArgumentExpressionAttribute")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a constructor is a type declaration's primary constructor.</summary>
    /// <param name="method">The method to test.</param>
    /// <returns><see langword="true"/> when the constructor is declared by the type's own header.</returns>
    /// <remarks>
    /// A positional record's parameter list is the record's definition, not a signature the author chose;
    /// there is no overload to express it as, so the rule has nothing to ask for.
    /// </remarks>
    private static bool IsPrimaryConstructor(IMethodSymbol method)
    {
        if (method.MethodKind != MethodKind.Constructor)
        {
            return false;
        }

        var references = method.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            if (references[i].GetSyntax() is TypeDeclarationSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a method implements an interface member, so its shape is not its own.</summary>
    /// <param name="method">The method to test.</param>
    /// <returns><see langword="true"/> when some interface member maps to this method.</returns>
    private static bool ImplementsAnInterfaceMember(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var members = interfaces[i].GetMembers(method.Name);
            for (var j = 0; j < members.Length; j++)
            {
                if (members[j] is not IMethodSymbol interfaceMethod)
                {
                    continue;
                }

                var implementation = containingType.FindImplementationForInterfaceMember(interfaceMethod);
                if (SymbolEqualityComparer.Default.Equals(implementation, method))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a symbol can be seen from outside the assembly that declares it.</summary>
    /// <param name="symbol">The symbol to test.</param>
    /// <returns><see langword="true"/> when the symbol and every type containing it are visible.</returns>
    /// <remarks>
    /// The hazard is a caller in another assembly, compiled at another time, holding a stale default. Inside
    /// the assembly every caller is rebuilt together, so an internal or private default is never stale.
    /// </remarks>
    private static bool IsExternallyVisible(ISymbol symbol)
    {
        for (var current = symbol; current is not null && current.Kind != SymbolKind.Namespace; current = current.ContainingSymbol)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                {
                    break;
                }

                default:
                {
                    return false;
                }
            }
        }

        return true;
    }
}
