namespace readytohelpapi.Common.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public interface ISubmissionTransaction : IDisposable
{
    void Commit();
}

public interface ISubmissionTransactionFactory
{
    ISubmissionTransaction Begin(int key);
}

/// <summary>
/// Serializes a submission's checks and writes across API instances. Reports and feedback
/// use the occurrence type as their key, including the search for a new occurrence.
/// Negative keys are reserved for user administration.
/// </summary>
public sealed class SubmissionTransactionFactory(AppDbContext context) : ISubmissionTransactionFactory
{
    public ISubmissionTransaction Begin(int key)
    {
        var transaction = context.Database.BeginTransaction();
        try
        {
            // Fixed application namespace plus a parameterized key; released at transaction end.
            context.Database.ExecuteSqlInterpolated($"SELECT pg_advisory_xact_lock(7284, {key})");
            return new SubmissionTransaction(transaction);
        }
        catch
        {
            transaction.Dispose();
            throw;
        }
    }

    private sealed class SubmissionTransaction(IDbContextTransaction transaction) : ISubmissionTransaction
    {
        public void Commit() => transaction.Commit();
        public void Dispose() => transaction.Dispose();
    }
}
