import { apiGet, apiPost } from '../lib/api';
import type { CreateUserGoalRequest, GoalStatusResponse, SkipGoalResponse, UserGoalDto } from '../types/goals';

export function getGoalStatus(): Promise<GoalStatusResponse> {
  return apiGet<GoalStatusResponse>('/users/goals/status');
}

export function createGoal(input: CreateUserGoalRequest): Promise<UserGoalDto> {
  return apiPost<UserGoalDto>('/users/goals', input);
}

export function skipGoal(): Promise<SkipGoalResponse> {
  return apiPost<SkipGoalResponse>('/users/goals/skip', {});
}
