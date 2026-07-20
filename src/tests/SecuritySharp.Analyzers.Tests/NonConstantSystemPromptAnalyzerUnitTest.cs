// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeSystemPrompt = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1601NonConstantSystemPromptAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1601 (an LLM system-role message must not carry non-constant content).</summary>
public class NonConstantSystemPromptAnalyzerUnitTest
{
    /// <summary>Inline stubs of the Microsoft.Extensions.AI chat types the rule gates on.</summary>
    private const string ExtensionsAiStubs = """

                                             namespace Microsoft.Extensions.AI
                                             {
                                                 public readonly struct ChatRole
                                                 {
                                                     public ChatRole(string value) => Value = value;

                                                     public string Value { get; }

                                                     public static ChatRole System => new ChatRole("system");

                                                     public static ChatRole User => new ChatRole("user");

                                                     public static ChatRole Assistant => new ChatRole("assistant");
                                                 }

                                                 public sealed class ChatMessage
                                                 {
                                                     public ChatMessage()
                                                     {
                                                     }

                                                     public ChatMessage(ChatRole role, string content)
                                                     {
                                                         Role = role;
                                                         Text = content;
                                                     }

                                                     public ChatMessage(ChatRole role, object[] contents)
                                                     {
                                                         Role = role;
                                                     }

                                                     public ChatRole Role { get; set; }

                                                     public string Text { get; } = string.Empty;
                                                 }
                                             }
                                             """;

    /// <summary>Inline stubs of the Semantic Kernel chat types the rule gates on.</summary>
    private const string SemanticKernelStubs = """

                                               namespace Microsoft.SemanticKernel.ChatCompletion
                                               {
                                                   public readonly struct AuthorRole
                                                   {
                                                       public AuthorRole(string label) => Label = label;

                                                       public string Label { get; }

                                                       public static AuthorRole System => new AuthorRole("system");

                                                       public static AuthorRole User => new AuthorRole("user");
                                                   }

                                                   public class ChatHistory
                                                   {
                                                       public void AddSystemMessage(string content)
                                                       {
                                                       }

                                                       public void AddMessage(AuthorRole authorRole, string content)
                                                       {
                                                       }

                                                       public void AddMessage(AuthorRole authorRole, object[] items)
                                                       {
                                                       }
                                                   }
                                               }

                                               namespace Microsoft.SemanticKernel
                                               {
                                                   using Microsoft.SemanticKernel.ChatCompletion;

                                                   public class ChatMessageContent
                                                   {
                                                       public ChatMessageContent()
                                                       {
                                                       }

                                                       public ChatMessageContent(AuthorRole role, string content)
                                                       {
                                                           Role = role;
                                                           Content = content;
                                                       }

                                                       public AuthorRole Role { get; set; }

                                                       public string Content { get; } = string.Empty;
                                                   }
                                               }
                                               """;

    /// <summary>Verifies a ChatMessage built with ChatRole.System and a parameter as content is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExtensionsChatMessageSystemRoleNonConstantReportedAsync()
        => await VerifyExtensionsAsync(
            """
            using Microsoft.Extensions.AI;

            public class C
            {
                public ChatMessage M(string userData) => new ChatMessage(ChatRole.System, {|SES1601:userData|});
            }
            """);

    /// <summary>Verifies a target-typed ChatMessage system message with non-constant content is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExtensionsChatMessageTargetTypedNewReportedAsync()
        => await VerifyExtensionsAsync(
            """
            using Microsoft.Extensions.AI;

            public class C
            {
                public ChatMessage M(string userData)
                {
                    ChatMessage message = new(ChatRole.System, {|SES1601:userData|});
                    return message;
                }
            }
            """);

    /// <summary>Verifies an interpolated string in the system channel is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExtensionsChatMessageInterpolatedContentReportedAsync()
        => await VerifyExtensionsAsync(
            """
            using Microsoft.Extensions.AI;

            public class C
            {
                public ChatMessage M(string userName) => new ChatMessage(ChatRole.System, {|SES1601:$"You are assisting {userName}."|});
            }
            """);

    /// <summary>Verifies the content is reported when the role and content are passed by name in reverse order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExtensionsChatMessageNamedArgumentsReportedAsync()
        => await VerifyExtensionsAsync(
            """
            using Microsoft.Extensions.AI;

            public class C
            {
                public ChatMessage M(string userData) => new ChatMessage(content: {|SES1601:userData|}, role: ChatRole.System);
            }
            """);

    /// <summary>Verifies a Semantic Kernel ChatHistory.AddSystemMessage with non-constant content is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SemanticKernelAddSystemMessageNonConstantReportedAsync()
        => await VerifySemanticKernelAsync(
            """
            using Microsoft.SemanticKernel.ChatCompletion;

            public class C
            {
                public void M(ChatHistory history, string userData) => history.AddSystemMessage({|SES1601:userData|});
            }
            """);

    /// <summary>Verifies a Semantic Kernel ChatHistory.AddMessage with AuthorRole.System is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SemanticKernelAddMessageSystemRoleReportedAsync()
        => await VerifySemanticKernelAsync(
            """
            using Microsoft.SemanticKernel.ChatCompletion;

            public class C
            {
                public void M(ChatHistory history, string userData) => history.AddMessage(AuthorRole.System, {|SES1601:userData|});
            }
            """);

    /// <summary>Verifies a Semantic Kernel ChatMessageContent built with AuthorRole.System is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SemanticKernelChatMessageContentSystemRoleReportedAsync()
        => await VerifySemanticKernelAsync(
            """
            using Microsoft.SemanticKernel;
            using Microsoft.SemanticKernel.ChatCompletion;

            public class C
            {
                public ChatMessageContent M(string userData) => new ChatMessageContent(AuthorRole.System, {|SES1601:userData|});
            }
            """);

