export type TopicProgress = {
  topicId: number;
  masteryStatus: string;
  masteryConfidence: number;
  isLocked: boolean;
  lockReason?: string | null;
};

export type DiagnosticSubmitResult = {
  outcome: string;
};
