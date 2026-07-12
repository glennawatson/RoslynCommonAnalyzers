// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a signature that declares more parameters than the configured maximum (SST1472). The maximum
/// defaults to 7 and is configured with <c>stylesharp.SST1472.max_parameters</c>.
/// </summary>
/// <remarks>
/// <para>
/// The rule only reports a signature its author can actually change. An override, an interface
/// implementation, a P/Invoke and a <c>Deconstruct</c> all inherit their shape from somewhere else, so the
/// fix belongs at the declaration they follow, not at the one being read. A lambda takes its shape from the
/// delegate it is assigned to, so anonymous functions are never measured. A positional record is the
/// parameter object this rule asks for, so it is exempt unless
/// <c>stylesharp.SST1472.check_positional_records</c> opts it back in.
/// </para>
/// <para>
/// Ordered so the clean path is a count and one cached lookup. Excluding a parameter — an extension
/// receiver, a caller-info parameter, an optional one — can only ever lower the total, so the declared count
/// is an upper bound: a signature already inside the limit is rejected before any parameter is inspected.
/// The exclusions are then applied on syntax alone, and only a signature still over the limit afterwards
/// pays for a symbol bind — which means clean code never binds at all.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1472TooManyParametersAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The smallest count that can ever be reported (a maximum of 1 flags 2 parameters).</summary>
    private const int MinimumReportableParameters = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.TooManyParameters);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the per-compilation state, then analyzes every signature that declares parameters.</summary>
    /// <param name="context">The compilation start context.</param>
    /// <remarks>
    /// A class, struct or record declaration is registered for its primary constructor. Operators are not:
    /// their arity is fixed by the language, so there is nothing to report and nothing to fix.
    /// </remarks>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var optionsByTree = new ConcurrentDictionary<SyntaxTree, ParameterCountOptions>();
        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, optionsByTree),
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.DelegateDeclaration,
            SyntaxKind.LocalFunctionStatement,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration);
    }

    /// <summary>Measures one signature and reports it when it declares more parameters than allowed.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, ConcurrentDictionary<SyntaxTree, ParameterCountOptions> optionsByTree)
    {
        var node = context.Node;
        if (GetParameterList(node) is not { } parameterList)
        {
            return;
        }

        var declared = parameterList.Parameters.Count;
        if (declared < MinimumReportableParameters)
        {
            return;
        }

        var options = GetOptions(context, optionsByTree);
        if (declared <= options.Maximum)
        {
            return;
        }

        // Narrow the count on syntax alone before any exemption is considered, so a signature that is only
        // nominally long — an extension receiver, a tail of caller-info parameters — never reaches the bind.
        var counted = CountCallerWrittenParameters(parameterList, options);
        if (counted <= options.Maximum)
        {
            return;
        }

        if (IsSignatureFixedElsewhere(node, options, context))
        {
            return;
        }

        var identifier = GetIdentifier(node);
        context.ReportDiagnostic(Diagnostic.Create(
            MaintainabilityRules.TooManyParameters,
            identifier.GetLocation(),
            identifier.ValueText,
            counted,
            options.Maximum));
    }

    /// <summary>Reads the settings for the declaration's tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns>The resolved settings.</returns>
    private static ParameterCountOptions GetOptions(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, ParameterCountOptions> optionsByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (optionsByTree.TryGetValue(tree, out var options))
        {
            return options;
        }

        options = ParameterCountOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        optionsByTree.TryAdd(tree, options);
        return options;
    }

    /// <summary>Gets the parameter list a declaration measures, if it declares one.</summary>
    /// <param name="node">The declaration.</param>
    /// <returns>The parameter list, or <see langword="null"/> when the declaration has none.</returns>
    /// <remarks>
    /// A type declaration yields the parameter list of its primary constructor, and yields nothing when it
    /// has none. An indexer's list is bracketed, so both shapes are read through their common base.
    /// </remarks>
    private static BaseParameterListSyntax? GetParameterList(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.ParameterList,
        ConstructorDeclarationSyntax constructor => constructor.ParameterList,
        DelegateDeclarationSyntax @delegate => @delegate.ParameterList,
        LocalFunctionStatementSyntax local => local.ParameterList,
        IndexerDeclarationSyntax indexer => indexer.ParameterList,
        TypeDeclarationSyntax type => type.ParameterList,
        _ => null,
    };

    /// <summary>Gets the token a diagnostic is reported on, which also names the signature.</summary>
    /// <param name="node">The declaration.</param>
    /// <returns>The identifier, or the <c>this</c> keyword of an indexer.</returns>
    private static SyntaxToken GetIdentifier(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Identifier,
        ConstructorDeclarationSyntax constructor => constructor.Identifier,
        DelegateDeclarationSyntax @delegate => @delegate.Identifier,
        LocalFunctionStatementSyntax local => local.Identifier,
        IndexerDeclarationSyntax indexer => indexer.ThisKeyword,
        TypeDeclarationSyntax type => type.Identifier,
        _ => default,
    };

    /// <summary>Returns whether the signature's shape is dictated by a declaration other than this one.</summary>
    /// <param name="node">The declaration.</param>
    /// <param name="options">The resolved settings.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when shortening the list here is not possible or not the fix.</returns>
    /// <remarks>The semantic interface lookup runs last, so a syntactic exemption never pays for a bind.</remarks>
    private static bool IsSignatureFixedElsewhere(SyntaxNode node, in ParameterCountOptions options, SyntaxNodeAnalysisContext context)
    {
        if (node is RecordDeclarationSyntax)
        {
            return !options.CheckPositionalRecords;
        }

        var modifiers = GetModifiers(node);
        if (ModifierListHelper.ContainsEither(modifiers, SyntaxKind.OverrideKeyword, SyntaxKind.ExternKeyword))
        {
            return true;
        }

        if (HasExplicitInterfaceSpecifier(node)
            || IsDeconstructor(node)
            || IsPartialImplementation(node, modifiers)
            || HasNativeImportAttribute(GetAttributeLists(node)))
        {
            return true;
        }

        return ImplementsInterfaceMember(node, context);
    }

    /// <summary>Gets a declaration's modifiers.</summary>
    /// <param name="node">The declaration.</param>
    /// <returns>The modifier list, or an empty list.</returns>
    private static SyntaxTokenList GetModifiers(SyntaxNode node) => node switch
    {
        MemberDeclarationSyntax member => member.Modifiers,
        LocalFunctionStatementSyntax local => local.Modifiers,
        _ => default,
    };

    /// <summary>Gets a declaration's attribute lists.</summary>
    /// <param name="node">The declaration.</param>
    /// <returns>The attribute lists, or an empty list.</returns>
    private static SyntaxList<AttributeListSyntax> GetAttributeLists(SyntaxNode node) => node switch
    {
        MemberDeclarationSyntax member => member.AttributeLists,
        LocalFunctionStatementSyntax local => local.AttributeLists,
        _ => default,
    };

    /// <summary>Returns whether the declaration explicitly implements an interface member.</summary>
    /// <param name="node">The declaration.</param>
    /// <returns><see langword="true"/> when the interface dictates the signature.</returns>
    private static bool HasExplicitInterfaceSpecifier(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.ExplicitInterfaceSpecifier is not null,
        IndexerDeclarationSyntax indexer => indexer.ExplicitInterfaceSpecifier is not null,
        _ => false,
    };

    /// <summary>Returns whether the declaration is a deconstructor.</summary>
    /// <param name="node">The declaration.</param>
    /// <returns><see langword="true"/> for a <c>Deconstruct</c> method, whose parameters mirror the type's state.</returns>
    private static bool IsDeconstructor(SyntaxNode node)
        => node is MethodDeclarationSyntax { Identifier.ValueText: "Deconstruct" };

    /// <summary>Returns whether the declaration is the implementing half of a partial member.</summary>
    /// <param name="node">The declaration.</param>
    /// <param name="modifiers">The declaration's modifiers.</param>
    /// <returns><see langword="true"/> when the defining half already carries the report.</returns>
    /// <remarks>
    /// Both halves of a partial member repeat the parameter list, and reporting both says the same thing
    /// twice. The defining half has no body; the implementing half does, and stays silent.
    /// </remarks>
    private static bool IsPartialImplementation(SyntaxNode node, SyntaxTokenList modifiers)
    {
        if (!ModifierListHelper.Contains(modifiers, SyntaxKind.PartialKeyword))
        {
            return false;
        }

        return node switch
        {
            MethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,
            ConstructorDeclarationSyntax constructor => constructor.Body is not null || constructor.ExpressionBody is not null,
            IndexerDeclarationSyntax indexer => HasAccessorBody(indexer),
            _ => false,
        };
    }

    /// <summary>Returns whether an indexer declares an accessor with a body.</summary>
    /// <param name="indexer">The indexer declaration.</param>
    /// <returns><see langword="true"/> when the indexer implements rather than declares.</returns>
    private static bool HasAccessorBody(IndexerDeclarationSyntax indexer)
    {
        if (indexer.ExpressionBody is not null)
        {
            return true;
        }

        if (indexer.AccessorList is not { } accessors)
        {
            return false;
        }

        var list = accessors.Accessors;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Body is not null || list[i].ExpressionBody is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the declaration carries a P/Invoke attribute.</summary>
    /// <param name="lists">The declaration's attribute lists.</param>
    /// <returns><see langword="true"/> when a native API dictates the signature.</returns>
    /// <remarks>The attribute name is matched on its text; binding it would cost a lookup to learn nothing more.</remarks>
    private static bool HasNativeImportAttribute(SyntaxList<AttributeListSyntax> lists)
    {
        for (var i = 0; i < lists.Count; i++)
        {
            var attributes = lists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (GetSimpleName(attributes[j].Name) is "DllImport"
                    or "DllImportAttribute"
                    or "LibraryImport"
                    or "LibraryImportAttribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether the declaration implicitly implements an interface member.</summary>
    /// <param name="node">The declaration.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when an interface dictates the signature.</returns>
    /// <remarks>
    /// Only a method or an indexer can implement an interface member, and only a signature already over the
    /// maximum reaches this far, so the bind and the interface walk stay off the clean path entirely.
    /// </remarks>
    private static bool ImplementsInterfaceMember(SyntaxNode node, SyntaxNodeAnalysisContext context)
    {
        if (node is not (MethodDeclarationSyntax or IndexerDeclarationSyntax))
        {
            return false;
        }

        if (context.SemanticModel.GetDeclaredSymbol(node, context.CancellationToken) is not { ContainingType: { } containingType } symbol)
        {
            return false;
        }

        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidates = interfaces[i].GetMembers(symbol.Name);
            for (var j = 0; j < candidates.Length; j++)
            {
                var implementation = containingType.FindImplementationForInterfaceMember(candidates[j]);
                if (SymbolEqualityComparer.Default.Equals(implementation, symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Counts the parameters a caller actually writes at the call site.</summary>
    /// <param name="parameterList">The declared parameter list.</param>
    /// <param name="options">The resolved settings.</param>
    /// <returns>The number of parameters that count toward the maximum.</returns>
    /// <remarks>
    /// An extension method's receiver is written as the receiver, and a caller-info parameter is supplied by
    /// the compiler; neither costs the caller an argument, so neither is counted. Optional parameters are
    /// counted by default: a long tail of them is still a long signature to read. Set
    /// <c>stylesharp.SST1472.count_optional_parameters = false</c> when only the required ones should count.
    /// </remarks>
    private static int CountCallerWrittenParameters(BaseParameterListSyntax parameterList, in ParameterCountOptions options)
    {
        var parameters = parameterList.Parameters;
        var count = 0;
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            if (i == 0 && ModifierListHelper.Contains(parameter.Modifiers, SyntaxKind.ThisKeyword))
            {
                continue;
            }

            // A caller-info parameter must have a default, so the attribute scan stays behind that test and
            // never runs for a required parameter.
            if (parameter.Default is not null
                && (!options.CountOptionalParameters || HasCallerInfoAttribute(parameter.AttributeLists)))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    /// <summary>Returns whether a parameter is supplied by the compiler rather than by the caller.</summary>
    /// <param name="lists">The parameter's attribute lists.</param>
    /// <returns><see langword="true"/> for a caller-info parameter.</returns>
    private static bool HasCallerInfoAttribute(SyntaxList<AttributeListSyntax> lists)
    {
        for (var i = 0; i < lists.Count; i++)
        {
            var attributes = lists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (IsCallerInfoName(GetSimpleName(attributes[j].Name)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether an attribute name is one of the caller-info attributes.</summary>
    /// <param name="name">The attribute's simple name.</param>
    /// <returns><see langword="true"/> when the compiler fills the parameter in at the call site.</returns>
    private static bool IsCallerInfoName(string name) => name is "CallerMemberName"
        or "CallerMemberNameAttribute"
        or "CallerFilePath"
        or "CallerFilePathAttribute"
        or "CallerLineNumber"
        or "CallerLineNumberAttribute"
        or "CallerArgumentExpression"
        or "CallerArgumentExpressionAttribute";

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased name.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };
}
