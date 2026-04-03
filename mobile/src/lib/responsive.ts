// Minimal responsive helpers (RN)
// Amac: Sabit padding/font değerlerini cihaz genişliğine/yüksekliğine göre ölçeklemek.

export function clamp(n: number, min: number, max: number) {
  return Math.min(max, Math.max(min, n));
}

// iPhone SE / 375px civarı gibi baz genişlik varsayımı
export function getScale(width: number, baseWidth = 375) {
  return clamp(width / baseWidth, 0.85, 1.35);
}

// 667px civarı baz yükseklik
export function getVScale(height: number, baseHeight = 667) {
  return clamp(height / baseHeight, 0.85, 1.35);
}

export function rs(size: number, scale: number) {
  return Math.round(size * scale);
}

export function rvs(size: number, vScale: number) {
  return Math.round(size * vScale);
}

