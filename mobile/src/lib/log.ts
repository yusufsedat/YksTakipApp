/**
 * Merkezi loglama. Uygulama kodunda `console.*` kullanılmaz — yalnızca bu modül (sink).
 * @see .cursor/rules/logging.mdc
 */

const PREFIX = '[YksTakipApp]';

export const LogService = {
  /** Normal akış / debug — yalnızca geliştirme */
  info(message: string, ...args: unknown[]): void {
    if (!__DEV__) return;
    if (args.length) console.log(PREFIX, message, ...args);
    else console.log(PREFIX, message);
  },

  /** Kurtarılabilir sorun — geliştirmede konsola; production’da gürültüyü sınırlamak için şimdilik aynı */
  warn(message: string, ...args: unknown[]): void {
    if (!__DEV__) return;
    if (args.length) console.warn(PREFIX, message, ...args);
    else console.warn(PREFIX, message);
  },

  /** Beklenmeyen durum / hata — konsola yazılır (LogService ≠ kullanıcıya mesaj) */
  error(message: string, ...args: unknown[]): void {
    if (args.length) console.error(PREFIX, message, ...args);
    else console.error(PREFIX, message);
  },
};
