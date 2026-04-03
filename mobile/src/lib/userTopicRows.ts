import type { TopicDto, UserTopicDto } from '../types/api';

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
      status: ut.status,
    };
  });
  merged.sort((a, b) => a.name.localeCompare(b.name, 'tr'));
  return merged;
}
