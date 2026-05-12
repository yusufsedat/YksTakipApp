export type UserGoalDto = {
  id: string;
  targetUniversity: string;
  targetDepartment: string;
  targetTytNet: number | null;
  targetAytNet: number | null;
  dailyAvailableMinutes: number;
  createdAt: string;
};

export type GoalStatusResponse = {
  hasActiveGoal: boolean;
  canSkip: boolean;
  currentGoal: UserGoalDto | null;
};

export type CreateUserGoalRequest = {
  targetUniversity: string;
  targetDepartment: string;
  targetTytNet?: number | null;
  targetAytNet?: number | null;
  dailyAvailableMinutes: number;
};

export type SkipGoalResponse = {
  skipCount: number;
};
