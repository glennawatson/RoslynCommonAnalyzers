// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyThrow = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1485UnexpectedThrowAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1485 (members that must not throw should not throw).</summary>
public class UnexpectedThrowAnalyzerUnitTest
{
    /// <summary>Verifies the equality, hashing, formatting and disposal members are all measured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ImplicitlyInvokedMembersAreReportedAsync()
        => await VerifyThrow.VerifyAnalyzerAsync(
            """
            public class Values
            {
                private readonly int _value;

                public Values(int value) => _value = value;

                public override bool Equals(object obj) => {|SST1485:throw|} new System.InvalidOperationException();

                public override int GetHashCode() => {|SST1485:throw|} new System.InvalidOperationException();

                public override string ToString() => {|SST1485:throw|} new System.InvalidOperationException();

                public void Dispose() => {|SST1485:throw|} new System.InvalidOperationException();

                public System.Threading.Tasks.Task DisposeAsync() => {|SST1485:throw|} new System.InvalidOperationException();

                public int Read() => _value;
            }
            """);

    /// <summary>Verifies a typed <c>Equals</c> overload is measured like the object one.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TypedEqualsIsReportedAsync()
        => await VerifyThrow.VerifyAnalyzerAsync(
            """
            public class Values : System.IEquatable<Values>
            {
                public bool Equals(Values other)
                {
                    {|SST1485:throw|} new System.InvalidOperationException();
                }

                public override bool Equals(object obj) => Equals(obj as Values);

                public override int GetHashCode() => 0;
            }
            """);

    /// <summary>Verifies a static constructor, a finalizer and an implicit conversion are measured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>An explicit conversion is written by the caller, so its failure is visible and it is not measured.</remarks>
    [Test]
    public async Task RuntimeInvokedMembersAreReportedAsync()
        => await VerifyThrow.VerifyAnalyzerAsync(
            """
            public class Config
            {
                static Config()
                {
                    {|SST1485:throw|} new System.InvalidOperationException();
                }

                public Config()
                {
                    throw new System.InvalidOperationException();
                }
            }

            public class Handle
            {
                ~Handle()
                {
                    {|SST1485:throw|} new System.InvalidOperationException();
                }
            }

            public struct Money
            {
                public static implicit operator Money(int value) => {|SST1485:throw|} new System.InvalidOperationException();

                public static explicit operator int(Money value) => throw new System.InvalidOperationException();
            }
            """);

    /// <summary>Verifies the equality and ordering operators are measured, and an arithmetic operator is not.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ComparisonOperatorsAreReportedAsync()
        => await VerifyThrow.VerifyAnalyzerAsync(
            """
            public struct Amount
            {
                public static bool operator ==(Amount left, Amount right) => {|SST1485:throw|} new System.InvalidOperationException();

                public static bool operator !=(Amount left, Amount right) => {|SST1485:throw|} new System.InvalidOperationException();

                public static bool operator <(Amount left, Amount right) => {|SST1485:throw|} new System.InvalidOperationException();

                public static bool operator >(Amount left, Amount right) => {|SST1485:throw|} new System.InvalidOperationException();

                public static Amount operator +(Amount left, Amount right) => throw new System.InvalidOperationException();

                public override bool Equals(object obj) => false;

                public override int GetHashCode() => 0;
            }
            """);

    /// <summary>Verifies the two exceptions that mark a member as deliberately absent are allowed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>A type that derives from one of them says the same thing, and is recognized through the bind.</remarks>
    [Test]
    public async Task DeliberateAbsenceIsCleanAsync()
        => await VerifyThrow.VerifyAnalyzerAsync(
            """
            public class Placeholder
            {
                public override bool Equals(object obj) => throw new System.NotImplementedException();

                public override int GetHashCode() => throw new System.NotSupportedException();

                public override string ToString() => throw new PlatformAbsent();

                public void Dispose() => throw new System.NotImplementedException();
            }

            public class PlatformAbsent : System.NotSupportedException
            {
            }
            """);

