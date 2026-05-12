import { apiGet, apiPatch, apiPost } from '../lib/api';
import type {
  PlanGenerationResponse,
  ScheduleTaskDto,
  ScheduleTaskStatus,
} from '../types/planner';

/**
 * 422 RequiresGoal soft-gate: API hedef yoksa 422 döner ve UI hedef onboarding'ine yönlendirilmeli.
 * Diğer NoPlan reason'lar 200 döner; UI {status, reasonCode, message} alanlarına bakarak boş ekran basar.
 * 422 body'sini de envelope olarak okuyabilmek için acceptStatuses geçilir.
 */
export function generateWeeklyPlan(startDate: string): Promise<PlanGenerationResponse> {
  return apiPost<PlanGenerationResponse>(
    '/planner/generate',
    { startDate },
    { acceptStatuses: [422] }
  );
}

export function getWeeklyTasks(start: string, end: string): Promise<ScheduleTaskDto[]> {
  return apiGet<ScheduleTaskDto[]>(`/planner/weekly?start=${encodeURIComponent(start)}&end=${encodeURIComponent(end)}`);
}

export function updateTaskStatus(taskId: number, status: ScheduleTaskStatus): Promise<ScheduleTaskDto> {
  return apiPatch<ScheduleTaskDto>(`/planner/tasks/${taskId}/status`, { status });
}
