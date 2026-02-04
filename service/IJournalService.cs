using JournalApplication.common;
using JournalApplicaton.Entities;

namespace JournalApplication.service;
public interface IJournalService
{
    Task<ServiceResult<Journal>> AddOrUpdateJournalAsync(Journal Journal);
    Task<Journal?> GetJournalByDateAsync(DateTime date);
    Task<Journal?> GetJournalByIdAsync(int id);
    Task<(List<Journal> Journals, int TotalCount)> GetAllJournalsAsync(int page = 1, int pageSize = 10);
    Task DeleteJournalAsync(DateTime date);
    Task<(List<Journal>, int)> SearchJournalsAsync(
    int userId,
    string title,
    string mood,
    string tag,
    DateTime? intialDate,
    DateTime? finalDate,
    int page,
    int pageSize);
    Task<byte[]> GenerateJournalPdfAsync(DateTime fromDate, DateTime toDate);
}
