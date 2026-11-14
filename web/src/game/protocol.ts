import protocol from '../generated/protocol.json';

// 库洛协议的浏览器移植：签名、设备号、密码混淆。与 src/Core/Infrastructure/Crypto.cs 逐字节一致
// （原型阶段已用编译出的 Crypto.cs 对照验证）。固定参数由生成脚本从 Core 的 ApiConfig.cs 读取。

export const FIXED = protocol.fixed;
export const URLS = protocol.urls;
export const CAPTCHA_ID = protocol.captchaId;

/** 大写 UUIDv4，对应 Crypto.GetDeviceNum。 */
export const deviceNum = () => crypto.randomUUID().toUpperCase();

/** 对应 Crypto.EncodePassword：UTF-8 → Base64 → 两轮隔位互换。 */
export function encodePassword(password: string) {
  if (!password) return '';
  let bin = '';
  for (const b of new TextEncoder().encode(password)) bin += String.fromCharCode(b);
  const chars = btoa(bin).split('');
  const n = chars.length;
  for (const offset of [0, 1]) {
    let i = offset;
    while (i + 2 < n) {
      [chars[i], chars[i + 2]] = [chars[i + 2], chars[i]];
      if (i + 6 >= n) break;
      i += 4;
    }
  }
  return chars.join('');
}

/** 对应 Crypto.GenerateSignature：排除 sign 与 geetest*，按序数排序拼接，追加密钥后取 MD5 小写十六进制。 */
export function sign(dict: Record<string, string>, secret: string) {
  const keys = Object.keys(dict)
    .filter(k => k.toLowerCase() !== 'sign' && !k.toLowerCase().startsWith('geetest'))
    .sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
  return md5Hex(new TextEncoder().encode(keys.map(k => `${k}=${dict[k]}`).join('&') + secret));
}

// ---- MD5（RFC 1321）：WebCrypto 不提供 MD5 ----
const S = [7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
  4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21];
const K = Array.from({ length: 64 }, (_, i) => Math.floor(Math.abs(Math.sin(i + 1)) * 2 ** 32) >>> 0);

export function md5Hex(bytes: Uint8Array) {
  const len = bytes.length;
  const total = (((len + 8) >>> 6) + 1) * 64;
  const buf = new Uint8Array(total);
  buf.set(bytes);
  buf[len] = 0x80;
  const view = new DataView(buf.buffer);
  view.setUint32(total - 8, (len * 8) >>> 0, true);
  view.setUint32(total - 4, Math.floor(len / 0x20000000), true);
  let a0 = 0x67452301, b0 = 0xefcdab89, c0 = 0x98badcfe, d0 = 0x10325476;
  const M = new Uint32Array(16);
  for (let off = 0; off < total; off += 64) {
    for (let j = 0; j < 16; j++) M[j] = view.getUint32(off + j * 4, true);
    let A = a0, B = b0, C = c0, D = d0;
    for (let i = 0; i < 64; i++) {
      let F: number, g: number;
      if (i < 16) { F = (B & C) | (~B & D); g = i; }
      else if (i < 32) { F = (D & B) | (~D & C); g = (5 * i + 1) % 16; }
      else if (i < 48) { F = B ^ C ^ D; g = (3 * i + 5) % 16; }
      else { F = C ^ (B | ~D); g = (7 * i) % 16; }
      F = (F + A + K[i] + M[g]) >>> 0;
      A = D; D = C; C = B;
      B = (B + ((F << S[i]) | (F >>> (32 - S[i])))) >>> 0;
    }
    a0 = (a0 + A) >>> 0; b0 = (b0 + B) >>> 0; c0 = (c0 + C) >>> 0; d0 = (d0 + D) >>> 0;
  }
  const out = new DataView(new ArrayBuffer(16));
  [a0, b0, c0, d0].forEach((v, i) => out.setUint32(i * 4, v, true));
  return Array.from(new Uint8Array(out.buffer), b => b.toString(16).padStart(2, '0')).join('');
}
