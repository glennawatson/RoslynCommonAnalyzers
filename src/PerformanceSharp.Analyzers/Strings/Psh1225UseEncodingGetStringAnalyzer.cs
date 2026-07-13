// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports bytes decoded into a <c>char[]</c> only to be handed straight to the string constructor
/// (PSH1225) — <c>new string(encoding.GetChars(bytes))</c>. The char buffer is allocated, decoded
/// into, and then copied again into the string that gets returned; the buffer is garbage the moment
/// the constructor returns. <c>Encoding.GetString</c> decodes into the string directly.
/// </summary>
/// <remarks>
/// <para>
/// The rewrite reuses the arguments untouched, so it maps <c>GetChars(bytes)</c> onto
/// <c>GetString(bytes)</c> and <c>GetChars(bytes, index, count)</c> onto
/// <c>GetString(bytes, index, count)</c>. The two decode with the same encoding, the same decoder
/// fallback, and the same handling of a partial sequence at the end of the input, so the resulting
/// string is character-for-character what the buffer held.
/// </para>
/// <para>
/// <b>The sibling overload is probed, never assumed.</b> The rule does not hard-code
/// <see cref="System.Text.Encoding"/>'s overload list. It takes the <c>GetChars</c> that actually
/// bound, looks for a <c>GetString</c> on the same type whose parameters match it exactly, and then
/// binds the rewritten call. A custom <see cref="System.Text.Encoding"/> subclass that adds a
/// <c>GetChars</c> without the matching <c>GetString</c> is left alone, and so is any target framework
/// that lacks the pair — the suggestion is only ever made where the author can act on it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1225UseEncodingGetStringAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The decoded member name the syntax gate requires.</summary>
    internal const string GetCharsMethodName = "GetChars";

    /// <summary>The replacement member name.</summary>
    internal const string GetStringMethodName = "GetString";

    /// <summary>The metadata name of the encoding base type a reported receiver must derive from.</summary>
    private const string EncodingMetadataName = "System.Text.Encoding";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseEncodingGetString);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(EncodingMetadataName) is not { } encoding)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeStringCreation(nodeContext, encoding),
                SyntaxKind.ObjectCreationExpression);
        });
    }

    /// <summary>Returns the <c>GetChars</c> call a <c>new string(...)</c> is built from, syntactically.</summary>
    /// <param name="creation">The candidate string creation.</param>
    /// <returns>The decoding call, or <see langword="null"/> when the shape does not match.</returns>
    internal static InvocationExpressionSyntax? TryGetDecodeCall(ObjectCreationExpressionSyntax creation)
    {
        if (creation.Type is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.StringKeyword }
            || creation.Initializer is not null
            || creation.ArgumentList is not { Arguments.Count: 1 } arguments
            || arguments.Arguments[0] is not { NameColon: null, RefOrOutKeyword.RawKind: (int)SyntaxKind.None } argument
            || argument.Expression is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            || access.Name.Identifier.ValueText != GetCharsMethodName)
        {
            return null;
        }

        return invocation;
    }

    /// <summary>Builds the <c>encoding.GetString(...)</c> rewrite, reusing the decoding arguments.</summary>
    /// <param name="decode">The reported <c>GetChars</c> call.</param>
    /// <returns>The rewritten call.</returns>
    internal static InvocationExpressionSyntax BuildGetString(InvocationExpressionSyntax decode)
    {
        var access = (MemberAccessExpressionSyntax)decode.Expression;
        return decode
            .WithExpression(access.WithName(SyntaxFactory.IdentifierName(GetStringMethodName)))
            .WithoutTrivia();
    }

    /// <summary>Confirms the rewrite binds to a <c>GetString</c> on the same encoding, returning a string.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The reported expression's position.</param>
    /// <param name="rewritten">The rewritten call.</param>
    /// <param name="decode">The bound <c>GetChars</c> method.</param>
    /// <returns><see langword="true"/> when the fix compiles and decodes the same way.</returns>
    internal static bool RewriteBindsToGetString(
        SemanticModel model,
        int position,
        InvocationExpressionSyntax rewritten,
        IMethodSymbol decode)
    {
        var symbol = model.GetSpeculativeSymbolInfo(position, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol;
        return symbol is IMethodSymbol { IsStatic: false, Name: GetStringMethodName, ReturnType.SpecialType: SpecialType.System_String } resolved
            && SymbolEqualityComparer.Default.Equals(resolved.ContainingType, decode.ContainingType)
            && HasSameParameters(resolved, decode);
    }

    /// <summary>Reports PSH1225 for a decode-then-copy that <c>GetString</c> does in one step.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="encoding">The encoding base type.</param>
    private static void AnalyzeStringCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol encoding)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (TryGetDecodeCall(creation) is not { } decodeCall)
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        if (BindDecode(model, decodeCall, encoding, cancellationToken) is not { } decode
            || !BuildsAString(model, creation, cancellationToken)
            || SpanRewriteGuard.IsInsideExpressionTree(creation, model, cancellationToken)
            || !RewriteBindsToGetString(model, creation.SpanStart, BuildGetString(decodeCall), decode))
        {
            return;
        }

        var receiver = ((MemberAccessExpressionSyntax)decodeCall.Expression).Expression;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseEncodingGetString,
            creation.SyntaxTree,
            creation.Span,
            receiver + "." + GetStringMethodName));
    }

    /// <summary>Binds the decoding call and keeps it only when it is an encoding's own <c>GetChars</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="decodeCall">The <c>GetChars</c> invocation.</param>
    /// <param name="encoding">The encoding base type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The bound method, or <see langword="null"/> when it is not an encoding decode.</returns>
    private static IMethodSymbol? BindDecode(
        SemanticModel model,
        InvocationExpressionSyntax decodeCall,
        INamedTypeSymbol encoding,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(decodeCall, cancellationToken).Symbol is not IMethodSymbol decode
            || decode.IsStatic
            || decode.Name != GetCharsMethodName
            || decode.ReturnType is not IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Char })
        {
            return null;
        }

        return DerivesFrom(decode.ContainingType, encoding) ? decode : null;
    }

    /// <summary>Returns whether a type is, or derives from, the encoding base type.</summary>
    /// <param name="type">The candidate receiver type.</param>
    /// <param name="encoding">The encoding base type.</param>
    /// <returns><see langword="true"/> when the type is an encoding.</returns>
    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol encoding)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, encoding))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the creation really is the <c>string(char[])</c> constructor.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="creation">The string creation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the constructor takes the char buffer whole.</returns>
    private static bool BuildsAString(SemanticModel model, ObjectCreationExpressionSyntax creation, CancellationToken cancellationToken)
        => model.GetSymbolInfo(creation, cancellationToken).Symbol is IMethodSymbol
        {
            MethodKind: MethodKind.Constructor,
            ContainingType.SpecialType: SpecialType.System_String,
            Parameters: [{ Type: IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Char } }],
        };

    /// <summary>Returns whether two methods take exactly the same parameter types.</summary>
    /// <param name="first">The first method.</param>
    /// <param name="second">The second method.</param>
    /// <returns><see langword="true"/> when the parameter lists match.</returns>
    private static bool HasSameParameters(IMethodSymbol first, IMethodSymbol second)
    {
        if (first.Parameters.Length != second.Parameters.Length)
        {
            return false;
        }

        for (var i = 0; i < first.Parameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(first.Parameters[i].Type, second.Parameters[i].Type))
            {
                return false;
            }
        }

        return true;
    }
}
