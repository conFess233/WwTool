// 从桌面端与 Core 生成 Web 端资源，保证两端同源：
//   src/UI/Theming/Presets/*.axaml、Accents/*.axaml → src/generated/themes.css（预设 × 强调色）
//   src/UI/Theming/Motion.cs                         → src/generated/motion.css（时长与缓动）
//   src/Core/Local/Language/*.json + src/i18n/*.json  → src/generated/i18n/*.json
//   src/UI/Theming/Icons.axaml 的线条图标             → src/generated/icons.json
//   src/UI/Assets/Images 中的界面图片                 → public/icons/
// 资料图标与资料数据（catalog/）不再复制：浏览器直接从 GitHub 上的资料仓库加载。
//   src/Core/Models/Config/ApiConfig.cs 的协议参数     → src/generated/protocol.json（游戏登录只在浏览器里用）
// 强调色派生规则与 src/UI/Services/ThemeService.cs 的 ApplyAccent / Mix 完全一致。
import { readFileSync, writeFileSync, mkdirSync, readdirSync, copyFileSync, existsSync } from 'node:fs';
import { join, dirname, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const webRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
const repo = join(webRoot, '..');
const theming = join(repo, 'src', 'UI', 'Theming');
const outDir = join(webRoot, 'src', 'generated');
mkdirSync(join(outDir, 'i18n'), { recursive: true });

// ---------- 颜色 ----------
function parseColor(text) {
  const hex = text.trim().replace('#', '');
  if (hex.length === 6) return { a: 255, r: parseInt(hex.slice(0, 2), 16), g: parseInt(hex.slice(2, 4), 16), b: parseInt(hex.slice(4, 6), 16) };
  if (hex.length === 8) return { a: parseInt(hex.slice(0, 2), 16), r: parseInt(hex.slice(2, 4), 16), g: parseInt(hex.slice(4, 6), 16), b: parseInt(hex.slice(6, 8), 16) };
  throw new Error(`无法解析颜色 ${text}`);
}
const css = c => (c.a === 255 ? `rgb(${c.r} ${c.g} ${c.b})` : `rgb(${c.r} ${c.g} ${c.b} / ${+(c.a / 255).toFixed(3)})`);
const mix = (a, b, t) => ({
  a: Math.round(a.a + (b.a - a.a) * t), r: Math.round(a.r + (b.r - a.r) * t),
  g: Math.round(a.g + (b.g - a.g) * t), b: Math.round(a.b + (b.b - a.b) * t),
});
const WHITE = parseColor('#FFFFFF');
const BLACK = parseColor('#000000');

// BackgroundColor → background；CornerRadiusCard → radius-card；ShadowIsland → shadow-island
function cssName(key) {
  let name = key;
  if (name.endsWith('Color')) name = name.slice(0, -5);
  if (name.startsWith('CornerRadius')) name = 'Radius' + name.slice('CornerRadius'.length);
  return '--' + name.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase();
}

function parseShadow(text) {
  return text.split(',').map(part => part.trim().split(/\s+/).map(tok => (tok.startsWith('#') ? css(parseColor(tok)) : /^-?\d+(\.\d+)?$/.test(tok) ? `${tok}px` : tok)).join(' ')).join(', ');
}

function readDictionary(file) {
  const xml = readFileSync(file, 'utf8');
  const entries = [];
  const re = /<(?:sys:)?(\w+)\s+x:Key="(\w+)">([^<]*)<\//g;
  let m;
  while ((m = re.exec(xml))) entries.push({ type: m[1], key: m[2], value: m[3].trim() });
  return entries;
}

function presetBlock(entries) {
  const lines = [];
  for (const { type, key, value } of entries) {
    if (type === 'Color') lines.push(`${cssName(key)}: ${css(parseColor(value))};`);
    else if (type === 'BoxShadows') lines.push(`${cssName(key)}: ${parseShadow(value)};`);
    else if (type === 'CornerRadius') lines.push(`${cssName(key)}: ${value.split(',').map(v => `${v.trim()}px`).join(' ')};`);
    else if (type === 'Double') lines.push(`${cssName(key)}: ${value};`);
  }
  return lines;
}

const presets = readdirSync(join(theming, 'Presets')).filter(f => f.endsWith('.axaml')).map(f => basename(f, '.axaml'));
const accents = readdirSync(join(theming, 'Accents')).filter(f => f.endsWith('.axaml')).map(f => basename(f, '.axaml'));
const accentMeta = {};

let themeCss = '/* 由 scripts/gen-assets.mjs 从 src/UI/Theming 生成，勿手改。 */\n';
const presetMeta = {};
for (const preset of presets) {
  const entries = readDictionary(join(theming, 'Presets', `${preset}.axaml`));
  const dark = entries.find(e => e.key === 'IsDarkTheme')?.value === 'True';
  const value = key => css(parseColor(entries.find(e => e.key === key).value));
  presetMeta[preset] = { dark, background: value('BackgroundColor'), border: value('InputBorderColor'), accent: value('AccentColor') };
  const selector = preset === 'WarmWhite' ? `:root, :root[data-preset="${preset}"]` : `:root[data-preset="${preset}"]`;
  themeCss += `\n${selector} {\n  color-scheme: ${dark ? 'dark' : 'light'};\n  ${presetBlock(entries).join('\n  ')}\n}\n`;

  const surface = parseColor(entries.find(e => e.key === 'SurfaceColor').value);
  for (const accent of accents) {
    const a = Object.fromEntries(readDictionary(join(theming, 'Accents', `${accent}.axaml`)).map(e => [e.key, parseColor(e.value)]));
    const fill = a.AccentColor;
    accentMeta[accent] = css(fill);
    const soft = dark ? a.AccentContainerDarkColor : a.AccentContainerLightColor;
    const vars = {
      AccentColor: fill,
      TextOnAccentColor: a.TextOnAccentColor,
      AccentTextColor: dark ? a.AccentTextDarkColor : a.AccentTextLightColor,
      AccentContainerColor: soft,
      AccentLineColor: mix(soft, fill, 0.45),
      AccentHoverColor: mix(fill, WHITE, 0.1),
      AccentPressedColor: mix(fill, BLACK, 0.1),
      AccentContainerHoverColor: mix(soft, fill, 0.2),
      FocusRingColor: fill,
      ChartPrimaryColor: fill,
      GlowColor: fill,
      HighlightColor: mix(surface, soft, 0.5),
      HighlightStrongColor: mix(surface, soft, 0.35),
    };
    themeCss += `:root[data-preset="${preset}"][data-accent="${accent}"]${preset === 'WarmWhite' ? `, :root:not([data-preset])[data-accent="${accent}"]` : ''} {\n  ${Object.entries(vars).map(([k, v]) => `${cssName(k)}: ${css(v)};`).join('\n  ')}\n}\n`;
  }
}
writeFileSync(join(outDir, 'themes.css'), themeCss);
writeFileSync(join(outDir, 'themes.json'), JSON.stringify({ presets: presetMeta, accents: accentMeta }, null, 2));

// ---------- 动效 ----------
const motionSrc = readFileSync(join(theming, 'Motion.cs'), 'utf8');
let motionCss = '/* 由 scripts/gen-assets.mjs 从 src/UI/Theming/Motion.cs 生成，勿手改。 */\n:root {\n';
for (const [, name, ms] of motionSrc.matchAll(/TimeSpan (\w+) = TimeSpan\.FromMilliseconds\((\d+)\)/g)) {
  motionCss += `  --motion-${name.toLowerCase()}: ${ms}ms;\n`;
}
for (const [, name, args] of motionSrc.matchAll(/Easing (\w+)Easing = new SplineEasing\(([^)]+)\)/g)) {
  motionCss += `  --ease-${name.toLowerCase()}: cubic-bezier(${args});\n`;
}
motionCss += '}\n';
writeFileSync(join(outDir, 'motion.css'), motionCss);

