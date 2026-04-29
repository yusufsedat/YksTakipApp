import AsyncStorage from '@react-native-async-storage/async-storage';

const STOPWATCH_STATE_KEY = 'study.stopwatch.state.v1';

export type StopwatchState = {
  isRunning: boolean;
  elapsedMs: number;
  startedAtMs: number | null;
  selectedTopicId: number | null;
};

export const DEFAULT_STOPWATCH_STATE: StopwatchState = {
  isRunning: false,
  elapsedMs: 0,
  startedAtMs: null,
  selectedTopicId: null,
};

export async function readStopwatchState(): Promise<StopwatchState> {
  try {
    const raw = await AsyncStorage.getItem(STOPWATCH_STATE_KEY);
    if (!raw) return DEFAULT_STOPWATCH_STATE;
    const parsed = JSON.parse(raw) as Partial<StopwatchState>;
    const isRunning = Boolean(parsed.isRunning);
    const elapsedMs = Number(parsed.elapsedMs);
    const startedAtMs = parsed.startedAtMs == null ? null : Number(parsed.startedAtMs);
    const selectedTopicId = parsed.selectedTopicId == null ? null : Number(parsed.selectedTopicId);
    if (!Number.isFinite(elapsedMs) || (startedAtMs != null && !Number.isFinite(startedAtMs))) {
      return DEFAULT_STOPWATCH_STATE;
    }
    if (selectedTopicId != null && !Number.isFinite(selectedTopicId)) {
      return DEFAULT_STOPWATCH_STATE;
    }
    return {
      isRunning,
      elapsedMs: Math.max(0, elapsedMs),
      startedAtMs,
      selectedTopicId,
    };
  } catch {
    return DEFAULT_STOPWATCH_STATE;
  }
}

export async function writeStopwatchState(next: StopwatchState): Promise<void> {
  await AsyncStorage.setItem(STOPWATCH_STATE_KEY, JSON.stringify(next));
}
