// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Suggests the modern runtime argument-guard helpers in place of hand-written
/// checks: <c>ArgumentNullException.ThrowIfNull</c> (SST2000),
/// <c>ArgumentException.ThrowIfNullOrEmpty</c> (SST2001), and
/// <c>ArgumentException.ThrowIfNullOrWhiteSpace</c> (SST2002). Each helper is resolved
/// once per compilation by probing for the method, so a rule fires only where the
/// replacement actually exists in the referenced framework — no target framework
/// string is parsed and nothing is reported on older runtimes.
/// </summary>
/// <remarks>
/// Diagnostics: SST2000, SST2001, SST2002, SST2003, SST2004.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArgumentGuardAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernizationRules.UseThrowIfNull,
        ModernizationRules.UseThrowIfNullOrEmpty,
        ModernizationRules.UseThrowIfNullOrWhiteSpace,
        ModernizationRules.UseObjectDisposedThrowIf,
        ModernizationRules.UseArgumentOutOfRangeThrowIf);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var helpers = CreateHelpers(start.Compilation);

            if (!helpers.Any)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeIf(nodeContext, helpers), SyntaxKind.IfStatement);
        });
    }

    /// <summary>Returns whether an <c>if</c> statement matches any supported throw-helper pattern.</summary>
    /// <param name="ifStatement">The candidate if statement.</param>
    /// <param name="helpers">The guard helpers available in this scenario.</param>
    /// <returns><see langword="true"/> when the analyzer would report a diagnostic.</returns>
    internal static bool WouldReportForBenchmark(IfStatementSyntax ifStatement, in GuardHelpers helpers) =>
        (helpers.ThrowIfNull && ThrowGuardPatterns.TryMatchArgumentNull(ifStatement, out _))
        || (helpers.ThrowIfDisposed && !IsStaticContext(ifStatement) && ThrowGuardPatterns.TryMatchObjectDisposed(ifStatement, out _))
        || (ThrowGuardPatterns.TryMatchRangeGuard(ifStatement, out var rangeMatch) && HasRangeHelper(helpers.Range, rangeMatch.Helper))
        || (ThrowGuardPatterns.TryMatchStringGuard(ifStatement, out var guardMethod, out _) && IsStringGuardHelperAvailable(helpers, guardMethod!));

    /// <summary>Creates a helper set that enables every guard pattern currently supported by the analyzer.</summary>
    /// <returns>A helper set suitable for hot-path benchmarks.</returns>
    internal static GuardHelpers CreateBenchmarkHelpers()
        => new(
            ThrowIfNull: true,
            ThrowIfNullOrEmpty: true,
            ThrowIfNullOrWhiteSpace: true,
            ThrowIfDisposed: true,
            Range:
            [
                "ThrowIfNegative",
                "ThrowIfNegativeOrZero",
                "ThrowIfZero",
                "ThrowIfGreaterThan",
                "ThrowIfGreaterThanOrEqual",
                "ThrowIfLessThan",
                "ThrowIfLessThanOrEqual",
                "ThrowIfEqual",
                "ThrowIfNotEqual"
            ]);

    /// <summary>Creates the available guard-helper set for one compilation.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>The resolved guard-helper set.</returns>
    internal static GuardHelpers CreateHelpers(Compilation compilation)
    {
        var argumentNullException = compilation.GetTypeByMetadataName("System.ArgumentNullException");
        var argumentException = compilation.GetTypeByMetadataName("System.ArgumentException");
        var objectDisposedException = compilation.GetTypeByMetadataName("System.ObjectDisposedException");
        var argumentOutOfRangeException = compilation.GetTypeByMetadataName("System.ArgumentOutOfRangeException");
        ReadArgumentExceptionHelpers(argumentException, out var throwIfNullOrEmpty, out var throwIfNullOrWhiteSpace);

        return new(
            HasStaticMethod(argumentNullException, "ThrowIfNull"),
            throwIfNullOrEmpty,
            throwIfNullOrWhiteSpace,
            HasStaticMethod(objectDisposedException, "ThrowIf"),
            ResolveRangeHelpers(argumentOutOfRangeException));
    }

    /// <summary>Reads the string guard helpers exposed by <c>ArgumentException</c> in one pass.</summary>
    /// <param name="type">The resolved <c>ArgumentException</c> type, when available.</param>
    /// <param name="throwIfNullOrEmpty">Set to <see langword="true"/> when <c>ThrowIfNullOrEmpty</c> exists.</param>
    /// <param name="throwIfNullOrWhiteSpace">Set to <see langword="true"/> when <c>ThrowIfNullOrWhiteSpace</c> exists.</param>
    internal static void ReadArgumentExceptionHelpers(
        INamedTypeSymbol? type,
        out bool throwIfNullOrEmpty,
        out bool throwIfNullOrWhiteSpace)
    {
        throwIfNullOrEmpty = false;
        throwIfNullOrWhiteSpace = false;
        if (type is null)
        {
            return;
        }

        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is not IMethodSymbol { IsStatic: true, Name: var name })
            {
                continue;
            }

            switch (name)
            {
                case "ThrowIfNullOrEmpty":
                    {
                        throwIfNullOrEmpty = true;
                        break;
                    }

                case "ThrowIfNullOrWhiteSpace":
                    {
                        throwIfNullOrWhiteSpace = true;
                        break;
                    }
            }

            if (throwIfNullOrEmpty && throwIfNullOrWhiteSpace)
            {
                return;
            }
        }
    }

    /// <summary>Reports the applicable guard-helper suggestion for one if statement.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="helpers">The guard helpers available in this compilation.</param>
    private static void AnalyzeIf(SyntaxNodeAnalysisContext context, GuardHelpers helpers)
    {
        var ifStatement = (IfStatementSyntax)context.Node;

        if (helpers.ThrowIfNull && ThrowGuardPatterns.TryMatchArgumentNull(ifStatement, out var nullChecked))
        {
            context.ReportDiagnostic(Diagnostic.Create(ModernizationRules.UseThrowIfNull, ifStatement.GetLocation(), nullChecked!.ToString()));
            return;
        }

        if (helpers.ThrowIfDisposed
            && !IsStaticContext(ifStatement)
            && ThrowGuardPatterns.TryMatchObjectDisposed(ifStatement, out var disposedCondition))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    ModernizationRules.UseObjectDisposedThrowIf,
                    ifStatement.GetLocation(),
                    disposedCondition!.ToString()));
            return;
        }

        if (ThrowGuardPatterns.TryMatchRangeGuard(ifStatement, out var rangeMatch)
            && HasRangeHelper(helpers.Range, rangeMatch.Helper))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    ModernizationRules.UseArgumentOutOfRangeThrowIf,
                    ifStatement.GetLocation(),
                    rangeMatch.Helper));
            return;
        }

        if (!ThrowGuardPatterns.TryMatchStringGuard(ifStatement, out var guardMethod, out var stringChecked))
        {
            return;
        }

        ReportStringGuard(context, ifStatement, helpers, guardMethod!, stringChecked!);
    }

    /// <summary>Reports SST2001/SST2002 for a matched string guard when its helper is available.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="ifStatement">The matched if statement.</param>
    /// <param name="helpers">The guard helpers available in this compilation.</param>
    /// <param name="guardMethod">The matched guard method name.</param>
    /// <param name="checkedExpression">The checked string expression.</param>
    private static void ReportStringGuard(
        SyntaxNodeAnalysisContext context,
        IfStatementSyntax ifStatement,
        GuardHelpers helpers,
        string guardMethod,
        ExpressionSyntax checkedExpression)
    {
        var (available, rule) = guardMethod == ThrowGuardPatterns.IsNullOrEmpty
            ? (helpers.ThrowIfNullOrEmpty, ModernizationRules.UseThrowIfNullOrEmpty)
            : (helpers.ThrowIfNullOrWhiteSpace, ModernizationRules.UseThrowIfNullOrWhiteSpace);

        if (!available)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(rule, ifStatement.GetLocation(), checkedExpression.ToString()));
    }

    /// <summary>Returns whether the matching string-guard helper is available.</summary>
    /// <param name="helpers">The guard helpers available in this scenario.</param>
    /// <param name="guardMethod">The matched guard method name.</param>
    /// <returns><see langword="true"/> when the corresponding helper exists.</returns>
    private static bool IsStringGuardHelperAvailable(in GuardHelpers helpers, string guardMethod)
        => guardMethod == ThrowGuardPatterns.IsNullOrEmpty ? helpers.ThrowIfNullOrEmpty : helpers.ThrowIfNullOrWhiteSpace;

    /// <summary>Returns whether a type has a static method with the given name.</summary>
    /// <param name="type">The type symbol, when resolved.</param>
    /// <param name="methodName">The method name to look for.</param>
    /// <returns><see langword="true"/> when the type exists and declares such a method.</returns>
    private static bool HasStaticMethod(INamedTypeSymbol? type, string methodName)
    {
        if (type is null)
        {
            return false;
        }

        var members = type.GetMembers(methodName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the available out-of-range helper names once per compilation.</summary>
    /// <param name="type">The resolved <c>ArgumentOutOfRangeException</c> type, when available.</param>
    /// <returns>The available helper names.</returns>
    private static string[] ResolveRangeHelpers(INamedTypeSymbol? type)
    {
        if (type is null)
        {
            return [];
        }

        var members = type.GetMembers();
        var count = 0;
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: true, Name: var name } && name.StartsWith("ThrowIf", StringComparison.Ordinal))
            {
                count++;
            }
        }

        if (count == 0)
        {
            return [];
        }

        var helpers = new string[count];
        var index = 0;
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is not IMethodSymbol { IsStatic: true, Name: var name } ||
                !name.StartsWith("ThrowIf", StringComparison.Ordinal))
            {
                continue;
            }

            helpers[index] = name;
            index++;
        }

        return helpers;
    }

    /// <summary>Returns whether the resolved range-helper array contains the supplied helper name.</summary>
    /// <param name="helpers">The resolved range-helper names.</param>
    /// <param name="helper">The helper name to find.</param>
    /// <returns><see langword="true"/> when the helper is available.</returns>
    private static bool HasRangeHelper(string[] helpers, string helper)
    {
        for (var i = 0; i < helpers.Length; i++)
        {
            if (helpers[i] == helper)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an if statement is inside a static member or static local function.</summary>
    /// <param name="statement">The statement.</param>
    /// <returns><see langword="true"/> when <c>this</c> is unavailable.</returns>
    private static bool IsStaticContext(IfStatementSyntax statement)
    {
        for (var current = statement.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case LocalFunctionStatementSyntax local:
                    return ModifierListHelper.Contains(local.Modifiers, SyntaxKind.StaticKeyword);
                case MemberDeclarationSyntax member:
                    return ModifierListHelper.Contains(member.Modifiers, SyntaxKind.StaticKeyword);
            }
        }

        return true;
    }

    /// <summary>The guard helpers available in a compilation's referenced framework.</summary>
    /// <param name="ThrowIfNull">Whether <c>ArgumentNullException.ThrowIfNull</c> exists.</param>
    /// <param name="ThrowIfNullOrEmpty">Whether <c>ArgumentException.ThrowIfNullOrEmpty</c> exists.</param>
    /// <param name="ThrowIfNullOrWhiteSpace">Whether <c>ArgumentException.ThrowIfNullOrWhiteSpace</c> exists.</param>
    /// <param name="ThrowIfDisposed">Whether <c>ObjectDisposedException.ThrowIf</c> exists.</param>
    /// <param name="Range">The available <c>ArgumentOutOfRangeException</c> helper names.</param>
    internal readonly record struct GuardHelpers(
        bool ThrowIfNull,
        bool ThrowIfNullOrEmpty,
        bool ThrowIfNullOrWhiteSpace,
        bool ThrowIfDisposed,
        string[] Range)
    {
        /// <summary>Gets a value indicating whether any guard helper is available.</summary>
        public bool Any => ThrowIfNull || ThrowIfNullOrEmpty || ThrowIfNullOrWhiteSpace || ThrowIfDisposed || Range.Length > 0;
    }
}
