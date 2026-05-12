import { apiGet } from '../lib/api';
import type { TopicPriority } from '../types/recommendations';

export function getDailyRecommendations(): Promise<TopicPriority[]> {
  return apiGet<TopicPriority[]>('/recommendations/today');
}
