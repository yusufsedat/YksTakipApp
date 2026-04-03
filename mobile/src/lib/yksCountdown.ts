/**
 * YKS sınav tarihi ve geri sayım mantığı.
 * Varsayılan tarih yılda bir ÖSYM takvimine göre güncellenmeli (Haziran, TYT günü).
 */

export const YKS_EXAM_DATE_STORAGE_KEY = '@yks_countdown_exam_ymd';

/** İlerleme çubuğu: son 365 günü “yolun tamamı” kabul eder; daha uzun vadede çubuk boş kalır. */
export const COUNTDOWN_PROGRESS_WINDOW_DAYS = 365;

/** yyyy-aa-gg */
export function formatYmd(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

export function parseYmdLocal(ymd: string): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(ymd.trim());
  if (!m) return null;
  const y = Number(m[1]);
  const mo = Number(m[2]) - 1;
  const d = Number(m[3]);
  const dt = new Date(y, mo, d);
  if (dt.getFullYear() !== y || dt.getMonth() !== mo || dt.getDate() !== d) return null;
  return dt;
}

function startOfLocalDay(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}

/**
 * Takvim günü farkı: bugün ile sınav günü (aynı gün = 0).
 */
export function calendarDaysUntil(examDate: Date, now = new Date()): number {
  const a = startOfLocalDay(now).getTime();
  const b = startOfLocalDay(examDate).getTime();
  return Math.round((b - a) / 86400000);
}

/**
 * 0–1: sınav yaklaştıkça dolan çubuk. Sınav geçtiyse 1.
 */
export function countdownProgressRatio(remainingDays: number): number {
  if (remainingDays <= 0) return 1;
  const clamped = Math.min(remainingDays, COUNTDOWN_PROGRESS_WINDOW_DAYS);
  return Math.min(1, Math.max(0, (COUNTDOWN_PROGRESS_WINDOW_DAYS - clamped) / COUNTDOWN_PROGRESS_WINDOW_DAYS));
}

export type CountdownGuidance = {
  headline: string;
  body: string;
};

/**
 * Stres yerine “zamanı yönetme” tonu; kırmızı/alarma dili yok.
 */
export function getCountdownGuidance(remainingDays: number): CountdownGuidance {
  if (remainingDays < 0) {
    return {
      headline: 'Bu sınav dönemi tamamlandı',
      body: 'Yeni bir hedef için sınav tarihini güncelleyebilirsin. Her dönem kendi ritmini ister.',
    };
  }
  if (remainingDays === 0) {
    return {
      headline: 'Bugün sınav günü',
      body: 'Planına güvendiysen zihnin hazırdır. Derin nefes, sade adımlar — soru soru ilerle.',
    };
  }
  if (remainingDays <= 7) {
    return {
      headline: 'Son düzlük — rutini koru',
      body: 'Uyku ve hafif tekrar; yeni zor konu açma. Zihnini taze tutmak da çalışmanın parçası.',
    };
  }
  if (remainingDays <= 30) {
    return {
      headline: 'Son 30 gün: deneme ve pekiştirme',
      body: 'Yeni konu yerine deneme analizi ve eksik kapanışı öne al. Süre ve dikkat kasını güçlendir.',
    };
  }
  if (remainingDays <= 90) {
    return {
      headline: 'Son 90 gün: vites yükseltme zamanı',
      body: 'Konu bitişlerini netleştir, haftalık deneme ritmini sıkılaştır. Küçük hedeflerle momentum kur.',
    };
  }
  if (remainingDays <= 180) {
    return {
      headline: 'Tempolu ve sürdürülebilir',
      body: 'Haftalık programına sadık kal; ara vermek temponu bozmaz, aksine korur.',
    };
  }
  return {
    headline: 'Uzun maraton — düzen seni taşır',
    body: 'Her hafta net bir odak seç; ilerlemeyi küçük adımlarla ölç. Acele yok, istikrar var.',
  };
}

/** ÖSYM duyurusuna göre güncellenir (ay: 0=Ocak). Gelecekteki ilk geçerli tarih. */
const DEFAULT_EXAM_MONTH = 5;
const DEFAULT_EXAM_DAY = 21;

export function getDefaultYksExamDate(reference = new Date()): Date {
  let y = reference.getFullYear();
  let exam = new Date(y, DEFAULT_EXAM_MONTH, DEFAULT_EXAM_DAY);
  const todayStart = startOfLocalDay(reference).getTime();
  while (startOfLocalDay(exam).getTime() < todayStart) {
    y += 1;
    exam = new Date(y, DEFAULT_EXAM_MONTH, DEFAULT_EXAM_DAY);
  }
  return exam;
}
