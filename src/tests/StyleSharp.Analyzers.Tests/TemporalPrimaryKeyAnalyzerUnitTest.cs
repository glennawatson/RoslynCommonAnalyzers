// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyKey = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2475TemporalPrimaryKeyAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2475 (an entity whose primary key is a temporal type).</summary>
public class TemporalPrimaryKeyAnalyzerUnitTest
{
    /// <summary>The minimal Entity Framework stubs the convention path resolves by name.</summary>
    private const string EntityFrameworkStubs = """

                                                namespace Microsoft.EntityFrameworkCore
                                                {
                                                    public class DbContext { }

                                                    public class DbSet<TEntity>
                                                        where TEntity : class
                                                    { }
                                                }
                                                """;

    /// <summary>Verifies a key-attributed <c>DateTime</c> key is reported without the framework present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeyAttributeDateTimeIsFlaggedAsync()
        => await VerifyReportAsync(
            """
            using System;
            using System.ComponentModel.DataAnnotations;

            public class Reading
            {
                [Key]
                public DateTime {|SST2475:CapturedAt|} { get; set; }
            }
            """);

    /// <summary>Verifies a key-attributed <c>DateTimeOffset</c> key is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeyAttributeDateTimeOffsetIsFlaggedAsync()
        => await VerifyReportAsync(
            """
            using System;
            using System.ComponentModel.DataAnnotations;

            public class Reading
            {
                [Key]
                public DateTimeOffset {|SST2475:CapturedAt|} { get; set; }
            }
            """);

    /// <summary>Verifies a nullable temporal key attributed with the key attribute is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeyAttributeNullableDateTimeIsFlaggedAsync()
        => await VerifyReportAsync(
            """
            using System;
            using System.ComponentModel.DataAnnotations;

            public class Reading
            {
                [Key]
                public DateTime? {|SST2475:CapturedAt|} { get; set; }
            }
            """);

    /// <summary>Verifies the <c>Id</c> convention on a <c>DbSet</c> entity flags a temporal key.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConventionIdOnDbSetEntityIsFlaggedAsync()
        => await VerifyReportAsync(
            """
            using System;
            using Microsoft.EntityFrameworkCore;

            public class Order
            {
                public DateTime {|SST2475:Id|} { get; set; }
            }

            public class ShopContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }
            """ + EntityFrameworkStubs);

    /// <summary>Verifies the <c>&lt;TypeName&gt;Id</c> convention flags a temporal key on an entity.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConventionTypeNameIdOnDbSetEntityIsFlaggedAsync()
        => await VerifyReportAsync(
            """
            using System;
            using Microsoft.EntityFrameworkCore;

            public class Order
            {
                public DateTimeOffset {|SST2475:OrderId|} { get; set; }
            }

            public class ShopContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }
            """ + EntityFrameworkStubs);

    /// <summary>Verifies a nested entity registered through a <c>DbSet</c> field is flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedEntityWithDbSetFieldIsFlaggedAsync()
        => await VerifyReportAsync(
            """
            using System;
            using Microsoft.EntityFrameworkCore;

            public class Warehouse
            {
                public class Order
                {
                    public DateTime {|SST2475:Id|} { get; set; }
                }

                public class ShopContext : DbContext
                {
                    public DbSet<Order> Orders = null!;
                }
            }
            """ + EntityFrameworkStubs);

    /// <summary>Verifies an integer key attributed with the key attribute is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IntegerKeyAttributeIsCleanAsync()
        => await VerifyReportAsync(
            """
            using System.ComponentModel.DataAnnotations;

            public class Reading
            {
                [Key]
                public int Id { get; set; }
            }
            """);

    /// <summary>Verifies a non-temporal convention key on a <c>DbSet</c> entity is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonTemporalConventionKeyIsCleanAsync()
        => await VerifyReportAsync(
            """
            using Microsoft.EntityFrameworkCore;

            public class Order
            {
                public int Id { get; set; }
            }

            public class ShopContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }
            """ + EntityFrameworkStubs);

    /// <summary>Verifies a convention-named temporal key on a type not exposed by any <c>DbSet</c> is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConventionKeyWithoutDbSetMembershipIsCleanAsync()
        => await VerifyReportAsync(
            """
            using System;
            using Microsoft.EntityFrameworkCore;

            public class AuditRow
            {
                public DateTime Id { get; set; }
            }

            public class ShopContext : DbContext
            {
            }
            """ + EntityFrameworkStubs);

    /// <summary>Verifies a static convention-named temporal property is never treated as a key.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConventionStaticKeyIsCleanAsync()
        => await VerifyReportAsync(
            """
            using System;
            using Microsoft.EntityFrameworkCore;

            public class Order
            {
                public static DateTime Id { get; set; }
            }

            public class ShopContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }
            """ + EntityFrameworkStubs);

    /// <summary>Verifies a read-only convention-named temporal property is never treated as a key.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConventionGetOnlyKeyIsCleanAsync()
        => await VerifyReportAsync(
            """
            using System;
            using Microsoft.EntityFrameworkCore;

            public class Order
            {
                public DateTime Id { get; } = DateTime.UtcNow;
            }

            public class ShopContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }
            """ + EntityFrameworkStubs);

    /// <summary>Verifies a non-public convention-named temporal property is never treated as a key.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConventionNonPublicKeyIsCleanAsync()
        => await VerifyReportAsync(
            """
            using System;
            using Microsoft.EntityFrameworkCore;

            public class Order
            {
                internal DateTime Id { get; set; }
            }

            public class ShopContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }
            """ + EntityFrameworkStubs);

    /// <summary>Verifies the rule is silent when neither the framework nor the key attribute is present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NeitherFrameworkNorKeyPresentIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public class Thing
                              {
                                  public DateTime Id { get; set; }
                                  public DateTimeOffset ThingId { get; set; }
                              }
                              """;

        var test = new VerifyKey.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyReportAsync(string source)
    {
        var test = new VerifyKey.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
