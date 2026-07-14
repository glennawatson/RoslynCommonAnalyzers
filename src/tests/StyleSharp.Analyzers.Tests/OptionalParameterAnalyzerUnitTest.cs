// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyOptionalParameter = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2309OptionalParameterAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2309 (use an overload instead of an optional parameter).</summary>
public class OptionalParameterAnalyzerUnitTest
{
    /// <summary>Verifies an optional parameter on a visible method and constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalParameterOnAVisibleMemberIsReportedAsync()
        => await VerifyOptionalParameter.VerifyAnalyzerAsync(
            """
            public class Client
            {
                public Client(int {|SST2309:retries|} = 3)
                {
                }

                public void Send(string request, int {|SST2309:timeout|} = 30)
                {
                }

                protected void Retry(int {|SST2309:attempts|} = 1)
                {
                }
            }
            """);

    /// <summary>Verifies an optional parameter on an extension method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalParameterOnAnExtensionMethodIsReportedAsync()
        => await VerifyOptionalParameter.VerifyAnalyzerAsync(
            """
            public static class Extensions
            {
                public static void Truncate(this string value, int {|SST2309:length|} = 10)
                {
                }
            }
            """);

    /// <summary>Verifies a caller-info parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// These attributes only work on an optional parameter — the default is the slot the compiler writes the
    /// caller's details into — so an overload is not something the language would accept here.
    /// </remarks>
    [Test]
    public async Task CallerInfoParameterIsNotReportedAsync()
        => await VerifyOptionalParameter.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class Logger
            {
                public void Log(
                    string message,
                    [CallerMemberName] string caller = "",
                    [CallerFilePath] string path = "",
                    [CallerLineNumber] int line = 0)
                {
                }
            }
            """);

    /// <summary>Verifies a member whose signature belongs to a base or an interface is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The declarations that set the shape carry the diagnostic; the implementations cannot change it.</remarks>
    [Test]
    public async Task MemberThatCannotChangeItsSignatureIsNotReportedAsync()
        => await VerifyOptionalParameter.VerifyAnalyzerAsync(
            """
            public interface ISender
            {
                void Send(string request, int {|SST2309:timeout|} = 30);
            }

            public abstract class SenderBase
            {
                public abstract void Retry(int {|SST2309:attempts|} = 1);
            }

            public sealed class Sender : SenderBase, ISender
            {
                public override void Retry(int attempts = 1)
                {
                }

                public void Send(string request, int timeout = 30)
                {
                }
            }

            public sealed class ExplicitSender : ISender
            {
                void ISender.Send(string request, int timeout)
                {
                }
            }
            """);

    /// <summary>Verifies a positional record's primary constructor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Its parameter list is the record's definition, and there is no overload to write it as.</remarks>
    [Test]
    public async Task PositionalRecordPrimaryConstructorIsNotReportedAsync()
        => await VerifyOptionalParameter.VerifyAnalyzerAsync(
            """
            namespace System.Runtime.CompilerServices
            {
                internal static class IsExternalInit
                {
                }
            }

            public record Options(string Endpoint, int Retries = 3);
            """);

    /// <summary>Verifies a params array and a member that is not externally visible are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A params array carries no default to bake in, and inside an assembly every caller recompiles together,
    /// so an internal or private default can never go stale.
    /// </remarks>
    [Test]
    public async Task ParamsAndNonVisibleMembersAreNotReportedAsync()
        => await VerifyOptionalParameter.VerifyAnalyzerAsync(
            """
            public class Batch
            {
                public void Send(params int[] items)
                {
                }

                private void Retry(int attempts = 1)
                {
                }

                internal void Flush(int size = 10)
                {
                }
            }

            internal class Internal
            {
                public void Send(int timeout = 30)
                {
                }
            }
            """);
}
