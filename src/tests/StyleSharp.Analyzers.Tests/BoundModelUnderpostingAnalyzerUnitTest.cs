// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using VerifyUnderposting = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2705BoundModelUnderpostingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2705 (a bound model member should be required or nullable). The rule is opt-in.</summary>
public class BoundModelUnderpostingAnalyzerUnitTest
{
    /// <summary>The inline stubs of the ASP.NET Core MVC and validation surface the rule gates on.</summary>
    private const string BindingStubs = """

        namespace System.ComponentModel.DataAnnotations
        {
            public sealed class RequiredAttribute : System.Attribute { }
        }

        namespace Microsoft.AspNetCore.Mvc.ModelBinding
        {
            public sealed class BindRequiredAttribute : System.Attribute { }
        }

        namespace Microsoft.AspNetCore.Mvc
        {
            public sealed class ApiControllerAttribute : System.Attribute { }

            public sealed class FromBodyAttribute : System.Attribute { }

            public sealed class FromQueryAttribute : System.Attribute { }

            public abstract class ControllerBase { }
        }
        """;

    /// <summary>Verifies the under-postable value members of a body-bound model are reported and the marked or nullable ones are not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BodyBoundValueMembersReportedAsync() =>
        VerifyAsync(
            """
            using System.ComponentModel.DataAnnotations;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.AspNetCore.Mvc.ModelBinding;

            public enum Priority
            {
                Low,
                High
            }

            public class OrderRequest
            {
                public int {|SST2705:Quantity|} { get; set; }

                public decimal {|SST2705:Price|} { get; set; }

                public Priority {|SST2705:Level|} { get; set; }

                public long {|SST2705:Sku|};

                public int? OptionalCount { get; set; }

                [Required]
                public int RequiredCount { get; set; }

                [BindRequired]
                public int BoundCount { get; set; }

                public int ReadOnlyId { get; }

                public int Internal { get; private set; }

                public string Note { get; set; } = "";
            }

            [ApiController]
            public class OrdersController : ControllerBase
            {
                public void Create(OrderRequest request) { }
            }
            """);

    /// <summary>Verifies an explicit <c>[FromBody]</c> parameter is treated as a body-bound model.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExplicitFromBodyReportedAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class Payload
            {
                public int {|SST2705:Amount|} { get; set; }
            }

