/** API: JsonStringEnumConverter (camelCase) */
export type RecommendationType = 'topicStudy' | 'review' | 'practice';

export interface TopicPriority {
  topicId: number;
  topicName: string;
  subjectName: string;
  priorityScore: number;
  reason: string;
  recommendationType: RecommendationType;
}
