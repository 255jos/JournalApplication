using JournalApplication.common;
using JournalApplicaton.Entities;
using JournalApplication.data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System.Text.RegularExpressions;
using Colors = QuestPDF.Helpers.Colors;

namespace JournalApplication.service;

public class JournalService : IJournalService
{
    private readonly AppDbContext _context;

    public JournalService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<Journal>> AddOrUpdateJournalAsync(Journal journal)
    {
        try
        {
            if (journal == null)
                return ServiceResult<Journal>.FailureResult("Journal cannot be null");

            if (string.IsNullOrWhiteSpace(journal.Title))
                return ServiceResult<Journal>.FailureResult("Title is required");

            // Prevent future dates
            if (journal.CreateAT.Date > DateTime.Today)
                return ServiceResult<Journal>.FailureResult("Cannot create journal entries for future dates");

            var entryDate = journal.CreateAT.Date;

            // Check if journal exists for this date (one entry per day)
            var existingJournal = await _context.Journals
                .FirstOrDefaultAsync(j => j.CreateAT.Date == entryDate);

            if (existingJournal != null)
            {
                // UPDATE existing
                existingJournal.Title = journal.Title;
                existingJournal.Desciption = journal.Desciption;
                existingJournal.PrimaryMood = journal.PrimaryMood;
                existingJournal.SecondaryMoods = journal.SecondaryMoods ?? new List<string>();
                existingJournal.Tags = journal.Tags ?? new List<string>();
                existingJournal.WordCount = CountWords(journal.Desciption);
                existingJournal.UpdatedAt = DateTime.Now;

                _context.Journals.Update(existingJournal);
                await _context.SaveChangesAsync();

                return ServiceResult<Journal>.SuccessResult(existingJournal);
            }
            else
            {
                // CREATE new
                journal.CreateAT = entryDate;
                journal.UpdatedAt = DateTime.Now;
                journal.WordCount = CountWords(journal.Desciption);
                journal.SecondaryMoods = journal.SecondaryMoods ?? new List<string>();
                journal.Tags = journal.Tags ?? new List<string>();

                await _context.Journals.AddAsync(journal);
                await _context.SaveChangesAsync();

                return ServiceResult<Journal>.SuccessResult(journal);
            }
        }
        catch (Exception ex)
        {
            return ServiceResult<Journal>.FailureResult($"Error saving journal: {ex.Message}");
        }
    }

    public async Task<Journal?> GetJournalByDateAsync(DateTime date)
    {
        try
        {
            var entryDate = date.Date;
            return await _context.Journals
                .FirstOrDefaultAsync(j => j.CreateAT.Date == entryDate);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving journal: {ex.Message}", ex);
        }
    }

    public async Task<Journal?> GetJournalByIdAsync(int id)
    {
        try
        {
            return await _context.Journals.FindAsync(id);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving journal: {ex.Message}", ex);
        }
    }

    public async Task<(List<Journal> Journals, int TotalCount)> GetAllJournalsAsync(int page = 1, int pageSize = 10)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var totalCount = await _context.Journals.CountAsync();

