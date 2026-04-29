import { Platform } from 'react-native';
import { apiPost } from './api';
import { enqueuePendingStudyTime, isLikelyNetworkError } from './pendingStudyTimes';
import { DEFAULT_STOPWATCH_STATE, readStopwatchState, writeStopwatchState } from './stopwatchState';

const TICK_MS = 1000;
const CHANNEL_ID = 'study-stopwatch-channel';
const NOTIFICATION_ID = 'study-stopwatch-notification';
const ACTION_PAUSE = 'stopwatch-pause';
const ACTION_FINISH = 'stopwatch-finish';
const ANDROID_IMPORTANCE_LOW = 2;
const ANDROID_VISIBILITY_PUBLIC = 1;
const EVENT_ACTION_PRESS = 1;

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
let eventHandlersRegistered = false;

type NotifeeModule = {
  createChannel: (input: {
    id: string;
    name: string;
    importance: number;
    vibration: boolean;
    sound?: string;
  }) => Promise<void>;
  registerForegroundService: (runner: () => Promise<void>) => void;
  displayNotification: (input: {
    id: string;
    title: string;
    body: string;
    android: {
      channelId: string;
      asForegroundService: boolean;
      ongoing: boolean;
      onlyAlertOnce: boolean;
      visibility: number;
      pressAction: { id: string; launchActivity: string };
      actions: Array<{ title: string; pressAction: { id: string; launchActivity: string } }>;
    };
  }) => Promise<void>;
  stopForegroundService: () => Promise<void>;
  cancelNotification: (id: string) => Promise<void>;
  requestPermission: () => Promise<void>;
  onBackgroundEvent: (handler: (event: { type: number; detail: { pressAction?: { id?: string } } }) => Promise<void>) => void;
  onForegroundEvent: (handler: (event: { type: number; detail: { pressAction?: { id?: string } } }) => Promise<void>) => void;
};

let notifeeModule: NotifeeModule | null | undefined;

function getNotifee(): NotifeeModule | null {
  if (Platform.OS !== 'android') return null;
  if (notifeeModule !== undefined) return notifeeModule;
  try {
    // eslint-disable-next-line @typescript-eslint/no-var-requires
    const loaded = require('@notifee/react-native');
    notifeeModule = (loaded?.default ?? loaded) as NotifeeModule;
    return notifeeModule;
  } catch {
    notifeeModule = null;
    return null;
  }
}

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
  const notifee = getNotifee();
  if (!notifee || channelReady) return;
  await notifee.createChannel({
    id: CHANNEL_ID,
    name: 'Kronometre',
    importance: ANDROID_IMPORTANCE_LOW,
    vibration: false,
    sound: undefined,
  });
  channelReady = true;
}

function ensureForegroundServiceRegistered(): void {
  const notifee = getNotifee();
  if (!notifee || foregroundRegistered) return;
  notifee.registerForegroundService(() => {
    return new Promise<void>((resolve) => {
      foregroundStopResolver = resolve;
    });
  });
  foregroundRegistered = true;
}

async function updateNotificationContent(elapsedMs: number): Promise<void> {
  const notifee = getNotifee();
  if (!notifee) return;
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
      visibility: ANDROID_VISIBILITY_PUBLIC,
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
  if (!getNotifee()) return;
  await ensureChannel();
  ensureForegroundServiceRegistered();
  state.baseElapsedMs = initialElapsedMs;
  state.startedAtMs = Date.now();
  await updateNotificationContent(getElapsedMs());
  void runTimerTask();
}

export async function pauseStopwatchForegroundService(elapsedMs: number): Promise<void> {
  if (!getNotifee()) return;
  state.baseElapsedMs = elapsedMs;
  state.startedAtMs = null;
  await stopStopwatchForegroundService();
}

export async function stopStopwatchForegroundService(): Promise<void> {
  const notifee = getNotifee();
  if (!notifee) return;
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
  if (!getNotifee()) return;
  await updateNotificationContent(elapsedMs);
}

export async function requestStopwatchNotificationPermission(): Promise<void> {
  const notifee = getNotifee();
  if (!notifee) return;
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

function registerNotificationEventHandlers(): void {
  const notifee = getNotifee();
  if (!notifee || eventHandlersRegistered) return;
  notifee.onBackgroundEvent(async ({ type, detail }) => {
    if (type !== EVENT_ACTION_PRESS) return;
    await handleActionPress(detail.pressAction?.id ?? '');
  });
  notifee.onForegroundEvent(async ({ type, detail }) => {
    if (type !== EVENT_ACTION_PRESS) return;
    await handleActionPress(detail.pressAction?.id ?? '');
  });
  eventHandlersRegistered = true;
}

registerNotificationEventHandlers();
