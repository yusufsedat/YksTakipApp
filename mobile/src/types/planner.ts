export type ScheduleTaskStatus = 'planned' | 'completed' | 'skipped' | 'deferred';

export type TaskType = 'study' | 'review' | 'diagnosticTest';

export type ScheduleTaskDto = {
  id: number;
  topicId: number;
  topicName: string;
  subjectName: string;
  taskDate: string;
  durationMinutes: number;
  status: ScheduleTaskStatus;
  isRecoveryTask: boolean;
  taskType?: TaskType;
  reason?: string | null;
  prerequisiteTopicId?: number | null;
  mainTopicId?: number | null;
};

export type PlanGenerationStatus = 'success' | 'noPlanGenerated';

export type PlanGenerationReasonCode =
  | 'none'
  | 'requiresGoal'
  | 'dailyCapacityTooLow'
  | 'noTopics'
  | 'noRecommendations';

export type PlanGenerationResponse = {
  status: PlanGenerationStatus;
  reasonCode: PlanGenerationReasonCode;
  message?: string | null;
  currentMinutes?: number | null;
  minimumRequiredMinutes?: number | null;
  tasks: ScheduleTaskDto[];
};
