// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2327SelfTypeCheckAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2327 (a type inspecting its own runtime type against a specific named type).</summary>
public class SelfTypeCheckAnalyzerUnitTest
{
    /// <summary>Verifies <c>this is Derived</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisIsDerivedTypeIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool Check() => {|SST2327:this is Dog|};
            }

            public class Dog : Animal
            {
            }
            """);

    /// <summary>Verifies <c>this is Derived name</c> (a declaration pattern) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisIsDerivedDeclarationPatternIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool Check() => {|SST2327:this is Dog dog|} && dog is not null;
            }

            public class Dog : Animal
            {
            }
            """);

    /// <summary>Verifies <c>this as Derived</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisAsDerivedTypeIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool NotDog()
                {
                    var dog = {|SST2327:this as Dog|};
                    return dog is null;
                }
            }

            public class Dog : Animal
            {
            }
            """);

    /// <summary>Verifies <c>this.GetType() == typeof(Derived)</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisGetTypeEqualsTypeOfIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool IsExactlyDog() => {|SST2327:this.GetType() == typeof(Dog)|};
            }

            public class Dog : Animal
            {
            }
            """);

    /// <summary>Verifies <c>typeof(Derived) == this.GetType()</c> (operands reversed) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeOfEqualsThisGetTypeReversedIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool IsExactlyDog() => {|SST2327:typeof(Dog) == this.GetType()|};
            }

            public class Dog : Animal
            {
            }
            """);

    /// <summary>Verifies <c>this.GetType() != typeof(Derived)</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisGetTypeNotEqualsTypeOfIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool IsNotDog() => {|SST2327:this.GetType() != typeof(Dog)|};
            }

            public class Dog : Animal
            {
            }
            """);

    /// <summary>Verifies a test of some other value (<c>other is Derived</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherValueIsDerivedIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool Check(object other) => other is Dog;
            }

            public class Dog : Animal
            {
            }
            """);

    /// <summary>Verifies a declaration pattern on some other value (<c>other is Derived name</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherValueDeclarationPatternIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool Check(object other) => other is Dog dog && dog is not null;
            }

            public class Dog : Animal
            {
            }
            """);

    /// <summary>Verifies an interface capability check (<c>this is IThing</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisIsInterfaceIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public interface IThing
            {
            }

            public class Animal
            {
                public bool Check() => this is IThing;
            }

            public class Dog : Animal, IThing
            {
            }
            """);

    /// <summary>Verifies a test against a type parameter (<c>this is T</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisIsTypeParameterIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Box<T>
            {
                public bool Check() => this is T;
            }
            """);

    /// <summary>Verifies a negated pattern on <c>this</c> (<c>this is not Derived</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisIsNotDerivedNegatedPatternIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool Check() => this is not Dog;
            }

            public class Dog : Animal
            {
            }
            """);

    /// <summary>Verifies a property pattern on <c>this</c> (<c>this is { }</c>), which tests no named type, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisIsPropertyPatternIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool Check() => this is { };
            }
            """);

    /// <summary>Verifies a <c>GetType()</c> comparison whose receiver is not <c>this</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherGetTypeComparisonIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                private readonly object _other = new object();

                public bool Check() => _other.GetType() == typeof(Dog);
            }

            public class Dog : Animal
            {
            }
            """);

    /// <summary>Verifies a bare <c>GetType()</c> comparison without an explicit <c>this</c> receiver is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareGetTypeComparisonIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool Check() => GetType() == typeof(Dog);
            }

            public class Dog : Animal
            {
            }
            """);

    /// <summary>Verifies an unrelated equality (neither side a <c>typeof</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedEqualityIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
                public bool Check(int a, int b) => a == b;
            }
            """);

    /// <summary>Verifies a type that dispatches through a virtual member instead of inspecting its own type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VirtualDispatchIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class Animal
            {
                public abstract string Speak();
            }

            public class Dog : Animal
            {
                public override string Speak() => "woof";
            }
            """);
}
