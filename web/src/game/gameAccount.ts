import { FIXED, URLS, CAPTCHA_ID, deviceNum, encodePassword, sign } from './protocol';

// 游戏账号登录与账号资料查询：全部在浏览器内直连库洛。
// 密码、code、autoToken 用完即弃。返回权限最小的 oauthCode（scope=launcher），以及换攻略站令牌用的 access_token
// （调用方立即交给后端换一次 Guide Token 后丢弃，见 profileSync.ts）。本模块不向本站后端发送任何东西。

export class GameError extends Error {}

async function postForm(url: string, form: Record<string, string>) {
  const res = await fetch(url, { method: 'POST', body: new URLSearchParams(form), credentials: 'omit', referrerPolicy: 'no-referrer' });
  if (!res.ok) throw new GameError(`HTTP ${res.status}`);
  return res.json();
}

async function postJson(url: string, json: unknown) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(json),
    credentials: 'omit',
    referrerPolicy: 'no-referrer',
  });
  if (!res.ok) throw new GameError(`HTTP ${res.status}`);
  return res.json();
}

/** queryPlayerInfo / queryRole 会先返回 "retrying"，与 GetDataService.QueryWithRetryAsync 一样重试。 */
async function postJsonWithRetry(url: string, json: unknown) {
  for (let attempt = 1; attempt <= 5; attempt++) {
    const body = await postJson(url, json);
    if (!String(body?.message ?? '').toLowerCase().includes('retrying')) return body;
    await new Promise(r => setTimeout(r, 1000));
  }
  throw new GameError('查询重试次数已达上限。');
}

declare global {
  interface Window {
    initGeetest4?: (config: Record<string, unknown>, cb: (obj: any) => void) => void;
  }
}

let geetestLoader: Promise<void> | undefined;
function loadGeetest() {
  geetestLoader ??= new Promise((resolve, reject) => {
    if (window.initGeetest4) return resolve();
    const script = document.createElement('script');
    script.src = 'https://static.geetest.com/v4/gt4.js';
    script.onload = () => resolve();
    script.onerror = () => {
      geetestLoader = undefined;
      reject(new GameError('无法加载安全验证组件，请关闭去广告插件后重试。'));
    };
    document.head.append(script);
  });
  return geetestLoader;
}

/** 极验 v4（与桌面端 GeetestServer 同一个 captchaId），以弹窗形式完成验证。 */
async function solveGeetest(): Promise<Record<string, string>> {
  await loadGeetest();
  return new Promise((resolve, reject) => {
    window.initGeetest4!({ captchaId: CAPTCHA_ID, product: 'bind' }, obj => {
      obj.onReady(() => obj.showCaptcha());
      obj.onSuccess(() => resolve(obj.getValidate()));
      obj.onError((err: any) => reject(new GameError(`安全验证失败：${err?.msg ?? ''}`)));
      obj.onClose(() => reject(new GameError('已取消安全验证。')));
    });
  });
}

/** 攻略站登录需要的上下文：cUid / cName 是账号标识，accessToken 是凭据，用完即弃 */
export interface GuideLogin { cUid: string; cName: string; accessToken: string }

/** 邮箱密码登录，返回 oauthCode 与攻略站登录上下文。调用方应在调用后立刻清空密码。 */
export async function loginWithEmail(email: string, password: string, onCaptcha?: () => void): Promise<{ oauthCode: string; guide: GuideLogin }> {
  const dev = deviceNum();
  const req: Record<string, string> = {
    __e__: FIXED.DefaultE, email, client_id: FIXED.ClientId, deviceNum: dev, password: encodePassword(password),
    platform: FIXED.Platform, productId: FIXED.LoginProductId, productKey: FIXED.ProductKey, projectId: FIXED.ProjectId,
    redirect_uri: FIXED.RedirectUri, response_type: FIXED.ResponseType, sdkVersion: FIXED.SdkVersion,
    channelId: FIXED.EmailLoginChannelId, sign: '',
  };
  req.sign = sign(req, FIXED.ClientSecret);

  let res = await postForm(URLS.EmailLoginUrl, req);
  if (res?.codes === 41000) {
    onCaptcha?.();
    const v = await solveGeetest();
    Object.assign(req, {
      geetestCaptchaOutput: v.captcha_output, geetestGenTime: v.gen_time,
      geetestLotNumber: v.lot_number, geetestPassToken: v.pass_token,
    });
    res = await postForm(URLS.EmailLoginUrl, req);
  }
  req.password = '';
  if (res?.codes !== 0 || !res.code) throw new GameError(res?.error_description || `登录失败（${res?.codes ?? '无响应'}）`);
  const code: string = res.code;
  const cUid = String(res.cuid ?? '');
  const cName = String(res.username ?? '');
  res = null; // 丢弃 autoToken、temp_token 等

  const tokenReq: Record<string, string> = {
    client_id: FIXED.ClientId, deviceNum: dev, productId: FIXED.AuthProductId, projectId: FIXED.ProjectId,
    grant_type: FIXED.GrantType, code, client_secret: FIXED.ClientSecret, redirect_uri: FIXED.RedirectUri, sign: '',
  };
  tokenReq.sign = sign(tokenReq, FIXED.ClientSecret);
  let token = await postForm(URLS.GetTokenUrl, tokenReq);
  if (!token?.access_token) throw new GameError('获取授权失败，请重试。');

  const gen = await postForm(URLS.GenerateUrl, {
    client_id: FIXED.ClientId, deviceNum: dev, client_secret: FIXED.ClientSecret, access_token: token.access_token,
    productId: FIXED.AuthProductId, projectId: FIXED.ProjectId, redirect_uri: FIXED.RedirectUri, scope: FIXED.LauncherScope,
  });
  const accessToken: string = token.access_token;
  token = null;
  if (gen?.codes !== 0 || !gen.oauthCode) throw new GameError(gen?.error_description || '生成授权码失败。');
  return { oauthCode: gen.oauthCode as string, guide: { cUid, cName, accessToken } };
}

export interface GameRole {
  uid: string;
  region: string;
  name: string;
  level: number;
  /** queryPlayerInfo 中该大区的原始 JSON 字符串（不含凭据），原样转交后端 */
  playerInfo: string;
}

/** 列出授权下的全部游戏角色（一个邮箱可能有多个大区）。授权失效时抛出 GameError。 */
export async function listRoles(oauthCode: string): Promise<GameRole[]> {
  const info = await postJsonWithRetry(URLS.QueryPlayerInfoUrl, { oauthCode });
  if (info?.code !== 0 || !info.data) throw new GameError(info?.message || '游戏账号授权已失效。');
  return Object.entries(info.data as Record<string, string>).map(([region, json]) => {
    const p = JSON.parse(json);
    return { uid: String(p.roleId), region, name: String(p.roleName ?? ''), level: Number(p.level ?? 0), playerInfo: json };
  });
}

/** 查询某个角色的详细资料，返回该大区的原始 JSON 字符串。 */
export async function queryRoleDetail(oauthCode: string, role: GameRole): Promise<string> {
  const res = await postJsonWithRetry(URLS.QueryRoleUrl, { oauthCode, playerId: Number(role.uid), region: role.region });
  const json = res?.data?.[role.region] ?? (res?.data ? Object.values(res.data)[0] : undefined);
  if (res?.code !== 0 || typeof json !== 'string') throw new GameError(res?.message || '获取账号资料失败。');
  return json;
}
