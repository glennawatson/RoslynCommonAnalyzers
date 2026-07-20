// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags an LLM system-role message whose content is not a compile-time constant (SES1601). The
/// instruction/system channel of a chat model is its trusted template; a value computed at runtime --
/// a parameter, a field, an interpolated string -- can smuggle attacker-controlled text into that
/// channel and override the model's guardrails, a prompt-injection foot-gun. The rule reports the
/// content expression of a <c>Microsoft.Extensions.AI.ChatMessage</c> constructed with
/// <c>ChatRole.System</c>, and of a Semantic Kernel <c>ChatHistory.AddSystemMessage</c>,
/// <c>ChatHistory.AddMessage(AuthorRole.System, ...)</c>, or <c>ChatMessageContent(AuthorRole.System, ...)</c>,
/// when that content has no constant value. The role is confirmed by binding the argument to the
/// static <c>System</c> role property, so a non-system message and a constant system template are both
/// left alone. The rule resolves the AI types once per compilation and registers nothing when neither
/// Microsoft.Extensions.AI nor Semantic Kernel is referenced, so a project that cannot call these APIs
/// pays nothing. Detection is purely local -- the content expression itself must be non-constant; no
/// value is tracked across statements.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1601NonConstantSystemPromptAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the static <c>System</c> role property on both AI libraries' role types.</summary>
    private const string SystemRolePropertyName = "System";

    /// <summary>The Semantic Kernel <c>ChatHistory</c> convenience method that adds a system-role message.</summary>
    private const string AddSystemMessageMethodName = "AddSystemMessage";

    /// <summary>The Semantic Kernel <c>ChatHistory</c> method that adds a message with an explicit role.</summary>
    private const string AddMessageMethodName = "AddMessage";

    /// <summary>The reported channel label for a Semantic Kernel <c>AddSystemMessage</c> call.</summary>
    private const string AddSystemMessageChannel = "ChatHistory.AddSystemMessage";

    /// <summary>The reported channel label for a Semantic Kernel <c>AddMessage</c> call.</summary>
    private const string AddMessageChannel = "ChatHistory.AddMessage";

    /// <summary>The minimum argument count for a <c>(role, content, ...)</c> system-message shape.</summary>
    private const int RoleAndContentArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.NonConstantSystemPrompt);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var apis = LlmChatApis.Resolve(start.Compilation);
            if (apis is null)
            {
                return;
            }

            if (apis.HasMessageCreationTargets)
            {
                start.RegisterSyntaxNodeAction(
                    nodeContext => AnalyzeMessageCreation(nodeContext, apis),
                    SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.ImplicitObjectCreationExpression);
            }

            if (apis.HasChatHistory)
            {
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeChatHistoryCall(nodeContext, apis), SyntaxKind.InvocationExpression);
            }
        });
    }

    /// <summary>Reports SES1601 for a message type constructed with a system role and non-constant content.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="apis">The gated AI chat types resolved for the compilation.</param>
    private static void AnalyzeMessageCreation(SyntaxNodeAnalysisContext context, LlmChatApis apis)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;

        // Syntactic prefilter: only a (role, content, ...) construction can name a system prompt.
        if (creation.ArgumentList is not { Arguments.Count: >= RoleAndContentArgumentCount } argumentList)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || GetRoleTypeForMessage(constructor.ContainingType, apis) is not { } roleType
            || !IsRoleThenStringMethod(constructor, roleType))
        {
            return;
        }

        ReportIfSystemRoleWithNonConstantContent(context, argumentList, constructor, roleType, constructor.ContainingType.Name);
    }

    /// <summary>Reports SES1601 for a Semantic Kernel <c>ChatHistory</c> system-message call with non-constant content.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="apis">The gated AI chat types resolved for the compilation.</param>
    private static void AnalyzeChatHistoryCall(SyntaxNodeAnalysisContext context, LlmChatApis apis)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.AddSystemMessage(...)' or '.AddMessage(...)' call with arguments.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: AddSystemMessageMethodName or AddMessageMethodName }
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || apis.SemanticKernelChatHistory is not { } chatHistory
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, chatHistory))
        {
            return;
        }

        if (method.Name == AddSystemMessageMethodName)
        {
            ReportIfNonConstantContent(context, invocation.ArgumentList, method, 0, AddSystemMessageChannel);
            return;
        }

        if (apis.SemanticKernelAuthorRole is not { } authorRole || !IsRoleThenStringMethod(method, authorRole))
        {
            return;
        }

        ReportIfSystemRoleWithNonConstantContent(context, invocation.ArgumentList, method, authorRole, AddMessageChannel);
    }

    /// <summary>Reports the content when the role argument is the system role and the content is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="argumentList">The call's argument list.</param>
    /// <param name="method">The bound method or constructor whose parameter 0 is the role and 1 the content.</param>
    /// <param name="roleType">The gated role type whose <c>System</c> member marks a system message.</param>
    /// <param name="channel">The channel label reported in the diagnostic message.</param>
    private static void ReportIfSystemRoleWithNonConstantContent(SyntaxNodeAnalysisContext context, ArgumentListSyntax argumentList, IMethodSymbol method, INamedTypeSymbol roleType, string channel)
    {
        if (GetArgumentForParameter(argumentList, method, 0) is not { } roleArgument
            || !IsSystemRole(context.SemanticModel, roleArgument, roleType, context.CancellationToken))
        {
            return;
        }

        ReportIfNonConstantContent(context, argumentList, method, 1, channel);
    }

    /// <summary>Reports the content argument at an ordinal when it has no compile-time constant value.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="argumentList">The call's argument list.</param>
    /// <param name="method">The bound method or constructor.</param>
    /// <param name="contentOrdinal">The zero-based position of the string content parameter.</param>
    /// <param name="channel">The channel label reported in the diagnostic message.</param>
    private static void ReportIfNonConstantContent(SyntaxNodeAnalysisContext context, ArgumentListSyntax argumentList, IMethodSymbol method, int contentOrdinal, string channel)
    {
        if (GetArgumentForParameter(argumentList, method, contentOrdinal) is not { } contentArgument
            || HasConstantValue(context.SemanticModel, contentArgument, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.NonConstantSystemPrompt,
            contentArgument.SyntaxTree,
            contentArgument.Span,
            channel));
    }

    /// <summary>Returns whether a method's first parameter is a given role type and its second a <see cref="string"/>.</summary>
    /// <param name="method">The bound method or constructor.</param>
    /// <param name="roleType">The expected type of the first (role) parameter.</param>
    /// <returns><see langword="true"/> when the shape is <c>(role, string, ...)</c>.</returns>
    private static bool IsRoleThenStringMethod(IMethodSymbol method, INamedTypeSymbol roleType)
        => method.Parameters.Length >= RoleAndContentArgumentCount
            && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, roleType)
            && method.Parameters[1].Type.SpecialType == SpecialType.System_String;

    /// <summary>Returns the role type paired with a resolved message type, or <see langword="null"/> when the container is not a gated message type.</summary>
    /// <param name="containingType">The constructor's containing type.</param>
    /// <param name="apis">The gated AI chat types resolved for the compilation.</param>
    /// <returns>The role type whose <c>System</c> member marks a system message, or <see langword="null"/>.</returns>
    private static INamedTypeSymbol? GetRoleTypeForMessage(INamedTypeSymbol containingType, LlmChatApis apis)
    {
        if (SymbolEqualityComparer.Default.Equals(apis.ExtensionsChatMessage, containingType))
        {
            return apis.ExtensionsChatRole;
        }

        return SymbolEqualityComparer.Default.Equals(apis.SemanticKernelChatMessageContent, containingType)
            ? apis.SemanticKernelAuthorRole
            : null;
    }

    /// <summary>Returns the argument expression bound to a parameter ordinal, honouring an explicit name-colon.</summary>
    /// <param name="argumentList">The invocation or construction argument list.</param>
    /// <param name="method">The bound method or constructor.</param>
    /// <param name="parameterOrdinal">The zero-based parameter position.</param>
    /// <returns>The argument expression, or <see langword="null"/> when it cannot be identified.</returns>
    private static ExpressionSyntax? GetArgumentForParameter(ArgumentListSyntax argumentList, IMethodSymbol method, int parameterOrdinal)
    {
        // Callers only ask for a parameter position they have already proven exists on the bound method
        // (a system-message shape is (role, content, ...)), so the ordinal is always in range here.
        var parameterName = method.Parameters[parameterOrdinal].Name;
        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { } nameColon && nameColon.Name.Identifier.ValueText == parameterName)
            {
                return arguments[i].Expression;
            }
        }

        return parameterOrdinal < arguments.Count && arguments[parameterOrdinal].NameColon is null
            ? arguments[parameterOrdinal].Expression
            : null;
    }

    /// <summary>Returns whether an expression binds to the static <c>System</c> role property of a gated role type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The role argument expression.</param>
    /// <param name="roleType">The gated role type whose <c>System</c> member marks a system message.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression is the role type's <c>System</c> member.</returns>
    private static bool IsSystemRole(SemanticModel model, ExpressionSyntax expression, INamedTypeSymbol roleType, CancellationToken cancellationToken)
        => model.GetSymbolInfo(expression, cancellationToken).Symbol is IPropertySymbol { IsStatic: true, Name: SystemRolePropertyName } property
            && SymbolEqualityComparer.Default.Equals(property.ContainingType, roleType);

    /// <summary>Returns whether an expression has a compile-time constant value.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The content argument expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression is a compile-time constant.</returns>
    private static bool HasConstantValue(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken)
        => model.GetConstantValue(expression, cancellationToken).HasValue;

    /// <summary>The AI chat types SES1601 gates on, resolved once per compilation.</summary>
    private sealed class LlmChatApis
    {
        /// <summary>Initializes a new instance of the <see cref="LlmChatApis"/> class.</summary>
        /// <param name="extensionsChatMessage">The Microsoft.Extensions.AI <c>ChatMessage</c> type, if present.</param>
        /// <param name="extensionsChatRole">The Microsoft.Extensions.AI <c>ChatRole</c> type, if present.</param>
        /// <param name="semanticKernelChatHistory">The Semantic Kernel <c>ChatHistory</c> type, if present.</param>
        /// <param name="semanticKernelChatMessageContent">The Semantic Kernel <c>ChatMessageContent</c> type, if present.</param>
        /// <param name="semanticKernelAuthorRole">The Semantic Kernel <c>AuthorRole</c> type, if present.</param>
        private LlmChatApis(
            INamedTypeSymbol? extensionsChatMessage,
            INamedTypeSymbol? extensionsChatRole,
            INamedTypeSymbol? semanticKernelChatHistory,
            INamedTypeSymbol? semanticKernelChatMessageContent,
            INamedTypeSymbol? semanticKernelAuthorRole)
        {
            ExtensionsChatMessage = extensionsChatMessage;
            ExtensionsChatRole = extensionsChatRole;
            SemanticKernelChatHistory = semanticKernelChatHistory;
            SemanticKernelChatMessageContent = semanticKernelChatMessageContent;
            SemanticKernelAuthorRole = semanticKernelAuthorRole;
        }

        /// <summary>Gets the Microsoft.Extensions.AI <c>ChatMessage</c> type, or <see langword="null"/> when absent.</summary>
        public INamedTypeSymbol? ExtensionsChatMessage { get; }

        /// <summary>Gets the Microsoft.Extensions.AI <c>ChatRole</c> type, or <see langword="null"/> when absent.</summary>
        public INamedTypeSymbol? ExtensionsChatRole { get; }

        /// <summary>Gets the Semantic Kernel <c>ChatHistory</c> type, or <see langword="null"/> when absent.</summary>
        public INamedTypeSymbol? SemanticKernelChatHistory { get; }

        /// <summary>Gets the Semantic Kernel <c>ChatMessageContent</c> type, or <see langword="null"/> when absent.</summary>
        public INamedTypeSymbol? SemanticKernelChatMessageContent { get; }

        /// <summary>Gets the Semantic Kernel <c>AuthorRole</c> type, or <see langword="null"/> when absent.</summary>
        public INamedTypeSymbol? SemanticKernelAuthorRole { get; }

        /// <summary>Gets a value indicating whether a message-construction shape can be analysed.</summary>
        public bool HasMessageCreationTargets =>
            (ExtensionsChatMessage is not null && ExtensionsChatRole is not null)
            || (SemanticKernelChatMessageContent is not null && SemanticKernelAuthorRole is not null);

        /// <summary>Gets a value indicating whether a Semantic Kernel <c>ChatHistory</c> shape can be analysed.</summary>
        public bool HasChatHistory => SemanticKernelChatHistory is not null && SemanticKernelAuthorRole is not null;

        /// <summary>Resolves the AI chat types present in the compilation.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The resolved types, or <see langword="null"/> when no analysable shape is available.</returns>
        public static LlmChatApis? Resolve(Compilation compilation)
        {
            var apis = new LlmChatApis(
                compilation.GetTypeByMetadataName("Microsoft.Extensions.AI.ChatMessage"),
                compilation.GetTypeByMetadataName("Microsoft.Extensions.AI.ChatRole"),
                compilation.GetTypeByMetadataName("Microsoft.SemanticKernel.ChatCompletion.ChatHistory"),
                compilation.GetTypeByMetadataName("Microsoft.SemanticKernel.ChatMessageContent"),
                compilation.GetTypeByMetadataName("Microsoft.SemanticKernel.ChatCompletion.AuthorRole"));

            return apis.HasMessageCreationTargets || apis.HasChatHistory ? apis : null;
        }
    }
}
