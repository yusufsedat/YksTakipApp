import { useMemo } from 'react';
import { Pressable, Text, View } from 'react-native';

import { rs } from '../lib/responsive';
import type { ThemeColors } from '../theme/colors';
import type { NetTrendPoint, StatsWeeklyRow } from '../types/api';

function shortWeekdayLabel(isoDate: string): string {
  try {
    const [y, m, d] = isoDate.split('-').map(Number);
    const dt = new Date(y, m - 1, d);
    return dt.toLocaleDateString('tr-TR', { weekday: 'narrow' });
  } catch {
    return '?';
  }
}

export function WeeklyMinutesChart({
  rows,
  colors,
  scale,
}: {
  rows: StatsWeeklyRow[];
  colors: ThemeColors;
  scale: number;
}) {
  const maxM = Math.max(1, ...rows.map((r) => r.totalMinutes));
  return (
    <View style={{ marginTop: rs(8, scale) }}>
      <View style={{ flexDirection: 'row', alignItems: 'flex-end', justifyContent: 'space-between', gap: rs(4, scale) }}>
        {rows.map((r) => {
          const hPct = (r.totalMinutes / maxM) * 100;
          return (
            <View key={r.date} style={{ flex: 1, alignItems: 'center' }}>
              <View
                style={{
                  width: '100%',
                  height: rs(80, scale),
                  backgroundColor: colors.barTrack,
                  borderRadius: rs(6, scale),
                  justifyContent: 'flex-end',
                  overflow: 'hidden',
                }}
              >
                <View
                  style={{
                    height: `${hPct}%`,
                    minHeight: r.totalMinutes > 0 ? rs(4, scale) : 0,
                    backgroundColor: colors.statAccent,
                    borderBottomLeftRadius: rs(6, scale),
                    borderBottomRightRadius: rs(6, scale),
                  }}
                />
              </View>
              <Text
                style={{
                  fontSize: rs(11, scale),
                  color: colors.textMuted,
                  marginTop: rs(6, scale),
                  fontWeight: '600',
                }}
                numberOfLines={1}
              >
                {shortWeekdayLabel(r.date)}
              </Text>
              <Text style={{ fontSize: rs(10, scale), color: colors.textSecondary, marginTop: rs(2, scale) }}>
                {r.totalMinutes > 0 ? `${r.totalMinutes}` : '—'}
              </Text>
            </View>
          );
        })}
      </View>
    </View>
  );
}

/** Kronolojik net sütunları + üstte çizgi hissi için noktalar (SVG’siz, tüm platformlarda tutarlı). */
export function NetTrendColumnChart({
  points,
  colors,
  scale,
}: {
  points: NetTrendPoint[];
  colors: ThemeColors;
  scale: number;
}) {
  const summary = useMemo(() => {
    if (points.length < 2) return null;
    const first = points[0]!.totalNet;
    const last = points[points.length - 1]!.totalNet;
    const d = last - first;
    if (Math.abs(d) < 0.05) return 'Seride net yaklaşık sabit.';
    if (d > 0) return `Son deneme, serideki ilk denemeye göre +${d.toFixed(1)} net (yukarı ivme).`;
    return `Son deneme, serideki ilk denemeye göre ${d.toFixed(1)} net (aşağı yönlü).`;
  }, [points]);

  const maxN = Math.max(0.5, ...points.map((p) => p.totalNet));
  if (points.length === 0) {
    return (
      <Text style={{ color: colors.textMuted, fontSize: rs(13, scale), marginTop: rs(8, scale) }}>Henüz deneme yok.</Text>
    );
  }

  return (
    <View style={{ marginTop: rs(8, scale) }}>
      {summary ? (
        <Text style={{ fontSize: rs(12, scale), color: colors.textSecondary, marginBottom: rs(8, scale), lineHeight: rs(17, scale) }}>
          {summary}
        </Text>
      ) : null}
      <View style={{ flexDirection: 'row', alignItems: 'flex-end', gap: rs(4, scale), minHeight: rs(100, scale) }}>
        {points.map((p, i) => {
          const h = (p.totalNet / maxN) * 100;
          return (
            <View key={`${p.date}-${i}`} style={{ flex: 1, alignItems: 'center' }}>
              <View
                style={{
                  width: '100%',
                  height: rs(88, scale),
                  backgroundColor: colors.barTrack,
                  borderRadius: rs(6, scale),
                  justifyContent: 'flex-end',
                  overflow: 'hidden',
                }}
              >
                <View
                  style={{
                    height: `${h}%`,
                    minHeight: rs(3, scale),
                    backgroundColor: colors.sectionAccent,
                  }}
                />
              </View>
              <Text style={{ fontSize: rs(9, scale), color: colors.textMuted, marginTop: rs(4, scale) }}>{i + 1}</Text>
            </View>
          );
        })}
      </View>
    </View>
  );
}

export function SubjectAverageBar({
  label,
  avgNet,
  targetNet,
  maxNet,
  colors,
  scale,
  onPressTargetHint,
}: {
  label: string;
  avgNet: number;
  targetNet: number;
  maxNet: number;
  colors: ThemeColors;
  scale: number;
  onPressTargetHint?: () => void;
}) {
  const fillPct = Math.min(100, (avgNet / maxNet) * 100);
  const targetPct = Math.min(100, Math.max(0, (targetNet / maxNet) * 100));
  return (
    <View style={{ marginBottom: rs(10, scale) }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: rs(4, scale) }}>
        <Text style={{ flex: 1, fontSize: rs(12, scale), color: colors.textSecondary, fontWeight: '500' }} numberOfLines={1}>
          {label}
        </Text>
        <Text style={{ fontSize: rs(12, scale), fontWeight: '600', color: colors.statAccent }}>{avgNet.toFixed(2)}</Text>
      </View>
      <View style={{ position: 'relative', height: rs(16, scale), justifyContent: 'center' }}>
        <View
          style={{
            height: rs(14, scale),
            backgroundColor: colors.barTrack,
            borderRadius: rs(7, scale),
            overflow: 'hidden',
          }}
        >
          <View
            style={{
              height: '100%',
              width: `${fillPct}%`,
              backgroundColor: colors.barFill,
              borderRadius: rs(7, scale),
            }}
          />
        </View>
        {/* Hedef çizgisi */}
        <View
          pointerEvents="none"
          style={{
            position: 'absolute',
            left: `${targetPct}%`,
            marginLeft: -rs(1, scale),
            top: 0,
            bottom: 0,
            width: rs(3, scale),
            borderRadius: rs(1, scale),
            backgroundColor: colors.primary,
            opacity: 0.85,
          }}
        />
      </View>
      {onPressTargetHint ? (
        <Pressable onPress={onPressTargetHint} style={{ marginTop: rs(4, scale) }}>
          <Text style={{ fontSize: rs(10, scale), color: colors.primary }}>
            Hedef net: {targetNet} (dikey çizgi) · düzenle
          </Text>
        </Pressable>
      ) : (
        <Text style={{ fontSize: rs(10, scale), color: colors.textMuted, marginTop: rs(4, scale) }}>
          Hedef net: {targetNet}
        </Text>
      )}
    </View>
  );
}