    /// <summary>Verifies a constant literal system prompt is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantLiteralSystemPromptIsCleanAsync()
        => await VerifyExtensionsAsync(
            """
            using Microsoft.Extensions.AI;

            public class C
            {
                public ChatMessage M() => new ChatMessage(ChatRole.System, "You are a helpful assistant.");
            }
            """);

    /// <summary>Verifies a system prompt referencing a const is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstReferenceSystemPromptIsCleanAsync()
        => await VerifyExtensionsAsync(
            """
            using Microsoft.Extensions.AI;

            public class C
            {
                private const string Prompt = "You are a helpful assistant.";

                public ChatMessage M() => new ChatMessage(ChatRole.System, Prompt);
            }
            """);

    /// <summary>Verifies non-constant content on a non-system role is not reported (user data belongs there).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserRoleNonConstantContentIsCleanAsync()
        => await VerifyExtensionsAsync(
            """
            using Microsoft.Extensions.AI;

            public class C
            {
                public ChatMessage M(string userData) => new ChatMessage(ChatRole.User, userData);
            }
            """);

    /// <summary>Verifies a ChatMessage built from a non-string content overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStringContentOverloadIsCleanAsync()
        => await VerifyExtensionsAsync(
            """
            using Microsoft.Extensions.AI;

            public class C
            {
                public ChatMessage M(object[] parts) => new ChatMessage(ChatRole.System, parts);
            }
            """);

    /// <summary>Verifies an unrelated two-argument construction is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedTwoArgumentConstructionIsCleanAsync()
        => await VerifyExtensionsAsync(
            """
            public sealed class Pair
            {
                public Pair(string first, string second)
                {
                }
            }

            public class C
            {
                public Pair M(string a, string b) => new Pair(a, b);
            }
            """);

    /// <summary>Verifies a construction whose constructor does not bind statically (a dynamic argument) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DynamicallyBoundConstructionIsCleanAsync()
        => await VerifyExtensionsAsync(
            """
            using Microsoft.Extensions.AI;

            public class C
            {
                public ChatMessage M(dynamic role, string content) => new ChatMessage(role, content);
            }
            """);

    /// <summary>Verifies a Semantic Kernel AddMessage bound to a non-string content overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SemanticKernelAddMessageNonStringOverloadIsCleanAsync()
        => await VerifySemanticKernelAsync(
            """
            using Microsoft.SemanticKernel.ChatCompletion;

            public class C
            {
                public void M(ChatHistory history, object[] items) => history.AddMessage(AuthorRole.System, items);
            }
            """);

    /// <summary>Verifies an invocation with no member-access receiver (a local function call) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonMemberInvocationIsCleanAsync()
        => await VerifySemanticKernelAsync(
            """
            public class C
            {
                public void M()
                {
                    void AddSystemMessage(string content)
                    {
                    }

                    AddSystemMessage("You are a helpful assistant.");
                }
            }
            """);

    /// <summary>Verifies a same-named zero-argument message call is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroArgumentMessageCallIsCleanAsync()
        => await VerifySemanticKernelAsync(
            """
            public sealed class Notifier
            {
                public void AddMessage()
                {
                }
            }

            public class C
            {
                public void M(Notifier notifier) => notifier.AddMessage();
            }
            """);

    /// <summary>Verifies AddMessage with a non-system role and non-constant content is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SemanticKernelAddMessageUserRoleIsCleanAsync()
        => await VerifySemanticKernelAsync(
            """
            using Microsoft.SemanticKernel.ChatCompletion;

            public class C
            {
                public void M(ChatHistory history, string userData) => history.AddMessage(AuthorRole.User, userData);
            }
            """);

    /// <summary>Verifies a constant AddSystemMessage is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SemanticKernelConstantAddSystemMessageIsCleanAsync()
        => await VerifySemanticKernelAsync(
            """
            using Microsoft.SemanticKernel.ChatCompletion;

            public class C
            {
                public void M(ChatHistory history) => history.AddSystemMessage("You are a helpful assistant.");
            }
            """);

    /// <summary>Verifies a same-named AddSystemMessage on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedMethodOnUnrelatedTypeIsCleanAsync()
        => await VerifySemanticKernelAsync(
            """
            public sealed class Logger
            {
                public void AddSystemMessage(string content)
                {
                }
            }

            public class C
            {
                public void M(Logger logger, string data) => logger.AddSystemMessage(data);
            }
            """);

    /// <summary>Verifies the rule stays silent when neither AI library is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenAiLibrariesUnavailableAsync()
    {
        const string Source = """
                              public readonly struct ChatRole
                              {
                                  public static ChatRole System => default;
                              }

                              public sealed class ChatMessage
                              {
                                  public ChatMessage(ChatRole role, string content)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public ChatMessage M(string userData) => new ChatMessage(ChatRole.System, userData);
                              }
                              """;

        await RunAsync(Source);
    }

    /// <summary>Runs an analyzer-only verification with the Microsoft.Extensions.AI stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static Task VerifyExtensionsAsync(string source) => RunAsync(source + ExtensionsAiStubs);

    /// <summary>Runs an analyzer-only verification with the Semantic Kernel stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static Task VerifySemanticKernelAsync(string source) => RunAsync(source + SemanticKernelStubs);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The complete source to analyse.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source)
    {
        var test = new AnalyzeSystemPrompt.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
