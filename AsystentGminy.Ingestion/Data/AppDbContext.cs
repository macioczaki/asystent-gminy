using AsystentGminy.Ingestion.Models;
using Microsoft.EntityFrameworkCore;

namespace AsystentGminy.Ingestion.Data;

public class AppDbContext : DbContext
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Chunk> Chunks => Set<Chunk>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseNpgsql(
            "Host=localhost;Port=5432;Database=asystent_gminy;Username=postgres;Password=mojehaslo",
            o => o.UseVector());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Document>(e =>
        {
            e.ToTable("documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.SourcePath).HasColumnName("source_path");
            e.Property(x => x.SourceType).HasColumnName("source_type");
            e.Property(x => x.Checksum).HasColumnName("checksum");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Chunk>(e =>
        {
            e.ToTable("chunks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.ChunkIndex).HasColumnName("chunk_index");
            e.Property(x => x.Embedding).HasColumnType("vector(768)").HasColumnName("embedding");
            e.HasOne(x => x.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}