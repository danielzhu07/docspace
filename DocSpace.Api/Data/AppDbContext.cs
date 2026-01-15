using DocSpace.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DocSpace.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<Document> Documents => Set<Document>();
}