            [ApiController]
            public class PaymentsController : ControllerBase
            {
                public void Pay([FromBody] Payload payload) { }
            }
            """);

    /// <summary>Verifies a model bound from the query string is out of scope.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task QueryBoundModelIsCleanAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class Filter
            {
                public int Page { get; set; }
            }

            [ApiController]
            public class SearchController : ControllerBase
            {
                public void Search([FromQuery] Filter filter) { }
            }
            """);

    /// <summary>Verifies a simple-type parameter is not a bound model.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SimpleParameterIsCleanAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            public class ItemsController : ControllerBase
            {
                public void Get(int id) { }
            }
            """);

    /// <summary>Verifies a body-bound model with no under-postable members is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReferenceAndNullableMembersAreCleanAsync() =>
        VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class Contact
            {
                public string Name { get; set; } = "";

                public int? Age { get; set; }
            }

            [ApiController]
            public class ContactsController : ControllerBase
            {
                public void Save(Contact contact) { }
            }
            """);

    /// <summary>Verifies the rule stays silent when the ASP.NET Core MVC types are absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SilentWhenMvcTypesAbsentAsync() =>
        VerifyAsync(
            """
            public sealed class ApiControllerAttribute : System.Attribute { }

            public abstract class ControllerBase { }

            public class OrderRequest
            {
                public int Quantity { get; set; }
            }

            [ApiController]
            public class OrdersController : ControllerBase
            {
                public void Create(OrderRequest request) { }
            }
            """);

    /// <summary>Verifies inherited controller markers and overridden actions still discover a model exactly once.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InheritedControllerAndOverriddenActionReportOnceAsync() =>
        VerifyAsync("""
            using Microsoft.AspNetCore.Mvc;
            public class Payload { public int {|SST2705:Count|}; }
            [ApiController]
            public abstract class BaseController : ControllerBase
            {
                public abstract void Save(Payload value);
            }
            public class Controller : BaseController
            {
                public override void Save(Payload value) { }
                public void Again(Payload first, Payload second) { }
                public override bool Equals(object other) => false;
                public override int GetHashCode() => 0;
            }
            """);

    /// <summary>Verifies action discovery excludes non-actions and unsuitable method shapes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonActionAndNonInstanceMethodsAreIgnoredAsync() =>
        VerifyAsync("""
            using Microsoft.AspNetCore.Mvc;
            public class Payload { public int Count; }
            [ApiController]
            public abstract class Controller : ControllerBase
            {
                [NonAction] public void Excluded(Payload value) { }
                [CustomNonAction] public void AlsoExcluded(Payload value) { }
                [System.Obsolete] public void Included(object value) { }
                public static void Static(Payload value) { }
                public abstract void Abstract(Payload value);
                public void Generic<T>(Payload value) { }
                private void Private(Payload value) { }
                public Controller(Payload value) { }
                public int this[Payload value] => 0;
            }
            public class CustomNonActionAttribute : NonActionAttribute { }
            namespace Microsoft.AspNetCore.Mvc { public class NonActionAttribute : System.Attribute { } }
            """);

    /// <summary>Verifies collection, metadata, array, and type-parameter inputs are not source model classes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonModelParameterShapesAreIgnoredAsync() =>
        VerifyAsync("""
            using Microsoft.AspNetCore.Mvc;
            public class Collection : System.Collections.IEnumerable
            {
                public int Count;
                public System.Collections.IEnumerator GetEnumerator() => null;
            }
            public class Payload { public int Count; }
            [ApiController]
            public class Controller<T> : ControllerBase where T : class
            {
                public void Save(Collection collection, string text, Payload[] array, T generic) { }
            }
            """);

    /// <summary>Verifies all non-body source markers, including derived markers, exclude parameters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EveryNonBodyBindingSourceIsExcludedAsync() =>
        VerifyAsync("""
            using Microsoft.AspNetCore.Mvc;
            public class Payload { public int Count; }
            [ApiController]
            public class Controller : ControllerBase
            {
                public void Save([FromQuery] Payload query, [FromRoute] Payload route,
                    [FromForm] Payload form, [FromHeader] Payload header, [FromServices] Payload services,
                    [CustomRoute] Payload derived) { }
            }
            public class CustomRouteAttribute : FromRouteAttribute { }
            namespace Microsoft.AspNetCore.Mvc
            {
                public class FromRouteAttribute : System.Attribute { }
                public class FromFormAttribute : System.Attribute { }
                public class FromHeaderAttribute : System.Attribute { }
                public class FromServicesAttribute : System.Attribute { }
            }
            """);

    /// <summary>Verifies only public writable non-nullable value members are underpostable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MemberShapesAndUnrelatedAttributesAreDistinguishedAsync() =>
        VerifyAsync("""
            using Microsoft.AspNetCore.Mvc;
            public class MarkerAttribute : System.Attribute { }
            public class Payload<T> where T : struct
            {
                [Marker] public int {|SST2705:Count|} { get; init; }
                [Marker] public int {|SST2705:Field|};
                public T Generic { get; set; }
                public static int Static { get; set; }
                public static int StaticField;
                public const int Constant = 1;
                public readonly int Readonly;
                internal int Internal;
                public int? Nullable;
                public object Reference;
                public int this[int i] { get => 0; set { } }
            }
            [ApiController]
            public class Controller<T> : ControllerBase where T : struct
            {
                [Marker] public void Save([Marker] Payload<T> value) { }
            }
            """);

    /// <summary>Verifies either missing required MVC marker disables the rule.</summary>
    /// <param name="marker">The one marker available in the compilation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public class ApiControllerAttribute : System.Attribute { }")]
    [Arguments("public class ControllerBase { }")]
    public async Task MissingMvcMarkerDisablesAnalysisAsync(string marker)
    {
        var test = new VerifyUnderposting.Test
        {
            ReferenceAssemblies = RoslynCommon.Analyzers.Tests.AnalyzerFrameworks.Net90,
            TestCode = $$"""
                public class Payload { public int Count; }
                public class Controller { public void Save(Payload value) { } }
                namespace Microsoft.AspNetCore.Mvc { {{marker}} }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies with the binding stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyUnderposting.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source + BindingStubs };

        await test.RunAsync(CancellationToken.None);
    }
}
