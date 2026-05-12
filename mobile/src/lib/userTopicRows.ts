import type { TopicDto, UserTopicDto } from '../types/api';

/** API bazen enum'u string (JsonStringEnumConverter) döndürür; UI sayısal 0–3 bekler. */
export function coerceUserTopicStatus(status: unknown): number {
  if (typeof status === 'number' && Number.isFinite(status)) return status;
  if (typeof status === 'string') {
    const byName: Record<string, number> = {
      notStarted: 0,
      inProgress: 1,
      completed: 2,
      needsReview: 3,
      NotStarted: 0,
      InProgress: 1,
      Completed: 2,
      NeedsReview: 3,
    };
    const k = status.trim();
    if (k in byName) return byName[k];
    const n = Number(k);
    if (Number.isFinite(n)) return n;
  }
  return 0;
}

/** Konular ekranıyla aynı: test/deneme adları katalogdan çıkarılır. */
export function isLikelyTestTopicName(name: string): boolean {
  const n = name.trim().toLocaleLowerCase('tr');
  if (!n) return true;
  return n.includes('dadsa') || n.includes('asdf') || n.includes('test') || n.includes('deneme konu');
}

/** Konular sekmesindeki satırlarla aynı kaynak: kullanıcı + temizlenmiş katalog. */
export type UserTopicRow = {
  topicId: number;
  name: string;
  category: string;
  subject: string;
  status: number;
  masteryStatus: string;
  masteryConfidence: number;
  isLocked: boolean;
};

export function mergeUserTopicsWithCatalog(
  catalogItems: TopicDto[],
  userTopics: UserTopicDto[]
): UserTopicRow[] {
  const cleanedCatalog = catalogItems.filter((t) => !isLikelyTestTopicName(t.name));
  const byId = new Map(cleanedCatalog.map((t) => [t.id, t]));
  const merged = userTopics.map((ut) => {
    const t = byId.get(ut.topicId);
    return {
      topicId: ut.topicId,
      name: t?.name ?? `Konu #${ut.topicId}`,
      category: t?.category ?? '—',
      subject: t?.subject?.trim() ? t.subject : '—',
      status: coerceUserTopicStatus(ut.status),
      masteryStatus: typeof ut.masteryStatus === 'string' ? ut.masteryStatus : 'notStarted',
      masteryConfidence: typeof ut.masteryConfidence === 'number' ? ut.masteryConfidence : 0,
      isLocked: ut.isLocked === true,
    };
  });
  merged.sort((a, b) => a.name.localeCompare(b.name, 'tr'));
  return merged;
}
