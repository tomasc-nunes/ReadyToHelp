namespace readytohelpapi.Common.Tests;

using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using readytohelpapi.Authentication.Service;
using readytohelpapi.Common.Data;
using readytohelpapi.Feedback.Services;
using readytohelpapi.Occurrence.Models;
using readytohelpapi.Report.Services;
using readytohelpapi.User.Models;
using readytohelpapi.User.Services;
using Xunit;
using FeedbackModel = readytohelpapi.Feedback.Models.Feedback;
using ReportModel = readytohelpapi.Report.Models.Report;

/// <summary>HTTP regression tests using the real JWT middleware, without a database dependency.</summary>
[Trait("Category", "Unit")]
public class SecurityEndpointTests
{
    [Theory]
    [InlineData("api/user")]
    [InlineData("api/user/1")]
    [InlineData("api/user/email/user@example.com")]
    public async Task Citizen_CannotReadUserDirectory(string route)
    {
        using var app = new SecurityApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token("3", "CITIZEN"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
    }
    [Theory]
    [InlineData("api/reports")]
    [InlineData("api/feedback")]
    public async Task Submission_UsesValidatedSubject_NotClientIdentity(string route)
    {
        using var app = new SecurityApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token("3", "CITIZEN"));
        var response = await client.PostAsJsonAsync(route, new
        {
            userId = 1, id = 99, occurrenceId = 10, isConfirmed = true,
            title = "Report", description = "Details", type = "FLOOD", latitude = 40, longitude = -8,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(3, app.LastAuthor);
        Assert.Equal(0, app.LastFeedbackId);
    }

    [Theory]
    [InlineData("api/reports", null)]
    [InlineData("api/feedback", "invalid")]
    [InlineData("api/reports", "0")]
    public async Task Submission_WithoutValidSubject_IsRejected(string route, string? subject)
    {
        using var app = new SecurityApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token(subject, "CITIZEN"));
        var response = await client.PostAsJsonAsync(route, new { userId = 1, title = "T", description = "D" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, app.LastAuthor);
    }

    [Theory]
    [InlineData("POST", 0, "ADMIN")]
    [InlineData("PUT", 3, "ADMIN")]
    [InlineData("PUT", 1, "CITIZEN")]
    [InlineData("DELETE", 1, "ADMIN")]
    public async Task Manager_CannotCreatePromoteModifyOrDeleteAdministrator(string method, int target, string profile)
    {
        using var app = new SecurityApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token("2", "MANAGER"));
        using var request = new HttpRequestMessage(new HttpMethod(method), target == 0 ? "api/user" : $"api/user/{target}")
        {
            Content = JsonContent.Create(new { name = "User", email = "u@example.com", password = "changed", profile }),
        };
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        app.Users.Verify(u => u.Create(It.IsAny<User>()), Times.Never);
        app.Users.Verify(u => u.Update(It.IsAny<User>()), Times.Never);
        app.Users.Verify(u => u.Delete(It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData("ADMIN", "ADMIN")]
    [InlineData("MANAGER", "CITIZEN")]
    [InlineData("MANAGER", "MANAGER")]
    public async Task AuthorizedUserAdministration_RemainsAvailable(string actorRole, string requestedProfile)
    {
        using var app = new SecurityApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token("2", actorRole));
        var response = await client.PostAsJsonAsync("api/user", new
        {
            name = "User", email = "u@example.com", password = "example", profile = requestedProfile,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RejectsTokenForRequestsAndRefresh()
    {
        using var app = new SecurityApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token("1", "ADMIN"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/user/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("api/auth/refresh-token", null)).StatusCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task MissingIdentifierOrRevocationStoreFailure_RejectsAuthentication(bool omitId, bool storeFailure)
    {
        using var app = new SecurityApplication();
        using var client = app.CreateClient();
        app.Revocations.FailReads = storeFailure;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token("1", "ADMIN", omitId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/user/1")).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("CITIZEN")]
    public async Task UserWrites_RequireStaffRole(string? role)
    {
        using var app = new SecurityApplication();
        using var client = app.CreateClient();
        if (role != null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token("3", role));
        var response = await client.PostAsJsonAsync("api/user", new { name = "U", email = "e", password = "p" });
        Assert.Equal(role == null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class SecurityApplication : WebApplicationFactory<Program>
    {
        private const string Secret = "Security-test-only-signing-secret-with-at-least-32-bytes";
        public Mock<IUserService> Users { get; } = new();
        public MemoryRevocations Revocations { get; } = new();
        public int LastAuthor { get; private set; }
        public int LastFeedbackId { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:Secret", Secret);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = Secret, ["Jwt:Issuer"] = "SecurityTests", ["Jwt:Audience"] = "SecurityTests",
            }));
            builder.ConfigureTestServices(services =>
            {
                // Program captures JWT settings during startup; override validation for these
                // test-signed tokens while retaining its real bearer handler and events.
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
                    options.TokenValidationParameters.ValidIssuer = "SecurityTests";
                    options.TokenValidationParameters.ValidAudience = "SecurityTests";
                });
                Users.Setup(u => u.GetUserById(It.IsAny<int>())).Returns<int>(id => new User
                {
                    Id = id, Name = "User", Email = "user@example.com", Password = "hash",
                    Profile = id == 1 ? Profile.ADMIN : Profile.CITIZEN,
                });
                Users.Setup(u => u.Create(It.IsAny<User>())).Returns<User>(u => { u.Id = 9; return u; });
                Users.Setup(u => u.Update(It.IsAny<User>())).Returns<User>(u => u);
                Users.Setup(u => u.Delete(It.IsAny<int>())).Returns<int>(id => Users.Object.GetUserById(id));
                services.RemoveAll<IAuthSessionStore>();
                services.AddSingleton(Mock.Of<IAuthSessionStore>(s => s.Validate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()) == true));
                services.RemoveAll<IUserService>();
                services.AddSingleton(Users.Object);
                services.RemoveAll<ITokenRevocationStore>();
                services.AddSingleton<ITokenRevocationStore>(Revocations);
                services.RemoveAll<ISubmissionTransactionFactory>();
                services.AddSingleton(SecurityTestSupport.Transactions());
                var reports = new Mock<IReportService>();
                reports.Setup(r => r.Create(It.IsAny<ReportModel>())).Returns<ReportModel>(report =>
                {
                    LastAuthor = report.UserId;
                    return (report, new Occurrence { Id = 10, Status = OccurrenceStatus.WAITING });
                });
                services.RemoveAll<IReportService>();
                services.AddSingleton(reports.Object);
                var feedback = new Mock<IFeedbackService>();
                feedback.Setup(f => f.Create(It.IsAny<FeedbackModel>())).Returns<FeedbackModel>(input =>
                {
                    LastAuthor = input.UserId;
                    LastFeedbackId = input.Id;
                    return input;
                });
                services.RemoveAll<IFeedbackService>();
                services.AddSingleton(feedback.Object);
            });
        }

        public string Token(string? subject, string role, bool omitId = false)
        {
            var claims = new List<Claim> { new(ClaimTypes.Role, role), new("sid", "test-session") };
            if (subject != null) claims.Add(new Claim("sub", subject));
            if (!omitId) claims.Add(new Claim("jti", Guid.NewGuid().ToString("N")));
            var jwt = new JwtSecurityToken("SecurityTests", "SecurityTests", claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                    SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }
    }

    private sealed class MemoryRevocations : ITokenRevocationStore
    {
        private readonly ConcurrentDictionary<string, DateTime> tokens = new();
        public bool FailReads { get; set; }
        public bool IsRevoked(string id) => FailReads ? throw new InvalidOperationException("Test outage")
            : tokens.TryGetValue(id, out var expiry) && expiry > DateTime.UtcNow;
        public Task<bool> IsRevokedAsync(string id, CancellationToken cancellationToken) => Task.FromResult(IsRevoked(id));
        public void Revoke(string id, DateTime expiresAt) => tokens[id] = expiresAt;
    }
}