// ---------- 语言 ----------
const coreLang = join(repo, 'src', 'Core', 'Local', 'Language');
const webLang = join(webRoot, 'src', 'i18n');
const languages = readdirSync(coreLang).filter(f => f.endsWith('.json')).map(f => basename(f, '.json'));
const keySets = [];
for (const lang of languages) {
  const core = JSON.parse(readFileSync(join(coreLang, `${lang}.json`), 'utf8'));
  const webFile = join(webLang, `${lang}.json`);
  const web = existsSync(webFile) ? JSON.parse(readFileSync(webFile, 'utf8')) : {};
  for (const k of Object.keys(web)) if (k in core) throw new Error(`Web 文案键 ${k} 与 Core 重复（${lang}）`);
  const merged = { ...core, ...web };
  keySets.push([lang, new Set(Object.keys(merged))]);
  writeFileSync(join(outDir, 'i18n', `${lang}.json`), JSON.stringify(merged));
}
const [baseLang, baseKeys] = keySets[0];
for (const [lang, keys] of keySets.slice(1)) {
  const missing = [...baseKeys].filter(k => !keys.has(k)).concat([...keys].filter(k => !baseKeys.has(k)));
  if (missing.length) throw new Error(`语言 ${lang} 与 ${baseLang} 的键不一致：${missing.slice(0, 10).join(', ')}`);
}