    /// <summary>Verifies a throw inside a nested lambda or local function is not attributed to the member.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The delegate runs when it is invoked, which need not be during the member that declares it.</remarks>
    [Test]
    public async Task NestedFunctionsAreCleanAsync()
        => await VerifyThrow.VerifyAnalyzerAsync(
            """
            public class Deferred
            {
                public override string ToString()
                {
                    string Fail() => throw new System.InvalidOperationException();

                    return Describe(Fail);
                }

                public void Dispose()
                {
                    System.Action fail = () => throw new System.InvalidOperationException();

                    Register(fail);
                }

                private static string Describe(System.Func<string> factory) => factory is null ? "none" : "some";

                private static void Register(System.Action action)
                {
                }
            }
            """);

    /// <summary>Verifies a rethrow propagates rather than originates, and a new exception in the same catch does not.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RethrowIsCleanButANewExceptionIsNotAsync()
        => await VerifyThrow.VerifyAnalyzerAsync(
            """
            public class Propagating
            {
                public void Dispose()
                {
                    try
                    {
                        Release();
                    }
                    catch (System.InvalidOperationException)
                    {
                        throw;
                    }
                }

                private static void Release()
                {
                }
            }

            public class Replacing
            {
                public void Dispose()
                {
                    try
                    {
                        Release();
                    }
                    catch (System.InvalidOperationException error)
                    {
                        {|SST1485:throw|} new System.AggregateException(error);
                    }
                }

                private static void Release()
                {
                }
            }
            """);

    /// <summary>Verifies an ordinary member may throw, and that arity is part of a measured member's identity.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OrdinaryMembersAreCleanAsync()
        => await VerifyThrow.VerifyAnalyzerAsync(
            """
            public class Resource
            {
                public int Divide(int value, int divisor)
                {
                    if (divisor == 0)
                    {
                        throw new System.ArgumentOutOfRangeException(nameof(divisor));
                    }

                    return value / divisor;
                }

                protected virtual void Dispose(bool disposing)
                {
                    throw new System.InvalidOperationException();
                }

                public string ToString(string format)
                {
                    throw new System.InvalidOperationException();
                }
            }
            """);

    /// <summary>Verifies an abstract or interface member with no body is not measured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MembersWithoutBodiesAreCleanAsync()
        => await VerifyThrow.VerifyAnalyzerAsync(
            """
            public interface IResource
            {
                void Dispose();
            }

            public abstract class Resource
            {
                public abstract override string ToString();
            }
            """);

    /// <summary>Verifies a member named by the configuration is measured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AdditionalMembersAreMeasuredAsync()
    {
        var test = new VerifyThrow.Test
        {
            TestCode = """
                       public class Handler
                       {
                           public void OnError()
                           {
                               {|SST1485:throw|} new System.InvalidOperationException();
                           }

                           public void OnCompleted()
                           {
                               {|SST1485:throw|} new System.InvalidOperationException();
                           }

                           public void OnNext()
                           {
                               throw new System.InvalidOperationException();
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1485.additional_members = OnError, OnCompleted

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide key applies when no rule-specific key is set.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GeneralAdditionalMembersKeyAppliesAsync()
    {
        var test = new VerifyThrow.Test
        {
            TestCode = """
                       public class Handler
                       {
                           public void OnError()
                           {
                               {|SST1485:throw|} new System.InvalidOperationException();
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.additional_members = OnError

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a throw that is not written as an object creation is still bound and reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The syntactic allow-list is only a shortcut; a factory call has to be bound to be judged.</remarks>
    [Test]
    public async Task ThrownExpressionIsBoundWhenItIsNotAnObjectCreationAsync()
        => await VerifyThrow.VerifyAnalyzerAsync(
            """
            public class Factories
            {
                public override string ToString() => {|SST1485:throw|} Create();

                public override int GetHashCode() => throw Absent();

                public override bool Equals(object obj) => false;

                private static System.Exception Create() => new System.InvalidOperationException();

                private static System.NotSupportedException Absent() => new System.NotSupportedException();
            }
            """);
}
