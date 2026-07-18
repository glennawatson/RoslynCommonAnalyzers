// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUncreatableClass = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2451UncreatableClassAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2451 (a class only its own constructors can create that never creates itself).</summary>
public class UncreatableClassAnalyzerUnitTest
{
    /// <summary>Verifies a private-constructor class whose members never create an instance is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UninstantiatedSelfOnlyClassIsReportedAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class {|SST2451:C|}
            {
                private C()
                {
                }

                public static int Track() => 0;
            }
            """);

    /// <summary>Verifies a private-constructor class holding only instance members is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceOnlyMembersClassIsReportedAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class {|SST2451:C|}
            {
                private readonly int _value;

                private C(int value) => _value = value;

                public int Value => _value;
            }
            """);

    /// <summary>Verifies a constructor with no access modifier defaults to private and is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModifierlessConstructorIsReportedAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class {|SST2451:C|}
            {
                C()
                {
                }

                public static int Track() => 0;
            }
            """);

    /// <summary>Verifies creating an unrelated type that shares the class's name does not count as self-creation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNameOtherTypeCreationIsStillReportedAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            namespace Other
            {
                public sealed class C
                {
                }
            }

            public sealed class {|SST2451:C|}
            {
                private C()
                {
                }

                public static object Make() => new Other.C();
            }
            """);

    /// <summary>Verifies a sealed class whose private-protected constructor no one can chain is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SealedPrivateProtectedClassIsReportedAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class {|SST2451:C|}
            {
                private protected C()
                {
                }

                public static int Track() => 0;
            }
            """);

    /// <summary>Verifies a singleton exposed through a static field initializer is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticFieldSingletonIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public static readonly C Instance = new C();

                private C()
                {
                }
            }
            """);

    /// <summary>Verifies a singleton built by a target-typed new in a static property initializer is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticPropertySingletonWithImplicitNewIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private C()
                {
                }

                public static C Instance { get; } = new();
            }
            """);

    /// <summary>Verifies a static factory method discharges the type's creation duty.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticFactoryMethodIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private C()
                {
                }

                public static C Create() => new C();
            }
            """);

    /// <summary>Verifies a lazily-built singleton whose creation hides inside a Lazy initializer is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LazyInitializerIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private static readonly Lazy<C> Cached = new Lazy<C>(() => new C());

                private C()
                {
                }

                public static C Instance => Cached.Value;
            }
            """);

    /// <summary>Verifies a class anyone can construct is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicConstructorIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public C()
                {
                }
            }
            """);

    /// <summary>Verifies an internal constructor leaves creation open to the assembly and is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalConstructorIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                internal C()
                {
                }
            }
            """);

    /// <summary>Verifies an open class with a private-protected constructor is clean: a derived class elsewhere in the assembly can chain it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateProtectedConstructorOnOpenClassIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public class C
            {
                private protected C()
                {
                }
            }
            """);

    /// <summary>Verifies a static class is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticClassIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public static class C
            {
                public static int Track() => 0;
            }
            """);

    /// <summary>Verifies an abstract class with private constructors is clean: nested subclasses are its instances.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractClassIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public abstract class C
            {
                private C()
                {
                }

                public sealed class Known : C
                {
                }
            }
            """);

    /// <summary>Verifies a struct is never analyzed: the default value always exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public struct S
            {
                private S(int value) => Value = value;

                public int Value { get; }
            }
            """);

    /// <summary>Verifies creation from a nested type's members counts as the type creating itself.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedFactoryTypeIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private C()
                {
                }

                public static class Factory
                {
                    public static C Make() => new C();
                }
            }
            """);

    /// <summary>Verifies a nested derived class keeps the type alive by chaining its private constructor.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedDerivedTypeIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public class C
            {
                private C()
                {
                }

                public sealed class Derived : C
                {
                }
            }
            """);

    /// <summary>Verifies a partial class is not analyzed: another part may hold the creation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialClassIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed partial class C
            {
                private C()
                {
                }
            }
            """);

    /// <summary>Verifies a serialization-shaped constructor marks the type as created by a deserializer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SerializationConstructorIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            using System.Runtime.Serialization;

            public sealed class C
            {
                private C(SerializationInfo info, StreamingContext context) => Value = info.GetInt32("value");

                public int Value { get; }
            }
            """);

    /// <summary>Verifies a constructor designated for a deserializer by attribute marks the type as externally created.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DesignatedDeserializationConstructorIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            using System;

            namespace Serialization
            {
                [AttributeUsage(AttributeTargets.Constructor)]
                public sealed class JsonConstructorAttribute : Attribute
                {
                }
            }

            public sealed class C
            {
                [Serialization.JsonConstructor]
                private C()
                {
                }

                public int Value { get; }
            }
            """);

    /// <summary>Verifies a generic class creating a constructed instance of itself is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericSelfCreationIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class C<T>
            {
                private C()
                {
                }

                public static C<T> Create() => new C<T>();
            }
            """);

    /// <summary>Verifies a class with no declared constructor keeps its implicit public one and is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoDeclaredConstructorIsCleanAsync()
        => await VerifyUncreatableClass.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Value { get; set; }
            }
            """);
}
