using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Enums;

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
        public DbSet<ProblemNote> ProblemNotes => Set<ProblemNote>();
        public DbSet<UserGoal> UserGoals => Set<UserGoal>();
        public DbSet<ScheduleTask> ScheduleTasks => Set<ScheduleTask>();
        public DbSet<TopicPrerequisite> TopicPrerequisites => Set<TopicPrerequisite>();
        public DbSet<CommandExecution> CommandExecutions => Set<CommandExecution>();
        public DbSet<UserNotificationLog> UserNotificationLogs => Set<UserNotificationLog>();
        public DbSet<UserPlannerChurnEvent> UserPlannerChurnEvents => Set<UserPlannerChurnEvent>();
        public DbSet<PlannerDecisionLog> PlannerDecisionLogs => Set<PlannerDecisionLog>();
        public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
        public DbSet<UserFeatureFlagOverride> UserFeatureFlagOverrides => Set<UserFeatureFlagOverride>();
        public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();

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

        e.Property(x => x.SmartOnboardingSkipCount)
            .HasDefaultValue(0)
            .IsRequired();

        e.HasOne<UserGoal>()
            .WithMany()
            .HasForeignKey(x => x.ActiveGoalVersionId)
            .OnDelete(DeleteBehavior.NoAction);
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

        e.Property(x => x.OsymWeight)
            .HasDefaultValue(1.0)
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

        e.Property(ut => ut.MasteryStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(MasteryStatus.NotStarted)
            .IsRequired();

        e.Property(ut => ut.MasteryConfidence).IsRequired();
        e.Property(ut => ut.IsLocked).HasDefaultValue(false).IsRequired();
        e.Property(ut => ut.IsPriorityRequested).HasDefaultValue(false).IsRequired();
        e.Property(ut => ut.PriorityRequestedAt);
        e.Property(ut => ut.PriorityExpiresAt);
        e.Property(ut => ut.PriorityResolvedAt);

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
        e.HasIndex(ut => new { ut.UserId, ut.IsLocked });
        e.HasIndex(ut => new { ut.UserId, ut.IsPriorityRequested, ut.PriorityExpiresAt });
    });

    // ---------- TopicPrerequisite ----------
    modelBuilder.Entity<TopicPrerequisite>(e =>
    {
        e.HasKey(x => x.Id);
        MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(e.Property(x => x.Id));

        e.HasIndex(x => new { x.TopicId, x.PrerequisiteTopicId }).IsUnique();

        e.HasOne(x => x.Topic)
            .WithMany(t => t.Prerequisites)
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.PrerequisiteTopic)
            .WithMany(t => t.Dependents)
            .HasForeignKey(x => x.PrerequisiteTopicId)
            .OnDelete(DeleteBehavior.Restrict);

        e.ToTable(t => t.HasCheckConstraint(
            "CK_TopicPrerequisite_NoSelf",
            "`TopicId` <> `PrerequisiteTopicId`"));
    });

    // ---------- StudyTime ----------
    modelBuilder.Entity<StudyTime>(e =>
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.DurationMinutes).IsRequired();
        e.Property(x => x.Date).IsRequired();
        e.Property(x => x.ClientRequestId).HasMaxLength(100);

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
        e.HasIndex(x => new { x.UserId, x.Date });
        e.HasIndex(x => x.TopicId);
        e.HasIndex(x => new { x.UserId, x.ClientRequestId }).IsUnique();
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
        e.Property(x => x.TopicId);
        e.Property(x => x.ClientRequestId).HasMaxLength(100);

        e.HasOne(x => x.Topic)
         .WithMany()
         .HasForeignKey(x => x.TopicId)
         .OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.UserId);
        e.HasIndex(x => new { x.UserId, x.Date });
        e.HasIndex(x => new { x.UserId, x.TopicId });
        e.HasIndex(x => x.ExamType);
        e.HasIndex(x => new { x.UserId, x.ClientRequestId }).IsUnique();
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

    // ---------- UserGoal ----------
    modelBuilder.Entity<UserGoal>(e =>
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();

        e.Property(x => x.TargetUniversity)
            .HasMaxLength(200)
            .IsRequired();
        e.Property(x => x.TargetDepartment)
            .HasMaxLength(200)
            .IsRequired();
        e.Property(x => x.TargetTytNet).HasPrecision(5, 2);
        e.Property(x => x.TargetAytNet).HasPrecision(5, 2);
        e.Property(x => x.CreatedAt).IsRequired();

        e.HasOne(x => x.User)
            .WithMany(u => u.UserGoals)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.UserId, x.CreatedAt });

        e.Property(x => x.DailyAvailableMinutes)
            .HasDefaultValue(120)
            .IsRequired();
    });

    // ---------- ScheduleTask ----------
    modelBuilder.Entity<ScheduleTask>(e =>
    {
        e.HasKey(x => x.Id);
        MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(e.Property(x => x.Id));

        e.Property(x => x.TaskDate).IsRequired();
        e.Property(x => x.DurationMinutes).IsRequired();
        e.Property(x => x.IsRecoveryTask).HasDefaultValue(false).IsRequired();
        e.Property(x => x.CreatedAt).IsRequired();
        e.Property(x => x.UpdatedAt).IsRequired();

        e.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        e.Property(x => x.TaskType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(TaskType.Study)
            .IsRequired();

        e.Property(x => x.Reason).HasMaxLength(500);

        e.HasOne(x => x.User)
            .WithMany(u => u.ScheduleTasks)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.UserId, x.TaskDate });
        e.HasIndex(x => new { x.UserId, x.Status });
        e.HasIndex(x => new { x.UserId, x.TaskDate, x.TaskType });
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

    modelBuilder.Entity<CommandExecution>(e =>
    {
        e.HasKey(x => x.Id);
        MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(e.Property(x => x.Id));
        e.Property(x => x.CommandKey).HasMaxLength(120).IsRequired();
        e.Property(x => x.Operation).HasMaxLength(100).IsRequired();
        e.Property(x => x.Status).HasConversion<int>().IsRequired();
        e.Property(x => x.ResponseHash).HasMaxLength(128);
        e.HasIndex(x => new { x.UserId, x.Operation, x.CommandKey }).IsUnique();
        e.HasIndex(x => new { x.UserId, x.Operation, x.CreatedAt });
    });

    modelBuilder.Entity<UserNotificationLog>(e =>
    {
        e.HasKey(x => x.Id);
        MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(e.Property(x => x.Id));
        e.Property(x => x.NotificationType).HasMaxLength(80).IsRequired();
        e.Property(x => x.Message).HasMaxLength(300).IsRequired();
        e.Property(x => x.PayloadJson).HasMaxLength(3000).IsRequired();
        e.HasIndex(x => new { x.UserId, x.TargetDate, x.NotificationType }).IsUnique();
    });

    modelBuilder.Entity<UserPlannerChurnEvent>(e =>
    {
        e.HasKey(x => x.Id);
        MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(e.Property(x => x.Id));
        e.Property(x => x.ReasonCode).HasConversion<int>().IsRequired();
        e.HasIndex(x => new { x.UserId, x.TriggerDate });
        e.HasIndex(x => new { x.TriggerDate, x.ReasonCode });
        e.HasIndex(x => new { x.UserId, x.WeekStart, x.ReasonCode }).IsUnique();
    });

    modelBuilder.Entity<PlannerDecisionLog>(e =>
    {
        e.HasKey(x => x.Id);
        MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(e.Property(x => x.Id));
        e.Property(x => x.Status).HasConversion<int>().IsRequired();
        e.Property(x => x.ReasonCode).HasConversion<int>().IsRequired();
        e.Property(x => x.QualityBand).HasConversion<int?>();
        e.Property(x => x.BreakdownJson).HasMaxLength(8000).IsRequired();
        e.Property(x => x.CorrelationId).HasMaxLength(100);
        e.Property(x => x.IdempotencyKey).HasMaxLength(120);
        e.HasIndex(x => new { x.UserId, x.CreatedAt });
        e.HasIndex(x => x.WeekStart);
        e.HasIndex(x => x.ReasonCode);
    });

    modelBuilder.Entity<FeatureFlag>(e =>
    {
        e.HasKey(x => x.Key);
        e.Property(x => x.Key).HasMaxLength(120).IsRequired();
        e.Property(x => x.Description).HasMaxLength(500);
        e.Property(x => x.Segment).HasMaxLength(40);
        e.Property(x => x.RolloutPercentage).HasDefaultValue(100).IsRequired();
        e.HasIndex(x => x.Segment);
    });

    modelBuilder.Entity<UserFeatureFlagOverride>(e =>
    {
        e.HasKey(x => x.Id);
        MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(e.Property(x => x.Id));
        e.Property(x => x.FlagKey).HasMaxLength(120).IsRequired();
        e.HasIndex(x => new { x.UserId, x.FlagKey }).IsUnique();
        e.HasIndex(x => x.FlagKey);
    });

    modelBuilder.Entity<UserNotificationPreference>(e =>
    {
        e.HasKey(x => x.UserId);
        e.Property(x => x.UserId).ValueGeneratedNever();
        e.Property(x => x.DailyReminderEnabled).HasDefaultValue(true).IsRequired();
        e.Property(x => x.RecoveryReminderEnabled).HasDefaultValue(true).IsRequired();
        e.Property(x => x.WeeklyReviewEnabled).HasDefaultValue(true).IsRequired();
    });
        }
    }
}

