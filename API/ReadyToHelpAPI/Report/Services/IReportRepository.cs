namespace readytohelpapi.Report.Services;

using Models;

/// <summary>
///  Defines the contract for report repository to manage data.
/// </summary>
public interface IReportRepository
{
    /// <summary>
    ///   Creates a report in the repository.
    /// </summary>
    /// <param name="report">The report object to be created.</param>
    /// <returns>The created report entity.</returns>
    Report Create(Report report);

    /// <summary>
    ///  Retrieves a report by ID.
    /// </summary>
    /// <param name="id">The report ID.</param>
    /// <returns>The report entity if found; otherwise, null.</returns>
    Report? GetById(int id);

    /// <summary>
    ///   Checks if a report exists for a specific occurrence and user.
    /// </summary>
    /// <param name="occurrenceId">The occurrence ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>True if a report exists; otherwise, false.</returns>
    bool HasReport(int occurrenceId, int userId);

    /// <summary>
    ///   Counts the number of reporters for a specific occurrence.
    /// </summary>
    /// <param name="occurrenceId">The occurrence ID.</param>
    /// <returns>The count of reporters.</returns>
    int CountReporters(int occurrenceId);

    /// <summary>
    ///   Links a report to an occurrence.
    /// </summary>
    /// <param name="report">The report to link.</param>
    /// <param name="occurrenceId">The occurrence ID.</param>
    void LinkToOccurrence(Report report, int occurrenceId);
}