            var journals = await _context.Journals
                .OrderByDescending(j => j.CreateAT)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (journals, totalCount);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving journals: {ex.Message}", ex);
        }
    }

    public async Task DeleteJournalAsync(DateTime date)
    {
        try
        {
            var entryDate = date.Date;
            var journal = await _context.Journals
                .FirstOrDefaultAsync(j => j.CreateAT.Date == entryDate);

            if (journal != null)
            {
                _context.Journals.Remove(journal);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error deleting journal: {ex.Message}", ex);
        }
    }

    public async Task<(List<Journal>, int)> SearchJournalsAsync(
        int userId,
        string title,
        string mood,
        string tag,
        DateTime? intialDate,
        DateTime? finalDate,
        int page,
        int pageSize)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Journals.AsQueryable();

            // Search by title or description
            if (!string.IsNullOrWhiteSpace(title))
            {
                var searchTerm = title.ToLower();
                query = query.Where(j =>
                    j.Title.ToLower().Contains(searchTerm) ||
                    j.Desciption.ToLower().Contains(searchTerm));
            }

            // Filter by mood
            if (!string.IsNullOrWhiteSpace(mood))
            {
                var moodSearch = mood.ToLower();
                query = query.Where(j =>
                    j.PrimaryMood.ToLower() == moodSearch ||
                    j.SecondaryMoods.Any(m => m.ToLower() == moodSearch));
            }

            // Filter by tag
            if (!string.IsNullOrWhiteSpace(tag))
            {
                var tagSearch = tag.ToLower();
                query = query.Where(j => j.Tags.Any(t => t.ToLower().Contains(tagSearch)));
            }

            // Date range filter
            if (intialDate.HasValue)
            {
                var fromDate = intialDate.Value.Date;
                query = query.Where(j => j.CreateAT.Date >= fromDate);
            }

            if (finalDate.HasValue)
            {
                var toDate = finalDate.Value.Date;
                query = query.Where(j => j.CreateAT.Date <= toDate);
            }

            var totalCount = await query.CountAsync();

            var journals = await query
                .OrderByDescending(j => j.CreateAT)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (journals, totalCount);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error searching journals: {ex.Message}", ex);
        }
    }

    public async Task<byte[]> GenerateJournalPdfAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            var startDate = fromDate.Date;
            var endDate = toDate.Date;

            if (startDate > endDate)
                throw new ArgumentException("Start date cannot be after end date");

            var journals = await _context.Journals
                .Where(j => j.CreateAT.Date >= startDate && j.CreateAT.Date <= endDate)
                .OrderBy(j => j.CreateAT)
                .ToListAsync();

            if (!journals.Any())
                throw new InvalidOperationException("No journals found in the specified date range");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    // Header
                    page.Header().Column(header =>
                    {
                        header.Item().Text("My Journal")
                            .SemiBold()
                            .FontSize(20)
                            .AlignCenter()
                            .FontColor(Colors.Blue.Darken2);

                        header.Item().PaddingTop(5).Text(
                            $"{startDate:dd MMMM yyyy} - {endDate:dd MMMM yyyy}")
                            .AlignCenter()
                            .FontSize(11)
                            .FontColor(Colors.Grey.Medium);

                        header.Item().PaddingTop(10).LineHorizontal(1)
                            .LineColor(Colors.Grey.Lighten1);
                    });

                    // Content
                    page.Content().PaddingTop(20).Column(content =>
                    {
                        foreach (var journal in journals)
                        {
                            content.Item()
                                .PaddingBottom(20)
                                .Column(entry =>
                                {
                                    entry.Item().Text(journal.CreateAT.ToString("dddd, dd MMMM yyyy"))
                                        .SemiBold()
                                        .FontSize(13)
                                        .FontColor(Colors.Blue.Medium);

                                    entry.Item().PaddingTop(5)
                                        .Text(journal.Title)
                                        .Bold()
                                        .FontSize(14);

                                    entry.Item().PaddingTop(5).Row(row =>
                                    {
                                        row.RelativeItem().Text($"Mood: {journal.PrimaryMood}")
                                            .FontSize(10)
                                            .FontColor(Colors.Grey.Darken1);

                                        row.RelativeItem().AlignRight()
                                            .Text($"Words: {journal.WordCount}")
                                            .FontSize(10)
                                            .FontColor(Colors.Grey.Darken1);
                                    });

                                    if (journal.Tags != null && journal.Tags.Any())
                                    {
                                        entry.Item().PaddingTop(3)
                                            .Text($"Tags: {string.Join(", ", journal.Tags)}")
                                            .FontSize(10)
                                            .FontColor(Colors.Grey.Darken1);
                                    }

                                    entry.Item().PaddingTop(8)
                                        .Text(CleanHtml(journal.Desciption))
                                        .FontSize(11)
                                        .LineHeight(1.5f);

                                    entry.Item().PaddingTop(15)
                                        .LineHorizontal(0.5f)
                                        .LineColor(Colors.Grey.Lighten2);
                                });
                        }
                    });

                    // Footer
                    page.Footer().AlignCenter().Text(footer =>
                    {
                        footer.Span("Generated on ");
                        footer.Span(DateTime.Now.ToString("dd MMMM yyyy 'at' HH:mm"))
                            .SemiBold();
                        footer.Span(" | Page ");
                        footer.CurrentPageNumber();
                    });
                });
            });
            return document.GeneratePdf();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error generating PDF: {ex.Message}", ex);
        }
    }

    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var cleanText = CleanHtml(text);
        var words = cleanText.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Length;
    }

    private string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        html = Regex.Replace(html, @"<(br|BR)\s*/?>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</(p|div|li|h[1-6])>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</ul>|</ol>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<[^>]+>", string.Empty);
        html = System.Net.WebUtility.HtmlDecode(html);
        html = Regex.Replace(html, @"\n\s*\n", "\n\n");
        html = Regex.Replace(html, @"[ \t]+", " ");

        return html.Trim();
    }
}
