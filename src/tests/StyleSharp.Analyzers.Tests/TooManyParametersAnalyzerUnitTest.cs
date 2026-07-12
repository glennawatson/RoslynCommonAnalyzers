// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyParameters = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1472TooManyParametersAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1472 (signatures should not declare too many parameters).</summary>
public class TooManyParametersAnalyzerUnitTest
{
    /// <summary>The <c>init</c>-accessor polyfill positional records require on the test reference assemblies.</summary>
    private const string IsExternalInit = """

        namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
        """;

    /// <summary>Verifies a method over the default maximum is reported and one at the maximum is not.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MethodOverTheMaximumIsReportedAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void {|SST1472:Wide|}(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }

                public void AtLimit(int a, int b, int c, int d, int e, int f, int g)
                {
                }
            }
            """);

    /// <summary>Verifies a constructor, a delegate, a local function and an indexer are all measured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EverySignatureKindIsMeasuredAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            public delegate void {|SST1472:Handler|}(int a, int b, int c, int d, int e, int f, int g, int h);

            public class C
            {
                public {|SST1472:C|}(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }

                public int {|SST1472:this|}[int a, int b, int c, int d, int e, int f, int g, int h] => a;

                public void Outer()
                {
                    void {|SST1472:Inner|}(int a, int b, int c, int d, int e, int f, int g, int h)
                    {
                    }

                    Inner(1, 2, 3, 4, 5, 6, 7, 8);
                }
            }
            """);

    /// <summary>Verifies a class primary constructor is measured like any other constructor.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ClassPrimaryConstructorIsMeasuredAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            public class {|SST1472:Service|}(int a, int b, int c, int d, int e, int f, int g, int h);

            public struct {|SST1472:Point|}(int a, int b, int c, int d, int e, int f, int g, int h);
            """);

    /// <summary>Verifies a positional record is the parameter object the rule asks for, so it is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PositionalRecordIsCleanByDefaultAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            $$"""
            public record Person(int A, int B, int C, int D, int E, int F, int G, int H, int I, int J);

            public record struct Point(int A, int B, int C, int D, int E, int F, int G, int H);{{IsExternalInit}}
            """);

    /// <summary>Verifies a positional record is measured once it is opted back in.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PositionalRecordIsReportedWhenOptedInAsync()
    {
        var test = new VerifyParameters.Test
        {
            TestCode = $$"""
                       public record {|SST1472:Person|}(int A, int B, int C, int D, int E, int F, int G, int H);

                       public record Small(int A, int B);{{IsExternalInit}}
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1472.check_positional_records = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a signature whose shape a base type or an interface dictates is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The declaration that owns the shape is reported instead, which is where the fix belongs.</remarks>
    [Test]
    public async Task InheritedSignaturesAreReportedAtTheirSourceAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            public interface IProcessor
            {
                void {|SST1472:Run|}(int a, int b, int c, int d, int e, int f, int g, int h);
            }

            public abstract class Base
            {
                public abstract void {|SST1472:Execute|}(int a, int b, int c, int d, int e, int f, int g, int h);
            }

            public class Processor : Base, IProcessor
            {
                public void Run(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }

                public override void Execute(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }
            }
            """);

    /// <summary>Verifies an explicit interface implementation is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitInterfaceImplementationIsCleanAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            public interface IProcessor
            {
                void {|SST1472:Run|}(int a, int b, int c, int d, int e, int f, int g, int h);
            }

            public class Processor : IProcessor
            {
                void IProcessor.Run(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }
            }
            """);

    /// <summary>Verifies a P/Invoke keeps the signature the native API dictates.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NativeImportIsCleanAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            using System.Runtime.InteropServices;

            public sealed class LibraryImportAttribute : System.Attribute
            {
                public LibraryImportAttribute(string name) => Name = name;

                public string Name { get; }
            }

            public partial class Native
            {
                [DllImport("user32.dll")]
                public static extern int MessageBox(int a, int b, int c, int d, int e, int f, int g, int h);

                [LibraryImport("native")]
                public static partial int Send(int a, int b, int c, int d, int e, int f, int g, int h);

                public static partial int Send(int a, int b, int c, int d, int e, int f, int g, int h) => 0;
            }
            """);

    /// <summary>Verifies only the defining half of a partial method is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PartialMethodIsReportedOnceAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                public partial void {|SST1472:Run|}(int a, int b, int c, int d, int e, int f, int g, int h);

                public partial void Run(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }
            }
            """);

    /// <summary>Verifies a deconstructor's parameters mirror the type's state and are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DeconstructorIsCleanAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Deconstruct(out int a, out int b, out int c, out int d, out int e, out int f, out int g, out int h)
                {
                    a = b = c = d = e = f = g = h = 0;
                }
            }
            """);

    /// <summary>Verifies a lambda takes its shape from its delegate and is never measured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LambdaIsCleanAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M()
                {
                    System.Action<int, int, int, int, int, int, int, int> run =
                        (a, b, c, d, e, f, g, h) => System.Console.WriteLine(a + h);
                    run(1, 2, 3, 4, 5, 6, 7, 8);
                }
            }
            """);

    /// <summary>Verifies an extension method's receiver is written as the receiver, not as an argument.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExtensionReceiverIsNotCountedAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            public static class Extensions
            {
                public static int Sum(this int[] values, int a, int b, int c, int d, int e, int f, int g) => a;

                public static int {|SST1472:Wide|}(this int[] values, int a, int b, int c, int d, int e, int f, int g, int h) => a;
            }
            """);

    /// <summary>Verifies an extension block's receiver is not counted against its members.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExtensionBlockReceiverIsNotCountedAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            public static class Extensions
            {
                extension(int[] values)
                {
                    public int Sum(int a, int b, int c, int d, int e, int f, int g) => a;

                    public int {|SST1472:Wide|}(int a, int b, int c, int d, int e, int f, int g, int h) => a;
                }
            }
            """);

    /// <summary>Verifies the compiler-supplied caller-info parameters do not count against the maximum.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CallerInfoParametersAreNotCountedAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public void Log(
                    int a,
                    int b,
                    int c,
                    int d,
                    int e,
                    int f,
                    int g,
                    [CallerMemberName] string member = "",
                    [CallerFilePath] string file = "",
                    [CallerLineNumber] int line = 0)
                {
                }
            }
            """);

    /// <summary>Verifies optional parameters count by default.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OptionalParametersCountByDefaultAsync()
        => await VerifyParameters.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void {|SST1472:Configure|}(int a, int b, int c, int d, int e, int f, int g, int h = 0)
                {
                }
            }
            """);

    /// <summary>Verifies optional parameters can be excluded from the count.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OptionalParametersCanBeExcludedAsync()
    {
        var test = new VerifyParameters.Test
        {
            TestCode = """
                       public class C
                       {
                           public void Configure(int a, int b, int c, int d, int e, int f, int g, int h = 0, int i = 0)
                           {
                           }

                           public void {|SST1472:Required|}(int a, int b, int c, int d, int e, int f, int g, int h)
                           {
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1472.count_optional_parameters = false

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule-specific maximum overrides the project-wide one.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RuleSpecificMaximumWinsOverGeneralAsync()
    {
        var test = new VerifyParameters.Test
        {
            TestCode = """
                       public class C
                       {
                           public void {|SST1472:Four|}(int a, int b, int c, int d)
                           {
                           }

                           public void Three(int a, int b, int c)
                           {
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.max_parameters = 20
            stylesharp.SST1472.max_parameters = 3

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide maximum applies when no rule-specific key is set.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GeneralMaximumAppliesAsync()
    {
        var test = new VerifyParameters.Test
        {
            TestCode = """
                       public class C
                       {
                           public void {|SST1472:Three|}(int a, int b, int c)
                           {
                           }

                           public void Two(int a, int b)
                           {
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.max_parameters = 2

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unparsable maximum falls back to the default rather than reporting everything.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnparsableMaximumFallsBackToTheDefaultAsync()
    {
        var test = new VerifyParameters.Test
        {
            TestCode = """
                       public class C
                       {
                           public void AtLimit(int a, int b, int c, int d, int e, int f, int g)
                           {
                           }

                           public void {|SST1472:Wide|}(int a, int b, int c, int d, int e, int f, int g, int h)
                           {
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1472.max_parameters = lots

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
