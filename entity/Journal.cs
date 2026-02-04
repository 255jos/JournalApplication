namespace JournalApplicaton.Entities;

public class Journal
{
    public int JournalId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Desciption { get; set; } = string.Empty;

    public string PrimaryMood { get; set; } = string.Empty;

    public List<String> SecondaryMoods { get; set; } = new();

    public List<String> Tags { get; set; } = new();

    public int WordCount { get; set; }

    public DateTime CreateAT{ get; set; }
    public DateTime UpdatedAt { get; set; }
}