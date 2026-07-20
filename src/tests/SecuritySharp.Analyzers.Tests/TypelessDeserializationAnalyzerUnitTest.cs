// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeTypeless = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1405TypelessDeserializationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1405 (a MessagePack typeless deserializer must not reconstruct arbitrary types from untrusted input).</summary>
public class TypelessDeserializationAnalyzerUnitTest
{
    /// <summary>An inline stub of the MessagePack typeless surface, keyed by the same metadata names the rule probes.</summary>
    private const string MessagePackStub = """
        namespace MessagePack
        {
            public static class MessagePackSerializer
            {
                public static T Deserialize<T>(byte[] data) => default!;

                public static class Typeless
                {
                    public static object Deserialize(byte[] data) => null!;

                    public static System.Threading.Tasks.Task<object> DeserializeAsync(System.IO.Stream stream)
                        => System.Threading.Tasks.Task.FromResult<object>(null!);
                }
            }
        }

        namespace MessagePack.Resolvers
        {
            public sealed class TypelessObjectResolver
            {
                public static readonly TypelessObjectResolver Instance = new();
            }

            public sealed class TypelessContractlessStandardResolver
            {
                public static readonly TypelessContractlessStandardResolver Instance = new();
            }
        }
        """;

    /// <summary>Verifies a <c>MessagePackSerializer.Typeless.Deserialize</c> call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypelessFacadeDeserializeReportedAsync()
        => await VerifyAsync(
            """
            using MessagePack;

            public class C
            {
                public object M(byte[] data) => {|SES1405:MessagePackSerializer.Typeless.Deserialize(data)|};
            }
            """);

    /// <summary>Verifies a <c>MessagePackSerializer.Typeless.DeserializeAsync</c> call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypelessFacadeDeserializeAsyncReportedAsync()
        => await VerifyAsync(
            """
            using MessagePack;

            public class C
            {
                public System.Threading.Tasks.Task<object> M(System.IO.Stream stream)
                    => {|SES1405:MessagePackSerializer.Typeless.DeserializeAsync(stream)|};
            }
            """);

    /// <summary>Verifies a fully qualified <c>Typeless.Deserialize</c> call is reported without a using.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedTypelessFacadeReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public object M(byte[] data) => {|SES1405:MessagePack.MessagePackSerializer.Typeless.Deserialize(data)|};
            }
            """);

    /// <summary>Verifies a reference to <c>TypelessObjectResolver.Instance</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypelessObjectResolverReferenceReportedAsync()
        => await VerifyAsync(
            """
            using MessagePack.Resolvers;

            public class C
            {
                public object M() => {|SES1405:TypelessObjectResolver.Instance|};
            }
            """);

    /// <summary>Verifies a reference to <c>TypelessContractlessStandardResolver.Instance</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypelessContractlessResolverReferenceReportedAsync()
        => await VerifyAsync(
            """
            using MessagePack.Resolvers;

            public class C
            {
                public object M() => {|SES1405:TypelessContractlessStandardResolver.Instance|};
            }
            """);

    /// <summary>Verifies a typed (contract) <c>MessagePackSerializer.Deserialize&lt;T&gt;</c> call is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypedContractDeserializeIsCleanAsync()
        => await VerifyAsync(
            """
            using MessagePack;

            public class Payload
            {
            }

            public class C
            {
                public Payload M(byte[] data) => MessagePackSerializer.Deserialize<Payload>(data);
            }
            """);

    /// <summary>Verifies a lookalike non-MessagePack <c>Typeless.Deserialize</c> and resolver are not reported while the rule is active.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LookalikeTypesAreCleanAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                public static class MessagePackSerializer
                {
                    public static class Typeless
                    {
                        public static object Deserialize(byte[] data) => null!;
                    }
                }

                public sealed class TypelessObjectResolver
                {
                    public static readonly TypelessObjectResolver Instance = new();
                }
            }

            public class C
            {
                public object M(byte[] data) => App.MessagePackSerializer.Typeless.Deserialize(data);

                public object R() => App.TypelessObjectResolver.Instance;
            }
            """);

    /// <summary>Verifies an unrelated singleton <c>.Instance</c> reference is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedInstanceReferenceIsCleanAsync()
        => await VerifyAsync(
            """
            public sealed class Logger
            {
                public static readonly Logger Instance = new();
            }

            public class C
            {
                public object M() => Logger.Instance;
            }
            """);

    /// <summary>Verifies the rule stays silent when MessagePack is not referenced at all.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenMessagePackAbsentAsync()
    {
        const string Source = """
                              namespace Other
                              {
                                  public static class MessagePackSerializer
                                  {
                                      public static class Typeless
                                      {
                                          public static object Deserialize(byte[] data) => null!;
                                      }
                                  }

                                  public sealed class TypelessObjectResolver
                                  {
                                      public static readonly TypelessObjectResolver Instance = new();
                                  }
                              }

                              public class C
                              {
                                  public object M(byte[] data) => Other.MessagePackSerializer.Typeless.Deserialize(data);

                                  public object R() => Other.TypelessObjectResolver.Instance;
                              }
                              """;

        var test = new AnalyzeTypeless.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against .NET 9 with the MessagePack typeless surface stubbed in.</summary>
    /// <param name="source">The consumer source with diagnostic markup; the MessagePack stub is appended automatically.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeTypeless.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + "\n\n" + MessagePackStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
