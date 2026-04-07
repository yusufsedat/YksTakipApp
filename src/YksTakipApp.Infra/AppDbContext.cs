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
        public DbSet<ExamDetail> ExamDetails => Set<ExamDetail>();
        public DbSet<ScheduleEntry> ScheduleEntries => Set<ScheduleEntry>();
        public DbSet<ProblemNote> ProblemNotes => Set<ProblemNote>();

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
        e.Property(x => x.Role)
            .HasMaxLength(20)
            .IsRequired();
        e.Property(x => x.RefreshToken)
            .HasMaxLength(512);

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
        e.Property(x => x.Subject)
            .HasMaxLength(60)
            .IsRequired();

        e.HasIndex(x => x.Name);
        e.HasIndex(x => x.Category);
        e.HasIndex(x => x.Subject);
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

        e.HasOne(x => x.Topic)
         .WithMany()
         .HasForeignKey(x => x.TopicId)
         .OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.UserId);
        e.HasIndex(x => x.Date);
        e.HasIndex(x => x.TopicId);
    });

    // ---------- ExamResult ----------
    modelBuilder.Entity<ExamResult>(e =>
    {
        e.HasKey(x => x.Id);

        e.HasOne(x => x.User)
         .WithMany(u => u.ExamResults)
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        e.Property(x => x.ExamName).HasMaxLength(150).IsRequired();
        e.Property(x => x.ExamType).HasMaxLength(10).IsRequired().HasDefaultValue("TYT");
        e.Property(x => x.Subject).HasMaxLength(60);
        e.Property(x => x.Date).IsRequired();
        e.Property(x => x.ErrorReasons).HasMaxLength(500);

        e.HasIndex(x => x.UserId);
        e.HasIndex(x => new { x.UserId, x.Date });
        e.HasIndex(x => x.ExamType);
    });

    // ---------- ExamDetail ----------
    modelBuilder.Entity<ExamDetail>(e =>
    {
        e.HasKey(x => x.Id);

        e.HasOne(x => x.ExamResult)
         .WithMany(r => r.ExamDetails)
         .HasForeignKey(x => x.ExamResultId)
         .OnDelete(DeleteBehavior.Cascade);

        e.Property(x => x.Subject).HasMaxLength(60).IsRequired();

        e.HasIndex(x => x.ExamResultId);
    });

    // ---------- ScheduleEntry ----------
    modelBuilder.Entity<ScheduleEntry>(e =>
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Recurrence).HasMaxLength(20).IsRequired();
        e.Property(x => x.Title).HasMaxLength(150).IsRequired();

        e.HasOne(x => x.User)
         .WithMany(u => u.ScheduleEntries)
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Topic)
         .WithMany()
         .HasForeignKey(x => x.TopicId)
         .OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.UserId);
        e.HasIndex(x => x.TopicId);
    });

    // ---------- ProblemNote ----------
    modelBuilder.Entity<ProblemNote>(e =>
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.ImageUrl).IsRequired();
        e.Property(x => x.ImagePublicId).HasMaxLength(512);
        e.Property(x => x.TagsJson)
            .HasMaxLength(4000)
            .IsRequired();

        e.HasOne(x => x.User)
         .WithMany(u => u.ProblemNotes)
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => x.UserId);
        e.HasIndex(x => x.CreatedAt);
        e.HasIndex(x => new { x.UserId, x.IsDeleted });
    });
        }
    }
}

