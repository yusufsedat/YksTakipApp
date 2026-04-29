import notifee, { AndroidImportance, AndroidVisibility, EventType } from '@notifee/react-native';
import { Platform } from 'react-native';
import { apiPost } from './api';
import { enqueuePendingStudyTime, isLikelyNetworkError } from './pendingStudyTimes';
import { DEFAULT_STOPWATCH_STATE, readStopwatchState, writeStopwatchState } from './stopwatchState';

const TICK_MS = 1000;
const CHANNEL_ID = 'study-stopwatch-channel';
const NOTIFICATION_ID = 'study-stopwatch-notification';
const ACTION_PAUSE = 'stopwatch-pause';
const ACTION_FINISH = 'stopwatch-finish';

type ServiceState = {
  startedAtMs: number | null;
  baseElapsedMs: number;
};

const state: ServiceState = {
  startedAtMs: null,
  baseElapsedMs: 0,
};

let channelReady = false;
let foregroundRegistered = false;
let foregroundStopResolver: (() => void) | null = null;

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function formatDigital(ms: number): string {
  const totalSec = Math.floor(ms / 1000);
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

function getElapsedMs(): number {
  if (state.startedAtMs == null) return state.baseElapsedMs;
  return state.baseElapsedMs + (Date.now() - state.startedAtMs);
}

async function ensureChannel(): Promise<void> {
  if (Platform.OS !== 'android' || channelReady) return;
  await notifee.createChannel({
    id: CHANNEL_ID,
    name: 'Kronometre',
    importance: AndroidImportance.LOW,
    vibration: false,
    sound: undefined,
  });
  channelReady = true;
}

function ensureForegroundServiceRegistered(): void {
  if (Platform.OS !== 'android' || foregroundRegistered) return;
  notifee.registerForegroundService(() => {
    return new Promise<void>((resolve) => {
      foregroundStopResolver = resolve;
    });
  });
  foregroundRegistered = true;
}

async function updateNotificationContent(elapsedMs: number): Promise<void> {
  if (Platform.OS !== 'android') return;
  await ensureChannel();
  ensureForegroundServiceRegistered();
  await notifee.displayNotification({
    id: NOTIFICATION_ID,
    title: 'Kronometre calisiyor',
    body: `Gecen sure: ${formatDigital(elapsedMs)}`,
    android: {
      channelId: CHANNEL_ID,
      asForegroundService: true,
      ongoing: true,
      onlyAlertOnce: true,
      visibility: AndroidVisibility.PUBLIC,
      pressAction: {
        id: 'open-study',
        launchActivity: 'default',
      },
      actions: [
        {
          title: 'Duraklat',
          pressAction: {
            id: ACTION_PAUSE,
            launchActivity: 'default',
          },
        },
        {
          title: 'Bitir',
          pressAction: {
            id: ACTION_FINISH,
            launchActivity: 'default',
          },
        },
      ],
    },
  });
}

async function runTimerTask(): Promise<void> {
  while (state.startedAtMs !== null) {
    await updateNotificationContent(getElapsedMs());
    await sleep(TICK_MS);
  }
}

export async function startStopwatchForegroundService(initialElapsedMs = 0): Promise<void> {
  if (Platform.OS !== 'android') return;
  await ensureChannel();
  ensureForegroundServiceRegistered();
  state.baseElapsedMs = initialElapsedMs;
  state.startedAtMs = Date.now();
  await updateNotificationContent(getElapsedMs());
  void runTimerTask();
}

export async function pauseStopwatchForegroundService(elapsedMs: number): Promise<void> {
  if (Platform.OS !== 'android') return;
  state.baseElapsedMs = elapsedMs;
  state.startedAtMs = null;
  await stopStopwatchForegroundService();
}

export async function stopStopwatchForegroundService(): Promise<void> {
  if (Platform.OS !== 'android') return;
  state.baseElapsedMs = 0;
  state.startedAtMs = null;
  if (foregroundStopResolver) {
    foregroundStopResolver();
    foregroundStopResolver = null;
  }
  await notifee.stopForegroundService();
  await notifee.cancelNotification(NOTIFICATION_ID);
}

export async function syncStopwatchForegroundNotification(elapsedMs: number): Promise<void> {
  if (Platform.OS !== 'android') return;
  await updateNotificationContent(elapsedMs);
}

export async function requestStopwatchNotificationPermission(): Promise<void> {
  if (Platform.OS !== 'android') return;
  await notifee.requestPermission();
}

async function handleActionPress(actionId: string): Promise<void> {
  const current = await readStopwatchState();
  if (actionId === ACTION_PAUSE) {
    if (!current.isRunning || current.startedAtMs == null) return;
    const elapsedMs = current.elapsedMs + (Date.now() - current.startedAtMs);
    await writeStopwatchState({
      isRunning: false,
      elapsedMs: Math.max(0, elapsedMs),
      startedAtMs: null,
      selectedTopicId: current.selectedTopicId,
    });
    await pauseStopwatchForegroundService(Math.max(0, elapsedMs));
    return;
  }
  if (actionId === ACTION_FINISH) {
    const finalMs = current.isRunning && current.startedAtMs != null
      ? current.elapsedMs + (Date.now() - current.startedAtMs)
      : current.elapsedMs;
    await saveStopwatchToBackend(finalMs, current.selectedTopicId);
    await writeStopwatchState(DEFAULT_STOPWATCH_STATE);
    await stopStopwatchForegroundService();
  }
}

async function saveStopwatchToBackend(finalMs: number, topicId: number | null): Promise<void> {
  if (finalMs < 1000) return;
  const durationMinutes = Math.max(1, Math.round(finalMs / 60000));
  const payload = {
    durationMinutes,
    date: new Date().toISOString(),
    topicId,
  };
  try {
    await apiPost('/studytime/create', payload);
  } catch (error) {
    if (isLikelyNetworkError(error)) {
      await enqueuePendingStudyTime(payload);
    }
    // Bildirim aksiyonu sessizce calisir; hata olursa uygulama akisini bozma.
  }
}

notifee.onBackgroundEvent(async ({ type, detail }) => {
  if (type !== EventType.ACTION_PRESS) return;
  await handleActionPress(detail.pressAction?.id ?? '');
});

notifee.onForegroundEvent(async ({ type, detail }) => {
  if (type !== EventType.ACTION_PRESS) return;
  await handleActionPress(detail.pressAction?.id ?? '');
});
