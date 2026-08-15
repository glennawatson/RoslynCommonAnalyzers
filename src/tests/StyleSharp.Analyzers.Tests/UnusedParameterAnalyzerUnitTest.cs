// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUnusedParameter = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1461UnusedParameterAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1461UnusedParameterAnalyzer"/>.</summary>
public class UnusedParameterAnalyzerUnitTest
{
    /// <summary>Verifies an unused private method parameter is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PrivateMethodParameterIsReportedAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int M(int {|SST1461:value|}) => 1;
            }
            """);

    /// <summary>Verifies public method parameters are not reported because they are API surface.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicMethodParameterIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int value) => 1;
            }
            """);

    /// <summary>Verifies an (object, EventArgs) event handler is exempt even when both parameters are unread.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EventHandlerSignatureIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private void OnEvent(object sender, EventArgs e)
                {
                }
            }
            """);

    /// <summary>Verifies a PropertyChanged handler with a nullable sender and a derived EventArgs is exempt.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PropertyChangedHandlerIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            #nullable enable
            using System.ComponentModel;

            public sealed class C
            {
                private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
                    => System.Console.WriteLine(e.PropertyName);
            }
            """);

    /// <summary>Verifies the exemption is narrow: a two-parameter method whose second parameter is not EventArgs still reports.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ObjectFirstParameterWithNonEventArgsSecondIsReportedAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private void M(object {|SST1461:sender|}, string name)
                    => System.Console.WriteLine(name);
            }
            """);

    /// <summary>Verifies the exemption is narrow: an EventArgs second parameter with a non-object first parameter still reports.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonObjectFirstParameterIsReportedAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private void M(int {|SST1461:code|}, EventArgs e)
                    => System.Console.WriteLine(e);
            }
            """);

    /// <summary>Verifies an internal member's unread parameter is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InternalMethodParameterIsReportedAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            internal class C
            {
                internal int Compute(int used, int {|SST1461:unused|}) => used;
            }
            """);

    /// <summary>Verifies a public member on a non-externally-visible type is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicMemberOfInternalTypeIsReportedAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int Compute(int used, int {|SST1461:unused|}) => used;
            }
            """);

    /// <summary>Verifies an externally visible member is reported once the option asks for it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicMemberIsReportedWhenConfiguredAsync()
    {
        var test = new VerifyUnusedParameter.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Compute(int used, int {|SST1461:unused|}) => used;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add((
            "/.editorconfig",
            "root = true\n\n[*.cs]\nstylesharp.SST1461.unread_parameter_include_public_api = true\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide option key is honoured when the rule-specific one is unset.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicMemberIsReportedWhenConfiguredProjectWideAsync()
    {
        var test = new VerifyUnusedParameter.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Compute(int used, int {|SST1461:unused|}) => used;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add((
            "/.editorconfig",
            "root = true\n\n[*.cs]\nstylesharp.unread_parameter_include_public_api = true\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a constructor's unread parameter is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstructorParameterIsReportedAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private readonly int _value;

                public C(int value, int {|SST1461:unused|}) => _value = value;
            }
            """);

    /// <summary>Verifies a parameter passed to a constructor initializer counts as read.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ParameterUsedInConstructorInitializerIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
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

    /// <summary>Verifies a local function's unread parameter is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LocalFunctionParameterIsReportedAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int Run()
                {
                    return Compute(1, 2);

                    static int Compute(int used, int {|SST1461:unused|}) => used;
                }
            }
            """);

    /// <summary>Verifies a parameter read only inside a lambda counts as read.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ParameterReadInsideALambdaIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                private Func<int> Capture(int value) => () => value;
            }
            """);

    /// <summary>Verifies a parameter named only by nameof counts as read.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ParameterNamedByNameofIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private string Describe(int value) => nameof(value);
            }
            """);

    /// <summary>Verifies an interface implementation, whose signature the interface fixes, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InterfaceImplementationIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
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
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitInterfaceImplementationIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
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

    /// <summary>Verifies an override, whose signature the base fixes, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OverrideIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
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

    /// <summary>Verifies a partial method, whose other part may read the parameter, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PartialMethodIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
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
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThrowOnlyBodyIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
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

    /// <summary>Verifies a serialization callback, whose delegate fixes the signature, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SerializationCallbackShapeIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            using System.Runtime.Serialization;

            internal class C
            {
                private void OnDeserialized(StreamingContext context)
                {
                }
            }
            """);

    /// <summary>Verifies a method handed on as a method group, which must keep the delegate's shape, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MethodGroupUseIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                private Func<int, int, int> Get() => Compute;

                private int Compute(int used, int other) => used;
            }
            """);

    /// <summary>Verifies an attribute constructor, whose parameters every usage site depends on, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AttributeConstructorIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            internal sealed class MarkerAttribute : Attribute
            {
                public MarkerAttribute(string name, int order) => Name = name;

                public string Name { get; }
            }
            """);

    /// <summary>Verifies an extension method's unread receiver is left to SST1708, which asks for a different edit.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExtensionReceiverIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            internal static class Extensions
            {
                internal static int Compute(this string text, int used) => used;
            }
            """);

    /// <summary>Verifies a discard-named parameter, which already says it is unused, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DiscardNamedParameterIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int Compute(int used, int _) => used;
            }
            """);
}
