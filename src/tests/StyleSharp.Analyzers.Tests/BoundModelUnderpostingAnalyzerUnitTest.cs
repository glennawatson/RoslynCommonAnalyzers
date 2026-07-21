// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    [Test]
    public async Task BodyBoundValueMembersReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ExplicitFromBodyReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task QueryBoundModelIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task SimpleParameterIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ReferenceAndNullableMembersAreCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task SilentWhenMvcTypesAbsentAsync()
        => await VerifyAsync(
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

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies with the binding stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyUnderposting.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + BindingStubs
        };

        await test.RunAsync(CancellationToken.None);
    }
}
