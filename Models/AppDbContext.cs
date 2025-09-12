using Microsoft.EntityFrameworkCore;

namespace VideoLibrary.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Video> Videos { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<VideoTag> VideoTags { get; set; }
        public DbSet<Gallery> Galleries { get; set; }
        public DbSet<GalleryTag> GalleryTags { get; set; }
        public DbSet<LogEntry> LogEntries { get; set; }
        public DbSet<Playlist> Playlists { get; set; }

        public DbSet<PlaylistTag> PlaylistTags { get; set; }
        public DbSet<Content> Contents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VideoTag>()
                .HasKey(vt => new { vt.VideoId, vt.TagId });

            modelBuilder.Entity<VideoTag>()
                .HasOne(vt => vt.Video)
                .WithMany(v => v.VideoTags)
                .HasForeignKey(vt => vt.VideoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VideoTag>()
                .HasOne(vt => vt.Tag)
                .WithMany(t => t.VideoTags)
                .HasForeignKey(vt => vt.TagId)
                .OnDelete(DeleteBehavior.Cascade); ;

            // Gallery-Tag relationship
            modelBuilder.Entity<GalleryTag>()
                .HasKey(gt => new { gt.GalleryId, gt.TagId });

            modelBuilder.Entity<GalleryTag>()
                .HasOne(gt => gt.Gallery)
                .WithMany(g => g.GalleryTags)
                .HasForeignKey(gt => gt.GalleryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GalleryTag>()
                .HasOne(gt => gt.Tag)
                .WithMany(g => g.GalleryTags)
                .HasForeignKey(gt => gt.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            // Playlist-Tag relationship
            modelBuilder.Entity<PlaylistTag>()
                .HasKey(pt => new { pt.PlaylistId, pt.TagId });

            modelBuilder.Entity<PlaylistTag>()
                .HasOne(pt => pt.Playlist)
                .WithMany(p => p.PlaylistTags)
                .HasForeignKey(pt => pt.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlaylistTag>()
                .HasOne(pt => pt.Tag)
                .WithMany(t => t.PlaylistTags)
                .HasForeignKey(pt => pt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
