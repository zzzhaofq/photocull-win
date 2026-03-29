using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhotoCull.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PhotoCullDbContext>
{
    public PhotoCullDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PhotoCullDbContext>()
            .UseSqlite("Data Source=photocull_design.db")
            .Options;

        return new PhotoCullDbContext(options);
    }
}
