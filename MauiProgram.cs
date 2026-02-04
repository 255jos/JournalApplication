using JournalApplication.data;
using JournalApplication.service;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace JournalApplication
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddMudServices();
            builder.Services.AddDbContext<AppDbContext>();
            builder.Services.AddScoped<IJournalService, JournalService>();

            //configuration for the quest pdf to be implemented in download pdf
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            //configuration for the database setup
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            }

            return app;
        }
    }
}
