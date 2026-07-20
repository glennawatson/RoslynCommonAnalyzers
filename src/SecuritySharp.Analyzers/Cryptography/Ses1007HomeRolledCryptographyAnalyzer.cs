// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a type that implements a cryptographic primitive by hand (SES1007). The rule reports a class
/// whose base chain reaches one of the abstract primitive bases in <c>System.Security.Cryptography</c> --
/// <c>HashAlgorithm</c>, <c>KeyedHashAlgorithm</c>, <c>HMAC</c>, <c>SymmetricAlgorithm</c>,
/// <c>AsymmetricAlgorithm</c>, or <c>DeriveBytes</c> -- because those bases exist to be overridden with the
/// actual transform, so deriving from one means writing your own hash, MAC, cipher, key-exchange, or
/// key-derivation algorithm. Subclassing a concrete, named algorithm (for example <c>SHA256</c>, <c>Aes</c>,
/// <c>HMACSHA256</c>, or <c>RSA</c>) to configure it is not reported: the walk stops at the first
/// already-compiled algorithm in the chain, so only a chain that reaches a primitive base through source-only
/// intermediates is flagged. The syntactic prefilter is a class with a base list; the semantic model is
/// touched only once that shape matches. The rule resolves the primitive bases once per compilation and
/// registers nothing when none are present, so a project without <c>System.Security.Cryptography</c> pays
/// nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1007HomeRolledCryptographyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata names of the abstract primitive bases whose direct implementation is the smell.</summary>
    private static readonly string[] PrimitiveBaseMetadataNames =
    [
        "System.Security.Cryptography.HashAlgorithm",
        "System.Security.Cryptography.KeyedHashAlgorithm",
        "System.Security.Cryptography.HMAC",
        "System.Security.Cryptography.SymmetricAlgorithm",
        "System.Security.Cryptography.AsymmetricAlgorithm",
        "System.Security.Cryptography.DeriveBytes",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.HomeRolledCryptography);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // Resolve the primitive bases once. Without System.Security.Cryptography none resolve, so nothing
            // is registered and the clean path costs nothing.
            var primitiveBases = ResolvePrimitiveBases(start.Compilation);
            if (primitiveBases is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeClass(nodeContext, primitiveBases), SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>Resolves the abstract primitive base symbols present in the compilation, or <see langword="null"/> when none are.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>The resolved primitive base symbols, or <see langword="null"/> when the crypto assembly is absent.</returns>
    private static INamedTypeSymbol[]? ResolvePrimitiveBases(Compilation compilation)
    {
        var resolved = new INamedTypeSymbol[PrimitiveBaseMetadataNames.Length];
        var count = 0;
        for (var i = 0; i < PrimitiveBaseMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(PrimitiveBaseMetadataNames[i]) is { } type)
            {
                resolved[count] = type;
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        if (count == resolved.Length)
        {
            return resolved;
        }

        var trimmed = new INamedTypeSymbol[count];
        Array.Copy(resolved, trimmed, count);
        return trimmed;
    }

    /// <summary>Reports SES1007 for a class whose base chain reaches an abstract primitive base through source-only intermediates.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="primitiveBases">The resolved abstract primitive base symbols to match against.</param>
    private static void AnalyzeClass(SyntaxNodeAnalysisContext context, INamedTypeSymbol[] primitiveBases)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // Syntactic prefilter: only a class with a base list can derive from a primitive base.
        if (classDeclaration.BaseList is not { Types.Count: > 0 })
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken) is not { } classSymbol)
        {
            return;
        }

        if (FindPrimitiveBase(classSymbol, primitiveBases) is not { } primitiveBase)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.HomeRolledCryptography,
            classDeclaration.SyntaxTree,
            classDeclaration.Identifier.Span,
            classSymbol.Name,
            primitiveBase.Name));
    }

    /// <summary>Walks a class's base chain and returns the abstract primitive base it implements, if any.</summary>
    /// <param name="classSymbol">The class being inspected.</param>
    /// <param name="primitiveBases">The resolved abstract primitive base symbols to match against.</param>
    /// <returns>
    /// The matched primitive base, or <see langword="null"/>. A custom source intermediate is walked through, but
    /// the walk stops at the first already-compiled type -- a concrete, named algorithm being configured -- so a
    /// chain that reaches a primitive only through such an algorithm is not reported.
    /// </returns>
    private static INamedTypeSymbol? FindPrimitiveBase(INamedTypeSymbol classSymbol, INamedTypeSymbol[] primitiveBases)
    {
        for (var current = classSymbol.BaseType; current is not null; current = current.BaseType)
        {
            if (MatchPrimitiveBase(current, primitiveBases) is { } match)
            {
                return match;
            }

            // A metadata type in the chain is either a primitive base (handled above) or a concrete algorithm
            // being configured; either way, stop before crediting a deeper primitive to a subclass that only
            // reuses a vetted implementation. Custom source intermediates are walked through.
            if (current.DeclaringSyntaxReferences.IsDefaultOrEmpty)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Returns the primitive base equal to <paramref name="type"/>, or <see langword="null"/>.</summary>
    /// <param name="type">The base-chain type to test.</param>
    /// <param name="primitiveBases">The resolved abstract primitive base symbols to match against.</param>
    /// <returns>The matched primitive base, or <see langword="null"/> when the type is not one.</returns>
    private static INamedTypeSymbol? MatchPrimitiveBase(INamedTypeSymbol type, INamedTypeSymbol[] primitiveBases)
    {
        for (var i = 0; i < primitiveBases.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(type, primitiveBases[i]))
            {
                return primitiveBases[i];
            }
        }

        return null;
    }
}
