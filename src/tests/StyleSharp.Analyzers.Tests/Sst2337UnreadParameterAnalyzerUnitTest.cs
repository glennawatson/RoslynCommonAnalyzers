// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUnread = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2337UnreadParameterAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2337 (a parameter the body never reads).</summary>
public class Sst2337UnreadParameterAnalyzerUnitTest
{
    /// <summary>Verifies a private method's unread parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnreadParameterIsReportedAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int Compute(int used, int {|SST2337:unused|}) => used;
            }
            """);

    /// <summary>Verifies a local function's unread parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFunctionParameterIsReportedAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int Run()
                {
                    return Compute(1, 2);

                    static int Compute(int used, int {|SST2337:unused|}) => used;
                }
            }
            """);

    /// <summary>Verifies a constructor's unread parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorParameterIsReportedAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private readonly int _value;

                public C(int value, int {|SST2337:unused|}) => _value = value;
            }
            """);

    /// <summary>Verifies a parameter read only inside a lambda counts as read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterReadInsideALambdaIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                private Func<int> Capture(int value) => () => value;
            }
            """);

    /// <summary>Verifies a parameter named only by nameof counts as read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterNamedByNameofIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private string Describe(int value) => nameof(value);
            }
            """);

    /// <summary>Verifies a parameter passed to a constructor initializer counts as read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterUsedInConstructorInitializerIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal class Base
            {
                protected Base(int value) => Value = value;

                protected int Value { get; }
            }

            internal class C : Base
            {
                public C(int value)
                    : base(value)
                {
                }
            }
            """);

    /// <summary>Verifies an externally visible member is not reported by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMemberIsCleanByDefaultAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Compute(int used, int unused) => used;
            }
            """);

    /// <summary>Verifies an externally visible member is reported once the option asks for it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMemberIsReportedWhenConfiguredAsync()
    {
        var test = new VerifyUnread.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Compute(int used, int {|SST2337:unused|}) => used;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add((
            "/.editorconfig",
            "root = true\n\n[*.cs]\nstylesharp.SST2337.unread_parameter_include_public_api = true\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an override, whose signature the base fixes, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverrideIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal abstract class Base
            {
                public abstract int Compute(int used, int other);
            }

            internal class C : Base
            {
                public override int Compute(int used, int other) => used;
            }
            """);

    /// <summary>Verifies an interface implementation, whose signature the interface fixes, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceImplementationIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal interface ICompute
            {
                int Compute(int used, int other);
            }

            internal class C : ICompute
            {
                public int Compute(int used, int other) => used;
            }
            """);

    /// <summary>Verifies an explicit interface implementation is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitInterfaceImplementationIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal interface ICompute
            {
                int Compute(int used, int other);
            }

            internal class C : ICompute
            {
                int ICompute.Compute(int used, int other) => used;
            }
            """);

    /// <summary>Verifies a partial method, whose other part may read the parameter, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialMethodIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal partial class C
            {
                private partial int Compute(int used, int other);
            }

            internal partial class C
            {
                private partial int Compute(int used, int other) => used;
            }
            """);

    /// <summary>Verifies a stub whose body only throws is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowOnlyBodyIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                private int Compute(int used, int other) => throw new NotImplementedException();

                private int Compute2(int used, int other)
                {
                    throw new NotImplementedException();
                }
            }
            """);

    /// <summary>Verifies an event-handler shape, whose delegate fixes the signature, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EventHandlerShapeIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                private void OnChanged(object sender, EventArgs e) => Console.WriteLine("changed");
            }
            """);

    /// <summary>Verifies a method handed on as a method group, which must keep the delegate's shape, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodGroupUseIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                private Func<int, int, int> Get() => Compute;

                private int Compute(int used, int other) => used;
            }
            """);

    /// <summary>Verifies a discard-named parameter, which already says it is unused, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardNamedParameterIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int Compute(int used, int _) => used;
            }
            """);

    /// <summary>Verifies an extension method's unread receiver is left to SST1708, which asks for a different edit.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExtensionReceiverIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal static class Extensions
            {
                internal static int Compute(this string text, int used) => used;
            }
            """);

    /// <summary>Verifies an extension block's receiver, read only by the members it wraps, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExtensionBlockReceiverIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            internal static class Extensions
            {
                extension(string text)
                {
                    internal int Length2 => text.Length;
                }
            }
            """);

    /// <summary>Verifies an attribute constructor, whose parameters every usage site depends on, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AttributeConstructorIsCleanAsync()
        => await VerifyUnread.VerifyAnalyzerAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            internal sealed class MarkerAttribute : Attribute
            {
                public MarkerAttribute(string name, int order) => Name = name;

                public string Name { get; }
            }
            """);
}
