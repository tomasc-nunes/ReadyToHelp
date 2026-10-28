namespace readytohelpapi.Common.Tests;

using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using readytohelpapi.Authentication.Service;
using readytohelpapi.Common.Data;
using readytohelpapi.Feedback.Services;
using readytohelpapi.GeoPoint.Models;
using readytohelpapi.Notifications;
using readytohelpapi.Occurrence.Models;
using readytohelpapi.Occurrence.Services;
using readytohelpapi.Report.Services;
using readytohelpapi.ResponsibleEntity.Services;
using readytohelpapi.User.Models;
using Xunit;
using FeedbackModel = readytohelpapi.Feedback.Models.Feedback;
using ReportModel = readytohelpapi.Report.Models.Report;

[Trait("Category", "Integration")]
public class SecurityDatabaseTests
{
    [Fact]
    public void Session_RotatesWithoutExtendingLifetime_AndAccountChangesInvalidateIt()
    {
        using var database = new SecurityDatabase();
        var id = database.AddUsers(1)[0];
        using var context = database.Open();
        var user = context.Users.AsNoTracking().Single(u => u.Id == id);
        var store = new AuthSessionStore(context);
        var original = store.Issue(user, "first", null, null)!;
        var expiry = original.ExpiresAt;
        Assert.True(store.Validate(original.Id, "first", id));
        var rotated = store.Issue(user, "second", original.Id, "first")!;
        Assert.Equal(expiry, rotated.ExpiresAt);
        Assert.False(store.Validate(original.Id, "first", id));
        Assert.True(store.Validate(original.Id, "second", id));
        context.Users.Where(u => u.Id == id).ExecuteUpdate(s => s.SetProperty(u => u.Password, "changed"));
        Assert.False(store.Validate(original.Id, "second", id));
        Assert.Null(store.Issue(user, "third", original.Id, "second"));
    }

    [Fact]
    public void Session_RejectsReplayLogoutAndDeletedAccount()
    {
        using var database = new SecurityDatabase();
        var id = database.AddUsers(1)[0];
        using var context = database.Open();
        var user = context.Users.AsNoTracking().Single(u => u.Id == id);
        var store = new AuthSessionStore(context);
        var session = store.Issue(user, "a", null, null)!;
        store.Issue(user, "b", session.Id, "a");
        Assert.Null(store.Issue(user, "c", session.Id, "a"));
        Assert.False(store.Validate(session.Id, "b", id));
        var other = store.Issue(user, "d", null, null)!;
        store.Revoke(other.Id);
        Assert.False(store.Validate(other.Id, "d", id));
        var last = store.Issue(user, "e", null, null)!;
        context.Users.Where(u => u.Id == id).ExecuteDelete();
        Assert.False(store.Validate(last.Id, "e", id));
    }

