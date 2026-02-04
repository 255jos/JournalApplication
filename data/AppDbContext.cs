using JournalApplicaton.Entities;
using Microsoft.EntityFrameworkCore;

namespace JournalApplication.data;
public class AppDbContext : DbContext
{
    public DbSet<Journal> Journals { get; set; } = null!;

    private readonly string _dbPath;

    public AppDbContext()
    {
        // Path to store SQLite DB on device
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _dbPath = System.IO.Path.Combine(folder, "app.db");
    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }
}