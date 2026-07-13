// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Checks the shape of an exception type. Reports <b>SST1488</b> when the type does not declare the
/// constructors every caller expects — parameterless, message, and message plus inner exception — and
/// <b>SST1489</b> when it still carries the formatter-based serialization members.
/// </summary>
/// <remarks>
/// <para>
/// The two rules share one walk because both answer questions about the same declaration: a type that
/// derives from <c>System.Exception</c>. Neither asks for the
/// <c>(SerializationInfo, StreamingContext)</c> constructor — that is the point of SST1489, which says
/// the opposite.
/// </para>
/// <para>
/// SST1489 is gated on the target framework having actually obsoleted those members. Rather than test a
/// framework version, the rule asks the framework itself: it resolves <c>System.Exception</c> and looks
/// for an <c>[Obsolete]</c> attribute on <c>GetObjectData</c>. Modern .NET marks it obsolete; .NET
/// Framework and netstandard2.0 do not, and there the members are still live and are never reported.
/// The probe runs once per compilation and only for a type that is actually an exception.
/// </para>
/// <para>
/// Ordered so the clean path is a pointer walk. Almost no type in a compilation is an exception, so the
/// base-type chase rejects first; the constructor scan, the options read and the serialization probe all
/// sit behind it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExceptionConstructorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property naming the constructors SST1488 found missing.</summary>
    internal const string MissingConstructorsKey = "SST1488.Missing";

    /// <summary>The name of the member that carries the framework's serialization payload.</summary>
    private const string GetObjectDataName = "GetObjectData";

    /// <summary>The number of parameters the serialization members take.</summary>
    private const int SerializationParameterCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.ExceptionStandardConstructors,
        MaintainabilityRules.ObsoleteSerializationMember);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Creates the per-compilation state, then analyzes every named type.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var state = new ExceptionTypeState(context.Compilation);
        context.RegisterSymbolAction(symbolContext => AnalyzeNamedType(symbolContext, state), SymbolKind.NamedType);
    }

    /// <summary>Analyzes one type, reporting both rules when it is an exception.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="state">The lazily-resolved per-compilation state.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context, ExceptionTypeState state)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.IsStatic || !state.IsException(type))
        {
            return;
        }

        if (type.Locations.IsEmpty || type.Locations[0].SourceTree is not { } tree)
        {
            return;
        }

        ReportMissingConstructors(context, type, tree);
        ReportSerializationMembers(context, type, state);
    }

    /// <summary>Reports SST1488 when the type does not declare all the constructors callers expect.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The exception type.</param>
    /// <param name="tree">The tree the type is declared in, used to read its settings.</param>
    private static void ReportMissingConstructors(SymbolAnalysisContext context, INamedTypeSymbol type, SyntaxTree tree)
    {
        var options = ExceptionConstructorOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        if (!options.IncludeNonPublicTypes && !IsExternallyVisible(type))
        {
            return;
        }

        if (!BaseOffersAConstructorToChainTo(type))
        {
            return;
        }

        var missing = FindMissingConstructors(type, options.RequireParameterless);
        if (missing == StandardExceptionConstructors.None)
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(MissingConstructorsKey, ((int)missing).ToString(System.Globalization.CultureInfo.InvariantCulture));

        context.ReportDiagnostic(Diagnostic.Create(
            MaintainabilityRules.ExceptionStandardConstructors,
            type.Locations[0],
            properties,
            type.Name,
            Describe(missing)));
    }

    /// <summary>Returns whether the base exception offers a constructor the standard ones can chain to.</summary>
    /// <param name="type">The exception type.</param>
    /// <returns><see langword="true"/> when the base declares a reachable parameterless, message, or message-and-inner constructor.</returns>
    /// <remarks>
    /// A derived type cannot conjure a base it does not have. Where the base exposes only a constructor of
    /// its own shape — one taking a request, a status code, a response — there is no <c>: base(message)</c>
    /// to write, and the standard constructors cannot be declared at all. Asking for them would be asking
    /// for code that does not compile, so the rule keeps quiet and leaves the design alone.
    /// </remarks>
    private static bool BaseOffersAConstructorToChainTo(INamedTypeSymbol type)
    {
        if (type.BaseType is not { } baseType)
        {
            return false;
        }

        var constructors = baseType.InstanceConstructors;
        for (var i = 0; i < constructors.Length; i++)
        {
            var constructor = constructors[i];
            if (constructor.DeclaredAccessibility == Accessibility.Private)
            {
                continue;
            }

            if (Classify(constructor) != StandardExceptionConstructors.None)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Works out which of the expected constructors the type does not declare.</summary>
    /// <param name="type">The exception type.</param>
    /// <param name="requireParameterless">Whether a parameterless constructor is expected.</param>
    /// <returns>The set of constructors that are missing.</returns>
    /// <remarks>
    /// A derived type does not inherit its base's constructors, so every exception type must declare its
    /// own — inheriting from a well-formed exception buys nothing here.
    /// </remarks>
    private static StandardExceptionConstructors FindMissingConstructors(INamedTypeSymbol type, bool requireParameterless)
    {
        var found = StandardExceptionConstructors.None;
        var constructors = type.InstanceConstructors;
        for (var i = 0; i < constructors.Length; i++)
        {
            var constructor = constructors[i];
            if (constructor.IsImplicitlyDeclared || !IsReachable(constructor, type))
            {
                continue;
            }

            found |= Classify(constructor);
        }

        var expected = StandardExceptionConstructors.Message | StandardExceptionConstructors.MessageAndInner;
        if (requireParameterless)
        {
            expected |= StandardExceptionConstructors.Parameterless;
        }

        return expected & ~found;
    }

    /// <summary>Identifies which expected constructor a declaration satisfies, by parameter type.</summary>
    /// <param name="constructor">The declared constructor.</param>
    /// <returns>The constructor it satisfies, or none.</returns>
    /// <remarks>Parameter names are not read: what matters is the shape a caller can write.</remarks>
    private static StandardExceptionConstructors Classify(IMethodSymbol constructor)
    {
        var parameters = constructor.Parameters;
        if (parameters.Length == 0)
        {
            return StandardExceptionConstructors.Parameterless;
        }

        if (parameters[0].Type.SpecialType != SpecialType.System_String)
        {
            return StandardExceptionConstructors.None;
        }

        if (parameters.Length == 1)
        {
            return StandardExceptionConstructors.Message;
        }

        return parameters.Length == SerializationParameterCount && IsExceptionType(parameters[1].Type)
            ? StandardExceptionConstructors.MessageAndInner
            : StandardExceptionConstructors.None;
    }

    /// <summary>Returns whether a parameter's type is <c>System.Exception</c> itself.</summary>
    /// <param name="type">The parameter type.</param>
    /// <returns><see langword="true"/> when the constructor can wrap any cause.</returns>
    private static bool IsExceptionType(ITypeSymbol type)
        => type is INamedTypeSymbol { Name: nameof(Exception), ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } };

    /// <summary>Returns whether the people who can see the type can also call the constructor.</summary>
    /// <param name="constructor">The declared constructor.</param>
    /// <param name="type">The exception type.</param>
    /// <returns><see langword="true"/> when the constructor actually serves the type's callers.</returns>
    /// <remarks>
    /// An abstract exception is constructed only by its derived types, so a protected constructor is the
    /// correct shape there. A concrete exception needs a public one — unless the type is not visible
    /// outside the assembly, where an internal constructor already reaches everyone who can see it.
    /// </remarks>
    private static bool IsReachable(IMethodSymbol constructor, INamedTypeSymbol type)
    {
        var accessibility = constructor.DeclaredAccessibility;
        if (accessibility == Accessibility.Public)
        {
            return true;
        }

        if (type.IsAbstract)
        {
            return accessibility is Accessibility.Protected or Accessibility.ProtectedOrInternal;
        }

        return accessibility == Accessibility.Internal && !IsExternallyVisible(type);
    }

    /// <summary>Returns whether the type can be seen from outside the assembly.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> when the type and every container are public or protected.</returns>
    private static bool IsExternallyVisible(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Names the missing constructors as a phrase the message can finish a sentence with.</summary>
    /// <param name="missing">The set of missing constructors.</param>
    /// <returns>The phrase, e.g. <c>a parameterless constructor and a constructor taking a message</c>.</returns>
    private static string Describe(StandardExceptionConstructors missing)
    {
        var parts = new List<string>(3);
        if ((missing & StandardExceptionConstructors.Parameterless) != 0)
        {
            parts.Add("a parameterless constructor");
        }

        if ((missing & StandardExceptionConstructors.Message) != 0)
        {
            parts.Add("a constructor taking a message");
        }

        if ((missing & StandardExceptionConstructors.MessageAndInner) != 0)
        {
            parts.Add("a constructor taking a message and an inner exception");
        }

        return Join(parts);
    }

    /// <summary>Joins the phrases with commas and a trailing 'and'.</summary>
    /// <param name="parts">The phrases, of which there is always at least one.</param>
    /// <returns>The joined phrase.</returns>
    private static string Join(List<string> parts)
    {
        if (parts.Count == 1)
        {
            return parts[0];
        }

        var builder = new System.Text.StringBuilder(parts[0]);
        for (var i = 1; i < parts.Count; i++)
        {
            builder.Append(i == parts.Count - 1 ? " and " : ", ").Append(parts[i]);
        }

        return builder.ToString();
    }

    /// <summary>Reports SST1489 for each serialization member the target framework has obsoleted.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The exception type.</param>
    /// <param name="state">The per-compilation state, which knows whether the members are obsolete.</param>
    private static void ReportSerializationMembers(SymbolAnalysisContext context, INamedTypeSymbol type, ExceptionTypeState state)
    {
        if (!state.SerializationIsObsolete)
        {
            return;
        }

        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is not IMethodSymbol method || !state.IsSerializationMember(method))
            {
                continue;
            }

            var locations = method.Locations;
            if (locations.IsEmpty || !locations[0].IsInSource)
            {
                continue;
            }

            var member = method.MethodKind == MethodKind.Constructor
                ? type.Name + "(SerializationInfo, StreamingContext)"
                : GetObjectDataName;

            context.ReportDiagnostic(Diagnostic.Create(
                MaintainabilityRules.ObsoleteSerializationMember,
                locations[0],
                member));
        }
    }

    /// <summary>
    /// The per-compilation facts both rules need: what <c>System.Exception</c> is, whether the framework
    /// has obsoleted its serialization members, and what the serialization parameter types are.
    /// </summary>
    /// <remarks>
    /// Everything is resolved on first use, behind the base-type check — a compilation that declares no
    /// exception never pays a metadata lookup at all.
    /// </remarks>
    private sealed class ExceptionTypeState(Compilation compilation)
    {
        /// <summary>Guards the one-time resolution.</summary>
        private readonly object _gate = new();

        /// <summary>The compilation being analyzed.</summary>
        private readonly Compilation _compilation = compilation;

        /// <summary>Whether <see cref="Resolve"/> has run.</summary>
        private bool _resolved;

        /// <summary><c>System.Exception</c>, or null when it cannot be resolved.</summary>
        private INamedTypeSymbol? _exception;

        /// <summary><c>System.Runtime.Serialization.SerializationInfo</c>, or null.</summary>
        private INamedTypeSymbol? _serializationInfo;

        /// <summary><c>System.Runtime.Serialization.StreamingContext</c>, or null.</summary>
        private INamedTypeSymbol? _streamingContext;

        /// <summary>Whether the framework marks the serialization members obsolete.</summary>
        private bool _serializationIsObsolete;

        /// <summary>Gets a value indicating whether this target framework has obsoleted the serialization members.</summary>
        public bool SerializationIsObsolete
        {
            get
            {
                Resolve();
                return _serializationIsObsolete;
            }
        }

        /// <summary>Returns whether the type derives from <c>System.Exception</c>.</summary>
        /// <param name="type">The type.</param>
        /// <returns><see langword="true"/> when the type is an exception.</returns>
        public bool IsException(INamedTypeSymbol type)
        {
            Resolve();
            if (_exception is null)
            {
                return false;
            }

            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, _exception))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether the member is one of the formatter-based serialization members.</summary>
        /// <param name="method">The declared member.</param>
        /// <returns><see langword="true"/> for the serialization constructor or a <c>GetObjectData</c> override.</returns>
        public bool IsSerializationMember(IMethodSymbol method)
        {
            if (_serializationInfo is null || _streamingContext is null || !HasSerializationParameters(method))
            {
                return false;
            }

            return method.MethodKind == MethodKind.Constructor
                || (method.Name == GetObjectDataName && method.IsOverride);
        }

        /// <summary>Asks the framework whether it has obsoleted the serialization members.</summary>
        /// <param name="exception">The resolved <c>System.Exception</c>, or null.</param>
        /// <returns><see langword="true"/> when <c>Exception.GetObjectData</c> carries an <c>[Obsolete]</c> attribute.</returns>
        /// <remarks>
        /// This is what keeps the rule off .NET Framework and netstandard2.0, where these members are
        /// still live and removing them would be wrong. Asking the framework rather than testing a
        /// version number means the rule stays correct on targets that do not exist yet.
        /// </remarks>
        private static bool ProbeObsoletion(INamedTypeSymbol? exception)
        {
            if (exception is null)
            {
                return false;
            }

            var members = exception.GetMembers(GetObjectDataName);
            for (var i = 0; i < members.Length; i++)
            {
                if (IsObsolete(members[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether a symbol carries <c>[Obsolete]</c>.</summary>
        /// <param name="symbol">The symbol.</param>
        /// <returns><see langword="true"/> when the framework has obsoleted it.</returns>
        private static bool IsObsolete(ISymbol symbol)
        {
            var attributes = symbol.GetAttributes();
            for (var i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].AttributeClass?.Name == nameof(ObsoleteAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether the member takes exactly the serialization parameter pair.</summary>
        /// <param name="method">The declared member.</param>
        /// <returns><see langword="true"/> when the parameters match.</returns>
        private bool HasSerializationParameters(IMethodSymbol method)
        {
            var parameters = method.Parameters;
            return parameters.Length == SerializationParameterCount
                && SymbolEqualityComparer.Default.Equals(parameters[0].Type, _serializationInfo)
                && SymbolEqualityComparer.Default.Equals(parameters[1].Type, _streamingContext);
        }

        /// <summary>Resolves the well-known types and probes the framework for the obsoletion, once.</summary>
        private void Resolve()
        {
            if (Volatile.Read(ref _resolved))
            {
                return;
            }

            lock (_gate)
            {
                if (_resolved)
                {
                    return;
                }

                _exception = _compilation.GetTypeByMetadataName("System.Exception");
                _serializationInfo = _compilation.GetTypeByMetadataName("System.Runtime.Serialization.SerializationInfo");
                _streamingContext = _compilation.GetTypeByMetadataName("System.Runtime.Serialization.StreamingContext");
                _serializationIsObsolete = ProbeObsoletion(_exception);
                Volatile.Write(ref _resolved, true);
            }
        }
    }
}
