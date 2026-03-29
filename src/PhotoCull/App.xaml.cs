using System.Windows;
using PhotoCull.Data;
using Microsoft.EntityFrameworkCore;

namespace PhotoCull;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Ensure database is created
        using var db = new PhotoCullDbContext();
        db.Database.EnsureCreated();
    }
}
