import AsyncStorage from '@react-native-async-storage/async-storage';
import axios from 'axios';

import { apiPost } from './api';

const PENDING_STUDY_TIMES_KEY = 'pending_study_times';

export type PendingStudyTimeItem = {
  durationMinutes: number;
  date: string;
  topicId: number | null;
};

let flushPromise: Promise<number> | null = null;

function sanitizePendingItem(item: Partial<PendingStudyTimeItem>): PendingStudyTimeItem | null {
  const durationMinutes = Number(item.durationMinutes);
  const date = typeof item.date === 'string' ? item.date : '';
  const topicId = item.topicId == null ? null : Number(item.topicId);
  if (!Number.isFinite(durationMinutes) || durationMinutes < 1 || durationMinutes > 1440) return null;
  if (!date) return null;
  if (topicId != null && !Number.isFinite(topicId)) return null;
  return {
    durationMinutes: Math.round(durationMinutes),
    date,
    topicId,
  };
}

export function isLikelyNetworkError(error: unknown): boolean {
  if (axios.isAxiosError(error)) {
    if (!error.response) return true;
    return error.code === 'ERR_NETWORK' || error.code === 'ECONNABORTED';
  }
  if (error instanceof Error) {
    const msg = error.message.toLowerCase();
    return msg.includes('network') || msg.includes('timeout') || msg.includes('ağ isteği başarısız');
  }
  return false;
}

async function readPendingStudyTimes(): Promise<PendingStudyTimeItem[]> {
  try {
    const raw = await AsyncStorage.getItem(PENDING_STUDY_TIMES_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as Partial<PendingStudyTimeItem>[];
    if (!Array.isArray(parsed)) return [];
    return parsed.map(sanitizePendingItem).filter((x): x is PendingStudyTimeItem => x != null);
  } catch {
    return [];
  }
}

async function writePendingStudyTimes(items: PendingStudyTimeItem[]): Promise<void> {
  if (items.length === 0) {
    await AsyncStorage.removeItem(PENDING_STUDY_TIMES_KEY);
    return;
  }
  await AsyncStorage.setItem(PENDING_STUDY_TIMES_KEY, JSON.stringify(items));
}

export async function enqueuePendingStudyTime(item: PendingStudyTimeItem): Promise<void> {
  const safeItem = sanitizePendingItem(item);
  if (!safeItem) return;
  const current = await readPendingStudyTimes();
  current.push(safeItem);
  await writePendingStudyTimes(current);
}

export async function flushPendingStudyTimes(): Promise<number> {
  if (flushPromise) return flushPromise;

  flushPromise = (async () => {
    const pending = await readPendingStudyTimes();
    if (pending.length === 0) return 0;

    try {
      await apiPost('/studytime/bulk-create', { items: pending });
      await writePendingStudyTimes([]);
      return pending.length;
    } catch (error) {
      if (!isLikelyNetworkError(error)) {
        // Sunucu reddinde (validation vs) kuyruğun kilitlenmemesi için tek tek dene.
        const remaining: PendingStudyTimeItem[] = [];
        for (const item of pending) {
          try {
            await apiPost('/studytime/create', item);
          } catch (singleError) {
            remaining.push(item);
            if (isLikelyNetworkError(singleError)) break;
          }
        }
        await writePendingStudyTimes(remaining);
        return pending.length - remaining.length;
      }
      return 0;
    } finally {
      flushPromise = null;
    }
  })();

  return flushPromise;
}
