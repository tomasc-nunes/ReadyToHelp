namespace readytohelpapi.Common.Tests;

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using readytohelpapi.Common.Data;

internal static class SecurityTestSupport
{
    public static void SignIn(ControllerBase controller, int id = 1, string role = "ADMIN") =>
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                    new Claim(ClaimTypes.Role, role),
                }, "Test")),
            },
        };

    public static ISubmissionTransactionFactory Transactions() => new TestTransactions();

    // Only unit tests bypass PostgreSQL transactions. Production always uses database locks.
    private sealed class TestTransactions : ISubmissionTransactionFactory
    {
        public ISubmissionTransaction Begin(int key) => new TestTransaction();
    }

    private sealed class TestTransaction : ISubmissionTransaction
    {
        public void Commit() { }
        public void Dispose() { }
    }
}
