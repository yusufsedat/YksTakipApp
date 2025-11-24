using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;

namespace YksTakipApp.Infra
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Topic> Topics => Set<Topic>();
        public DbSet<UserTopic> UserTopics => Set<UserTopic>();
        public DbSet<StudyTime> StudyTimes => Set<StudyTime>();
        public DbSet<ExamResult> ExamResults => Set<ExamResult>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<User>(e =>
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();
        e.Property(x => x.Email)
            .HasMaxLength(255)
            .IsRequired();

        // Unique Email
        e.HasIndex(x => x.Email).IsUnique();
    });

    // ---------- Topic ----------
    modelBuilder.Entity<Topic>(e =>
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();
        e.Property(x => x.Category)
            .HasMaxLength(10)
            .IsRequired();

        e.HasIndex(x => x.Name);
        e.HasIndex(x => x.Category);
    });

    // ---------- UserTopic (junction) ----------
    modelBuilder.Entity<UserTopic>(e =>
    {
        // Composite key
        e.HasKey(ut => new { ut.UserId, ut.TopicId });

        e.HasOne(ut => ut.User)
         .WithMany(u => u.UserTopics)
         .HasForeignKey(ut => ut.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(ut => ut.Topic)
         .WithMany(t => t.UserTopics)
         .HasForeignKey(ut => ut.TopicId)
         .OnDelete(DeleteBehavior.Cascade);

        // Sık sorgular için indeks
        e.HasIndex(ut => ut.UserId);
        e.HasIndex(ut => ut.TopicId);
    });

    // ---------- StudyTime ----------
    modelBuilder.Entity<StudyTime>(e =>
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.DurationMinutes).IsRequired();
        e.Property(x => x.Date).IsRequired();

        e.HasOne(x => x.User)
         .WithMany(u => u.StudyTimes)
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => x.UserId);
        e.HasIndex(x => x.Date);
    });

    // ---------- ExamResult ----------
    modelBuilder.Entity<ExamResult>(e =>
    {
        e.HasKey(x => x.Id);

        e.HasOne(x => x.User)
         .WithMany(u => u.ExamResults)
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        e.Property(x => x.ExamName)
         .HasMaxLength(150)
         .IsRequired();
        e.Property(x => x.Date).IsRequired();

        e.HasIndex(x => x.UserId);
        e.HasIndex(x => new { x.UserId, x.Date });
    });
        }
    }
}

