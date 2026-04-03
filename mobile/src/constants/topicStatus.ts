/** Backend `TopicStatus` enum ile aynı sıra (0–3). */
export const TopicStatus = {
  NotStarted: 0,
  InProgress: 1,
  Completed: 2,
  NeedsReview: 3,
} as const;

export type TopicStatusValue = (typeof TopicStatus)[keyof typeof TopicStatus];

export const topicStatusLabel: Record<number, string> = {
  0: 'Başlanmadı',
  1: 'Devam ediyor',
  2: 'Tamamlandı',
  3: 'Tekrar gerekli',
};

export const topicStatusOptions: { value: TopicStatusValue; label: string }[] = [
  { value: TopicStatus.NotStarted, label: topicStatusLabel[0] },
  { value: TopicStatus.InProgress, label: topicStatusLabel[1] },
  { value: TopicStatus.Completed, label: topicStatusLabel[2] },
  { value: TopicStatus.NeedsReview, label: topicStatusLabel[3] },
];
