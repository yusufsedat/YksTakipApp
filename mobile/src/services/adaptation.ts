import { apiGet, apiPost } from '../lib/api';
import type { TopicProgress } from '../types/adaptation';
import type { ScheduleTaskDto } from '../types/planner';

export async function evaluatePerformance(topicId: number, recentExamScorePercent: number): Promise<void> {
  await apiPost<unknown>('/adaptation/evaluate-performance', { topicId, recentExamScorePercent });
}

export type DiagnosticResultPayload = 'passed' | 'failed' | 'skipped';

export async function submitDiagnosticResult(
  taskId: number,
  result: DiagnosticResultPayload
): Promise<{ outcome: string; task: ScheduleTaskDto }> {
  return apiPost<{ outcome: string; task: ScheduleTaskDto }>(
    `/adaptation/diagnostic-tasks/${taskId}/result`,
    { result }
  );
}

export function getTopicProgress(topicId: number): Promise<TopicProgress> {
  return apiGet<TopicProgress>(`/topics/${topicId}/progress`);
}
