namespace readytohelpapi.Report.Services;

using Microsoft.EntityFrameworkCore;
using readytohelpapi.Common.Data;
using readytohelpapi.Report.Models;

/// <summary>
///  Repository for managing report data.
/// </summary>
public class ReportRepository : IReportRepository
{
    private readonly AppDbContext reportContext;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReportRepository" /> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public ReportRepository(AppDbContext context)
    {
        reportContext = context;
    }

    /// <inheritdoc />
    public Report Create(Report report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        try
        {
            if (report.ReportDateTime == default)
                report.ReportDateTime = DateTime.UtcNow;

            var created = reportContext.Reports.Add(report).Entity;
            reportContext.SaveChanges();
            return created;
        }
        catch (Exception ex)
        {
            throw new DbUpdateException("Failed to create report", ex);
        }
    }

    /// <inheritdoc />
    public Report? GetById(int id)
    {
        if (id <= 0)
            return null;
        return reportContext.Reports.AsNoTracking().FirstOrDefault(r => r.Id == id);
    }

    /// <inheritdoc />
    public bool HasReport(int occurrenceId, int userId) => reportContext.Reports
        .Any(r => r.OccurrenceId == occurrenceId && r.UserId == userId);

    /// <inheritdoc />
    public int CountReporters(int occurrenceId) => reportContext.Reports
        .Where(r => r.OccurrenceId == occurrenceId).Select(r => r.UserId).Distinct().Count();

    /// <inheritdoc />
    public void LinkToOccurrence(Report report, int occurrenceId)
    {
        report.OccurrenceId = occurrenceId;
        reportContext.SaveChanges();
    }
}
