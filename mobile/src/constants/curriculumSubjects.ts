/** Müfredat seed ile uyumlu ders sırası (chip ve filtrelerde tutarlı sıra). */

export const TYT_SUBJECT_ORDER = [
  'Türkçe',
  'Matematik',
  'Geometri',
  'Tarih',
  'Coğrafya',
  'Felsefe',
  'Din Kültürü',
  'Fizik',
  'Kimya',
  'Biyoloji',
  'Sosyal Bilimler',
  'Fen Bilimleri',
  'Diğer',
] as const;

export const AYT_SUBJECT_ORDER = [
  'AYT Matematik',
  'AYT Geometri',
  'AYT Fizik',
  'AYT Kimya',
  'AYT Biyoloji',
  'AYT Edebiyat',
  'AYT Tarih',
  'AYT Coğrafya',
  'Matematik',
  'Fizik',
  'Kimya',
  'Biyoloji',
  'Edebiyat',
  'Tarih-1',
  'Tarih-2',
  'Coğrafya-1',
  'Coğrafya-2',
  'Felsefe Grubu',
  'Diğer',
] as const;

export const CURRICULUM_SECTION_TITLE: Record<'TYT' | 'AYT', string> = {
  TYT: '📘 TYT — Temel Yeterlilik',
  AYT: '📕 AYT — Alan Yeterlilik',
};

export function sortSubjectsForCategory(category: 'TYT' | 'AYT', subjects: string[]): string[] {
  const order: readonly string[] = category === 'TYT' ? TYT_SUBJECT_ORDER : AYT_SUBJECT_ORDER;
  const idx = (s: string) => {
    const i = order.indexOf(s);
    return i === -1 ? 999 : i;
  };
  return [...subjects].sort((a, b) => {
    const d = idx(a) - idx(b);
    if (d !== 0) return d;
    return a.localeCompare(b, 'tr');
  });
}