    [Fact]
    public void Feedback_AfterTenMinutes_IsAcceptedWithoutAddingAnotherIndependentUser()
    {
        using var database = new SecurityDatabase();
        var user = database.AddUsers(1)[0];
        var occurrence = database.AddOccurrence();
        using var context = database.Open();
        context.Feedbacks.Add(new FeedbackModel { UserId = user, OccurrenceId = occurrence,
            IsConfirmed = true, FeedbackDateTime = DateTime.UtcNow.AddMinutes(-11) });
        context.SaveChanges();
        Feedback(context).Create(new FeedbackModel { UserId = user, OccurrenceId = occurrence, IsConfirmed = true });
        Assert.Equal(2, context.Feedbacks.Count());
        Assert.Equal(OccurrenceStatus.ACTIVE, context.Occurrences.Single().Status);
    }
    [Fact]
    public async Task Revocation_PersistsAcrossIndependentContexts_AndConcurrentLogoutIsIdempotent()
    {
        using var database = new SecurityDatabase();
        var id = Guid.NewGuid().ToString("N");
        var expiry = DateTime.UtcNow.AddHours(1);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            using var context = database.Open();
            new TokenRevocationStore(context).Revoke(id, expiry);
        })));
        using var freshContext = database.Open();
        Assert.True(await new TokenRevocationStore(freshContext).IsRevokedAsync(id, CancellationToken.None));
        Assert.Equal(1, freshContext.RevokedTokens.Count());
        new TokenRevocationStore(freshContext).Revoke("expired", DateTime.UtcNow.AddMinutes(-1));
        Assert.False(new TokenRevocationStore(freshContext).IsRevoked("expired"));
    }

    [Fact]
    public async Task ConcurrentReports_ByOneUser_CreateOnlyOneReportAndOccurrence()
    {
        using var database = new SecurityDatabase();
        var user = database.AddUsers(1)[0];
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            using var context = database.Open();
            try { Reports(context).Create(Report(user)); return true; }
            catch (ArgumentException ex) when (ex.Message.Contains("already reported")) { return false; }
        })));
        using var verify = database.Open();
        Assert.Single(outcomes, success => success);
        Assert.Equal(1, verify.Reports.Count());
        var occurrence = Assert.Single(verify.Occurrences);
        Assert.Equal(1, occurrence.ReportCount);
        Assert.Equal(OccurrenceStatus.WAITING, occurrence.Status);
    }

    [Fact]
    public async Task ConcurrentIndependentReporters_ActivateAtThree_WithoutLosingCounts()
    {
        using var database = new SecurityDatabase();
        var users = database.AddUsers(3);
        await Task.WhenAll(users.Select(user => Task.Run(() =>
        {
            using var context = database.Open();
            Reports(context).Create(Report(user));
        })));
        using var verify = database.Open();
        var occurrence = Assert.Single(verify.Occurrences);
        Assert.Equal(3, occurrence.ReportCount);
        Assert.Equal(OccurrenceStatus.ACTIVE, occurrence.Status);
        Assert.Equal(3, verify.Reports.Count(r => r.OccurrenceId == occurrence.Id));
    }

    [Fact]
    public async Task ConcurrentFeedback_ByOneUser_CannotBypassHourlyLimit()
    {
        using var database = new SecurityDatabase();
        var user = database.AddUsers(1)[0];
        var occurrence = database.AddOccurrence();
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            using var context = database.Open();
            try
            {
                Feedback(context).Create(new FeedbackModel { UserId = user, OccurrenceId = occurrence });
                return true;
            }
            catch (ArgumentException ex) when (ex.Message.Contains("within the last 10 minutes")) { return false; }
        })));
        using var verify = database.Open();
        Assert.Single(outcomes, success => success);
        Assert.Equal(1, verify.Feedbacks.Count());
        Assert.Equal(OccurrenceStatus.ACTIVE, verify.Occurrences.Single().Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedOrOldNegativeVotes_DoNotCloseOccurrence(bool oldVotes)
    {
        using var database = new SecurityDatabase();
        var users = database.AddUsers(6);
        var occurrence = database.AddOccurrence();
        using (var seed = database.Open())
        {
            seed.Feedbacks.AddRange(Enumerable.Range(0, 5).Select(index => new FeedbackModel
            {
                OccurrenceId = occurrence, UserId = oldVotes ? users[index] : users[0],
                IsConfirmed = false, FeedbackDateTime = oldVotes ? DateTime.UtcNow.AddHours(-2) : DateTime.UtcNow,
            }));
            seed.SaveChanges();
        }
        using var context = database.Open();
        Feedback(context).Create(new FeedbackModel { UserId = users[5], OccurrenceId = occurrence });
        Assert.Equal(OccurrenceStatus.ACTIVE, context.Occurrences.AsNoTracking().Single().Status);
    }

    [Fact]
    public async Task FiveIndependentRecentNegativeVotes_CloseOccurrence()
    {
        using var database = new SecurityDatabase();
        var users = database.AddUsers(5);
        var occurrence = database.AddOccurrence();
        await Task.WhenAll(users.Select(user => Task.Run(() =>
        {
            using var context = database.Open();
            Feedback(context).Create(new FeedbackModel { UserId = user, OccurrenceId = occurrence });
        })));
        using var verify = database.Open();
        var closed = verify.Occurrences.Single();
        Assert.Equal(OccurrenceStatus.CLOSED, closed.Status);
        Assert.NotEqual(default, closed.EndDateTime);
    }

    [Fact]
    public void LatestPositiveFeedback_SupersedesHistoricalNegativeVote()
    {
        using var database = new SecurityDatabase();
        var users = database.AddUsers(5);
        var occurrence = database.AddOccurrence();
        using var context = database.Open();
        context.Feedbacks.AddRange(users.Take(4).Select(user => new FeedbackModel
        {
            UserId = user, OccurrenceId = occurrence, FeedbackDateTime = DateTime.UtcNow.AddMinutes(-30),
        }));
        context.Feedbacks.Add(new FeedbackModel
        {
            UserId = users[0], OccurrenceId = occurrence, IsConfirmed = true,
        });
        context.SaveChanges();
        Feedback(context).Create(new FeedbackModel { UserId = users[4], OccurrenceId = occurrence });
        Assert.Equal(OccurrenceStatus.ACTIVE, context.Occurrences.AsNoTracking().Single().Status);
    }

    [Fact]
    public void FailedOccurrenceCreation_RollsBackItsReport()
    {
        using var database = new SecurityDatabase();
        var user = database.AddUsers(1)[0];
        using var context = database.Open();
        var service = new ReportServiceImpl(new ReportRepository(context),
            Mock.Of<IOccurrenceService>(s => s.GetOccurrencesByType(It.IsAny<OccurrenceType>()) == new List<Occurrence>()),
            Mock.Of<IResponsibleEntityService>(), Mock.Of<INotifierClient>(), new SubmissionTransactionFactory(context));
        Assert.ThrowsAny<Exception>(() => service.Create(Report(user)));
        using var verify = database.Open();
        Assert.Empty(verify.Reports);
    }

    [Fact]
    public void NewMigration_BackfillsAnchorsWithoutInventingReporters_AndCanRollBack()
    {
        using var database = new SecurityDatabase();
        var user = database.AddUsers(1)[0];
        using var context = database.Open();
        var (_, occurrence) = Reports(context).Create(Report(user));
        var unlinked = Report(user);
        context.Reports.Add(unlinked);
        context.SaveChanges();
        context.Database.ExecuteSqlRaw("""
            ALTER TABLE reports DROP CONSTRAINT "FK_reports_occurrences_OccurrenceId";
            ALTER TABLE reports DROP COLUMN "OccurrenceId";
            DROP TABLE revoked_tokens;
            DROP TABLE auth_sessions;
            UPDATE occurrences SET "ReportCount" = 99;
            CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
            """);
        var migrations = context.Database.GetMigrations().ToArray();
        foreach (var migration in migrations[..^2])
            context.Database.ExecuteSqlInterpolated($"INSERT INTO \"__EFMigrationsHistory\" VALUES ({migration}, '9.0.9')");
        context.Database.Migrate();
        context.ChangeTracker.Clear();
        Assert.Equal(1, context.Occurrences.Single().ReportCount);
        Assert.Equal(occurrence.Id, context.Reports.Single(r => r.Id == occurrence.ReportId).OccurrenceId);
        Assert.Null(context.Reports.Single(r => r.Id == unlinked.Id).OccurrenceId);
        Assert.Equal(2, context.Reports.Count());
        var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(context);
        migrator.Migrate(migrations[^3]);
        using var command = context.Database.GetDbConnection().CreateCommand();
        context.Database.OpenConnection();
        command.CommandText = "SELECT COUNT(*) FROM reports";
        Assert.Equal(2L, command.ExecuteScalar());
    }

    private static ReportModel Report(int user) => new()
    {
        UserId = user, Title = "Road report", Description = "Details", Type = OccurrenceType.ROAD_DAMAGE,
        Location = new GeoPoint { Latitude = 41, Longitude = -8 },
    };

    private static IOccurrenceService Occurrences(AppDbContext context) =>
        new OccurrenceServiceImpl(new OccurrenceRepository(context), Mock.Of<IResponsibleEntityService>());

    private static ReportServiceImpl Reports(AppDbContext context)
    {
        var responsible = new Mock<IResponsibleEntityService>();
        responsible.Setup(service => service.FindResponsibleEntity(
            It.IsAny<OccurrenceType>(), It.IsAny<double>(), It.IsAny<double>()))
            .Returns(context.ResponsibleEntities.AsNoTracking().First());
        return new ReportServiceImpl(new ReportRepository(context), Occurrences(context),
            responsible.Object, Mock.Of<INotifierClient>(), new SubmissionTransactionFactory(context));
    }

    private static FeedbackServiceImpl Feedback(AppDbContext context) => new(new FeedbackRepository(context),
        Occurrences(context), new SubmissionTransactionFactory(context));

    private sealed class SecurityDatabase : IDisposable
    {
        private readonly string connection;
        public SecurityDatabase()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            var settings = new NpgsqlConnectionStringBuilder
            {
                Host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost",
                Port = int.Parse(Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432"),
                Username = Environment.GetEnvironmentVariable("POSTGRES_USERNAME") ?? "readytohelp",
                Password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "readytohelppwd",
                Database = $"rth_security_{Guid.NewGuid():N}",
            };
            connection = settings.ConnectionString;
            using var context = Open();
            context.Database.EnsureCreated();
            context.ResponsibleEntities.Add(new readytohelpapi.ResponsibleEntity.Models.ResponsibleEntity { Name = "Test entity" });
            context.SaveChanges();
        }

        public AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, options => options.UseNetTopologySuite()).Options);

        public int[] AddUsers(int count)
        {
            using var context = Open();
            var users = Enumerable.Range(0, count).Select(_ => new User
            {
                Name = "User", Email = $"{Guid.NewGuid():N}@example.com", Password = "unused", Profile = Profile.CITIZEN,
            }).ToArray();
            context.Users.AddRange(users);
            context.SaveChanges();
            return users.Select(user => user.Id).ToArray();
        }

        public int AddOccurrence()
        {
            using var context = Open();
            var occurrence = new Occurrence
            {
                Title = "Road", Description = "Details", Type = OccurrenceType.ROAD_DAMAGE,
                Status = OccurrenceStatus.ACTIVE, Location = new GeoPoint { Latitude = 41, Longitude = -8 },
                CreationDateTime = DateTime.UtcNow,
            };
            context.Occurrences.Add(occurrence);
            context.SaveChanges();
            return occurrence.Id;
        }

        public void Dispose()
        {
            using var context = Open();
            // The database name is generated here; no application database is ever removed.
            context.Database.EnsureDeleted();
        }
    }
}
