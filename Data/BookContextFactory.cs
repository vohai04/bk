using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookInfoFinder.Data;

// Scaffolding migrations must not run web startup or touch the configured database.
public sealed class BookContextFactory : IDesignTimeDbContextFactory<BookContext>
{
    public BookContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<BookContext>()
        .UseNpgsql("Host=127.0.0.1;Database=bookinfo_design;Username=design")
        .Options);
}
