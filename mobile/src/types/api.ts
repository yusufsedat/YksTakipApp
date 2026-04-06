export type Paginated<T> = {
  items: T[];
  meta: { page: number; pageSize: number; total: number };
};

export type TopicDto = {
  id: number;
  name: string;
  category: string;
  subject?: string;
};

export type UserTopicDto = {
  userId: number;
  topicId: number;
  status: number;
};

export type StudyTimeDto = {
  id: number;
  userId: number;
  date: string;
  durationMinutes: number;
  topicId?: number | null;
  topicName?: string | null;
};

/** Haftalık: dayOfWeek 0=Pazar…6=Cumartesi. Aylık: dayOfMonth 1–31. */
export type ScheduleEntryDto = {
  id: number;
  recurrence: string;
  dayOfWeek: number | null;
  dayOfMonth: number | null;
  startMinute: number;
  endMinute: number;
  title: string;
  topicId?: number | null;
};

export type ScheduleListResponse = { items: ScheduleEntryDto[] };

export type ProblemNoteDto = {
  id: number;
  /** Cloudinary HTTPS URL veya eski kayıtlarda data:/ham base64. */
  imageUrl: string;
  tags: string[];
  solutionLearned: boolean;
  createdAt: string;
};

export type ProblemNoteListResponse = { items: ProblemNoteDto[] };

export type ExamDetailDto = {
  id: number;
  subject: string;
  correct: number;
  wrong: number;
  blank: number;
  net: number;
};

export type ExamDto = {
  id: number;
  examName: string;
  examType: string;
  subject?: string;
  date: string;
  netTyt: number;
  netAyt: number;
  durationMinutes?: number;
  difficulty?: number;
  errorReasons?: string;
  details: ExamDetailDto[];
};

export type StatsSummary = {
  totalMinutesLast7Days: number;
  completedTopics: number;
  avgTyt: number;
  avgAyt: number;
};

export type StatsWeeklyRow = {
  date: string;
  totalMinutes: number;
};

export type StatsProgress = {
  thisWeekMinutes: number;
  lastWeekMinutes: number;
  changePercent: number;
};

export type StatsSubjectWin = {
  category: string;
  subject: string;
  completed: number;
  tracked: number;
  /** Tamamlanan konu adları (Konular listenden) */
  completedTopicNames?: string[];
};

export type StatsWins = {
  examStreakDays: number;
  subjectWins: StatsSubjectWin[];
};

export type ExamStatRow = {
  id: number;
  examName: string;
  date: string;
  examType?: string;
  totalNet: number;
  durationMinutes?: number;
  difficulty?: number;
};

export type NetTrendPoint = {
  date: string;
  totalNet: number;
};

export type TytStats = {
  examCount: number;
  avgNet: number;
  speedMetric: number;
  last5: ExamStatRow[];
  netTrend?: NetTrendPoint[];
  subjectAverages: { subject: string; avgNet: number }[];
};

export type AytStats = {
  examCount: number;
  avgNet: number;
  last5: ExamStatRow[];
  netTrend?: NetTrendPoint[];
  subjectAverages: { subject: string; avgNet: number }[];
};

export type BransSubjectStats = {
  subject: string;
  examCount: number;
  avgNet: number;
  difficultyDistribution: { difficulty: number; count: number }[];
  recentExams: {
    id: number;
    examName: string;
    date: string;
    totalNet: number;
    difficulty?: number;
  }[];
};

export type BransStats = {
  examCount: number;
  subjects: BransSubjectStats[];
  netTrend?: NetTrendPoint[];
};
