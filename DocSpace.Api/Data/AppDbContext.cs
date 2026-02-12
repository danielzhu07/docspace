using DocSpace.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DocSpace.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DocumentChunk>()
            .HasOne(c => c.Document)
            .WithMany(d => d.Chunks)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
