// 只保存每位访客自己的界面偏好（主题、语言、减少动画）。存储不可用时（隐私模式等）静默使用默认值。
const PREFIX = 'wwtool.';

export function readPref(key: string): string | null {
  try {
    return localStorage.getItem(PREFIX + key);
  } catch {
    return null;
  }
}

export function writePref(key: string, value: string) {
  try {
    localStorage.setItem(PREFIX + key, value);
  } catch {
    // 存储不可用：本次会话内仍然生效，只是不记住
  }
}
