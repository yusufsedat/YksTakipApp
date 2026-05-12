using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services
{
    public sealed class GoalService : IGoalService
    {
        public const string SkipLimitMessage = "SKIP_LIMIT";
        public const string ActiveGoalExistsMessage = "ACTIVE_GOAL_EXISTS";
        public const string UserNotFoundMessage = "USER_NOT_FOUND";

        private readonly AppDbContext _db;

        public GoalService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<GoalStatusResult?> GetStatusAsync(int userId)
        {
            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
                return null;

            UserGoal? goal = null;
            if (user.ActiveGoalVersionId is { } activeId)
                goal = await _db.UserGoals.AsNoTracking().FirstOrDefaultAsync(g => g.Id == activeId);

            var hasActiveGoal = goal is not null;
            var canSkip = user.SmartOnboardingSkipCount < 2 && !hasActiveGoal;

            return new GoalStatusResult
            {
                HasActiveGoal = hasActiveGoal,
                CanSkip = canSkip,
                CurrentGoal = goal is null ? null : ToSnapshot(goal)
            };
        }

        /// <remarks>EF first-level cache için FindAsync yerine Id ile sorgu (AsNoTracking senaryosu ile tutarlı).</remarks>
        private static UserGoalSnapshot ToSnapshot(UserGoal g) =>
            new()
            {
                Id = g.Id,
                TargetUniversity = g.TargetUniversity,
                TargetDepartment = g.TargetDepartment,
                TargetTytNet = g.TargetTytNet,
                TargetAytNet = g.TargetAytNet,
                DailyAvailableMinutes = g.DailyAvailableMinutes,
                CreatedAt = g.CreatedAt
            };

        public async Task<UserGoalSnapshot> CreateAsync(
            int userId,
            string targetUniversity,
            string targetDepartment,
            decimal? targetTytNet,
            decimal? targetAytNet,
            int dailyAvailableMinutes)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
                throw new InvalidOperationException(UserNotFoundMessage);

            var goal = new UserGoal
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TargetUniversity = targetUniversity,
                TargetDepartment = targetDepartment,
                TargetTytNet = targetTytNet,
                TargetAytNet = targetAytNet,
                DailyAvailableMinutes = dailyAvailableMinutes,
                CreatedAt = DateTime.UtcNow
            };

            _db.UserGoals.Add(goal);
            user.ActiveGoalVersionId = goal.Id;
            await _db.SaveChangesAsync();
            await InvalidateCurrentWeekPlannedAsync(userId);

            return ToSnapshot(goal);
        }

        public async Task<int> SkipAsync(int userId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
                throw new InvalidOperationException(UserNotFoundMessage);

            var activeId = user.ActiveGoalVersionId;
            if (activeId is not null)
            {
                var goalExists = await _db.UserGoals.AnyAsync(g => g.Id == activeId.Value);
                if (goalExists)
                    throw new InvalidOperationException(ActiveGoalExistsMessage);
            }

            if (user.SmartOnboardingSkipCount >= 2)
                throw new InvalidOperationException(SkipLimitMessage);

            user.SmartOnboardingSkipCount++;
            await _db.SaveChangesAsync();
            return user.SmartOnboardingSkipCount;
        }

        private async Task InvalidateCurrentWeekPlannedAsync(int userId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var mondayOffset = ((int)today.DayOfWeek + 6) % 7;
            var weekStart = today.AddDays(-mondayOffset);
            var weekEnd = weekStart.AddDays(6);

            await _db.ScheduleTasks
                .Where(t =>
                    t.UserId == userId
                    && t.TaskDate >= weekStart
                    && t.TaskDate <= weekEnd
                    && t.Status == ScheduleTaskStatus.Planned
                    && t.TaskType != TaskType.DiagnosticTest)
                .ExecuteDeleteAsync();
        }
    }
}
