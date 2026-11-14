import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import { fileURLToPath } from 'node:url';
import { readFileSync } from 'node:fs';

// 仓库位于含「#」的目录（C#）时 Vite 无法处理模块路径，scripts/vite.mjs 会经无「#」的目录联接启动，
// 并通过 WWTOOL_WEB_ROOT 传入联接路径。构建产物输出到后端 wwwroot，由 src/Web 同源托管；开发时 /api 代理到本机后端。
const realRoot = fileURLToPath(new URL('.', import.meta.url));
const root = process.env.WWTOOL_WEB_ROOT ?? realRoot;
// 版本号与桌面端一致（src/Directory.Build.props）
const props = readFileSync(fileURLToPath(new URL('../src/Directory.Build.props', import.meta.url)), 'utf8');
const version = /<Version>([^<]+)<\/Version>/.exec(props)?.[1] ?? '0.0.0';

export default defineConfig({
  root,
  plugins: [vue()],
  define: { __APP_VERSION__: JSON.stringify(version) },
  resolve: { preserveSymlinks: true },
  build: {
    outDir: fileURLToPath(new URL('../src/Web/wwwroot', import.meta.url)),
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: { '/api': 'http://localhost:5180' },
  },
});