// ---------- 图标 ----------
// ── 线条图标：src/UI/Theming/Icons.axaml 的 StreamGeometry（24×24 视窗、描边绘制），供 StrokeIcon 组件使用 ──
const strokeIcons = {};
for (const m of readFileSync(join(theming, 'Icons.axaml'), 'utf8').matchAll(/<StreamGeometry x:Key="Icon\.([A-Za-z0-9]+)">([^<]+)<\/StreamGeometry>/g)) {
  strokeIcons[m[1]] = m[2].trim();
}
writeFileSync(join(outDir, 'icons.json'), JSON.stringify(strokeIcons));

const iconsOut = join(webRoot, 'public', 'icons');
mkdirSync(iconsOut, { recursive: true });
// 桌面端全部图片素材（png / gif）原样复制，页面按文件名引用
const assetImages = join(repo, 'src', 'UI', 'Assets', 'Images');
for (const file of readdirSync(assetImages).filter(f => /\.(png|gif)$/i.test(f))) {
  copyFileSync(join(assetImages, file), join(iconsOut, file));
}
copyFileSync(join(repo, 'src', 'UI', 'Assets', 'WwTool.ico'), join(webRoot, 'public', 'favicon.ico'));

// ---------- 协议参数 ----------
const apiConfig = readFileSync(join(repo, 'src', 'Core', 'Models', 'Config', 'ApiConfig.cs'), 'utf8');
function classBlock(name) {
  const start = apiConfig.indexOf(`public class ${name}`);
  if (start < 0) throw new Error(`ApiConfig.cs 中找不到 ${name}`);
  const open = apiConfig.indexOf('{', start);
  let depth = 0;
  for (let i = open; i < apiConfig.length; i++) {
    if (apiConfig[i] === '{') depth++;
    else if (apiConfig[i] === '}' && --depth === 0) return apiConfig.slice(open, i);
  }
  throw new Error(`ApiConfig.cs 中 ${name} 不完整`);
}
const props = block => Object.fromEntries([...block.matchAll(/public (?:string|int) (\w+) \{ get; set; \} = "?([^";]*)"?;/g)].map(m => [m[1], m[2]]));
const fixed = props(classBlock('FixedParams'));
const urls = props(classBlock('Urls'));
const protocol = {
  fixed,
  urls: Object.fromEntries(['EmailLoginUrl', 'GenerateUrl', 'GetTokenUrl', 'QueryRoleUrl', 'QueryPlayerInfoUrl'].map(k => {
    if (!urls[k]) throw new Error(`ApiConfig.cs 缺少 ${k}`);
    return [k, urls[k]];
  })),
};
for (const k of ['ClientId', 'ClientSecret', 'ProductKey', 'Platform', 'ProjectId', 'SdkVersion', 'RedirectUri', 'ResponseType', 'GrantType', 'DefaultE', 'LauncherScope', 'LoginProductId', 'AuthProductId', 'EmailLoginChannelId']) {
  if (!fixed[k]) throw new Error(`ApiConfig.cs 的 FixedParams 缺少 ${k}`);
}
const geetest = readFileSync(join(repo, 'src', 'Core', 'Infrastructure', 'GeetestServer.cs'), 'utf8').match(/CAPTCHA_ID = "([0-9a-f]+)"/);
if (!geetest) throw new Error('GeetestServer.cs 中找不到 CAPTCHA_ID');
protocol.captchaId = geetest[1];
writeFileSync(join(outDir, 'protocol.json'), JSON.stringify(protocol, null, 2));

console.log(`生成完成：${presets.length} 套预设 × ${accents.length} 种强调色，${languages.length} 种语言`);
