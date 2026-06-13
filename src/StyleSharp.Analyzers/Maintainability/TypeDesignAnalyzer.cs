// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped maintainability analyzer for type-shape conventions: constructor accessibility on abstract
/// types, static members on generic types, and classes that could be static.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST1428 — an abstract type declares a <c>public</c> constructor.</description></item>
/// <item><description>SST1431 — a <c>static</c> member of a generic type ignores the type's type parameters.</description></item>
/// <item><description>SST1432 — a class declares only static members and could be marked <c>static</c> (opt-in).</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeDesignAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.NoPublicConstructorOnAbstractType,
        MaintainabilityRules.StaticMemberShouldUseTypeParameter,
        MaintainabilityRules.MakeClassStatic);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(
            AnalyzeStaticMember,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.FieldDeclaration,
            SyntaxKind.EventFieldDeclaration);
    }

    /// <summary>Returns whether a class member keeps the type eligible to be marked <c>static</c>.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member is static, a constant, a nested type, or a static constructor.</returns>
    internal static bool IsStaticCompatibleMember(MemberDeclarationSyntax member) => member switch
    {
        BaseTypeDeclarationSyntax => true,
        DelegateDeclarationSyntax => true,
        ConstructorDeclarationSyntax constructor => ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword),
        FieldDeclarationSyntax field => ModifierListHelper.ContainsEither(field.Modifiers, SyntaxKind.StaticKeyword, SyntaxKind.ConstKeyword),
        _ => ModifierListHelper.Contains(member.Modifiers, SyntaxKind.StaticKeyword)
    };

    /// <summary>Reports SST1428 for a <c>public</c> constructor declared on an abstract type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;
        if (ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword)
            || constructor.Parent is not TypeDeclarationSyntax type
            || !ModifierListHelper.Contains(type.Modifiers, SyntaxKind.AbstractKeyword))
        {
            return;
        }

        for (var i = 0; i < constructor.Modifiers.Count; i++)
        {
            if (constructor.Modifiers[i].IsKind(SyntaxKind.PublicKeyword))
            {
                context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoPublicConstructorOnAbstractType, constructor.Modifiers[i].GetLocation()));
                return;
            }
        }
    }

    /// <summary>Reports SST1432 for a non-static class whose members are all static-compatible.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;

        // A base list, a primary constructor, or an existing static/abstract marker rules the class out.
        if (declaration.Members.Count == 0
            || declaration.BaseList is not null
            || declaration.ParameterList is not null
            || ModifierListHelper.ContainsEither(declaration.Modifiers, SyntaxKind.StaticKeyword, SyntaxKind.AbstractKeyword))
        {
            return;
        }

        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (!IsStaticCompatibleMember(members[i]))
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.MakeClassStatic, declaration.Identifier.GetLocation(), declaration.Identifier.ValueText));
    }

    /// <summary>Reports SST1431 for an externally visible static member of a generic type that ignores its type parameters.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStaticMember(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberDeclarationSyntax)context.Node;
        if (!ModifierListHelper.Contains(member.Modifiers, SyntaxKind.StaticKeyword)
            || !IsExternallyVisible(member.Modifiers)
            || member.Parent is not TypeDeclarationSyntax { TypeParameterList: { } typeParameters })
        {
            return;
        }

        if (SignatureMentionsAnyTypeParameter(member, typeParameters))
        {
            return;
        }

        var identifier = GetMemberIdentifier(member);
        if (identifier is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.StaticMemberShouldUseTypeParameter, identifier.Value.GetLocation(), identifier.Value.ValueText));
    }

    /// <summary>Returns whether a member is reachable outside its type (so callers see the awkward type argument).</summary>
    /// <param name="modifiers">The member modifiers.</param>
    /// <returns><see langword="true"/> for a <c>public</c>, <c>internal</c>, or <c>protected</c> member.</returns>
    private static bool IsExternallyVisible(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].Kind() is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword or SyntaxKind.ProtectedKeyword)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a member's signature types reference any of the type's type parameters.</summary>
    /// <param name="member">The static member.</param>
    /// <param name="typeParameters">The enclosing type's type parameters.</param>
    /// <returns><see langword="true"/> when at least one type parameter appears in the signature.</returns>
    private static bool SignatureMentionsAnyTypeParameter(MemberDeclarationSyntax member, TypeParameterListSyntax typeParameters) => member switch
    {
        MethodDeclarationSyntax method => MethodSignatureMentionsAnyParameter(method, typeParameters),
        PropertyDeclarationSyntax property => TypeMentionsAnyParameter(property.Type, typeParameters),
        FieldDeclarationSyntax field => TypeMentionsAnyParameter(field.Declaration.Type, typeParameters),
        EventFieldDeclarationSyntax eventField => TypeMentionsAnyParameter(eventField.Declaration.Type, typeParameters),
        _ => true
    };

    /// <summary>Returns whether a method's return type or parameter types reference any type parameter.</summary>
    /// <param name="method">The static method.</param>
    /// <param name="typeParameters">The enclosing type's type parameters.</param>
    /// <returns><see langword="true"/> when the method signature uses a type parameter.</returns>
    private static bool MethodSignatureMentionsAnyParameter(MethodDeclarationSyntax method, TypeParameterListSyntax typeParameters)
    {
        if (TypeMentionsAnyParameter(method.ReturnType, typeParameters))
        {
            return true;
        }

        var parameters = method.ParameterList.Parameters;
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Type is { } parameterType && TypeMentionsAnyParameter(parameterType, typeParameters))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type syntax names any of the given type parameters.</summary>
    /// <param name="type">The type syntax to scan.</param>
    /// <param name="typeParameters">The type parameters to look for.</param>
    /// <returns><see langword="true"/> when an identifier in the type matches a type parameter name.</returns>
    private static bool TypeMentionsAnyParameter(TypeSyntax type, TypeParameterListSyntax typeParameters)
    {
        foreach (var token in type.DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken))
            {
                continue;
            }

            var parameters = typeParameters.Parameters;
            for (var i = 0; i < parameters.Count; i++)
            {
                if (string.Equals(parameters[i].Identifier.ValueText, token.ValueText, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns the name token a static member should be reported on, or <see langword="null"/>.</summary>
    /// <param name="member">The static member.</param>
    /// <returns>The member's identifier token, or <see langword="null"/> for a member with no single name.</returns>
    private static SyntaxToken? GetMemberIdentifier(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier,
        PropertyDeclarationSyntax property => property.Identifier,
        FieldDeclarationSyntax { Declaration.Variables: [var single] } => single.Identifier,
        EventFieldDeclarationSyntax { Declaration.Variables: [var single] } => single.Identifier,
        _ => null
    };
}
