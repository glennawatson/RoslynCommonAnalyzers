// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an enum whose underlying type is not one the project allows (SST2313). An enum that names no
/// underlying type is stored as <c>int</c>, and <c>int</c> is what a reader, a serializer and an interop
/// signature all assume unless told otherwise.
/// </summary>
/// <remarks>
/// Naming a different storage type is a real decision — <c>byte</c> to pack a struct, <c>long</c> to carry more
/// than thirty-two flags, a fixed width to match a wire format — so the rule does not claim to know which types
/// a project should permit. The allowed list is configuration: <c>stylesharp.SST2313.allowed_enum_storage</c>,
/// or <c>stylesharp.allowed_enum_storage</c> across rules, naming the types either as C# keywords (<c>byte</c>)
/// or as CLR names (<c>Byte</c>). It defaults to <c>int</c> alone.
/// <para>
/// An enum with no base list names no underlying type and is already an <c>int</c>, so it is rejected on syntax
/// alone — which is most enums, and they never reach the semantic model or read a single option.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2313EnumStorageAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.EnumStorageShouldBeAllowed);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EnumDeclaration);
    }

    /// <summary>Reports one enum stored as a type the project does not allow.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // No base list means no underlying type was named, which means the enum is already an int.
        var declaration = (EnumDeclarationSyntax)context.Node;
        if (declaration.BaseList is null)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { EnumUnderlyingType: { } storage })
        {
            return;
        }

        var keyword = GetKeyword(storage.SpecialType);
        if (keyword.Length == 0)
        {
            return;
        }

        var options = EnumStorageOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree));
        if (options.Allows(keyword, storage.Name))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            DesignRules.EnumStorageShouldBeAllowed,
            declaration.Identifier.GetLocation(),
            declaration.Identifier.ValueText,
            keyword,
            options.AllowedStorage));
    }

    /// <summary>Gets the C# keyword for an enum's underlying type.</summary>
    /// <param name="storage">The underlying type's special type.</param>
    /// <returns>The keyword, or an empty string when the type is not one an enum can be stored as.</returns>
    private static string GetKeyword(SpecialType storage) => storage switch
    {
        SpecialType.System_SByte => "sbyte",
        SpecialType.System_Byte => "byte",
        SpecialType.System_Int16 => "short",
        SpecialType.System_UInt16 => "ushort",
        SpecialType.System_Int32 => "int",
        SpecialType.System_UInt32 => "uint",
        SpecialType.System_Int64 => "long",
        SpecialType.System_UInt64 => "ulong",
        _ => string.Empty,
    };
}
