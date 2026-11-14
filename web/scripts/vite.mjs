// 启动 Vite 的包装脚本：用法 node scripts/vite.mjs [build|dev 的参数…]
// 仓库位于含「#」的目录（如 D:\code\C#\WwTool）时，Vite / Rollup 会把「#」当作 URL 片段，模块路径全部失效。
// 这里在系统临时目录建一个指向 web/ 的目录联接（Windows junction，其他系统为符号链接），从联接路径启动 Vite。
import { spawnSync } from 'node:child_process';
import { existsSync, symlinkSync, realpathSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const webRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
let root = webRoot;

if (webRoot.includes('#')) {
  const id = createHash('sha1').update(webRoot).digest('hex').slice(0, 8);
  const link = join(tmpdir(), `wwtool-web-${id}`);
  if (existsSync(link) && realpathSync(link) !== realpathSync(webRoot)) {
    console.error(`临时联接 ${link} 指向了别的目录，请手动删除后重试。`);
    process.exit(1);
  }
  if (!existsSync(link)) symlinkSync(webRoot, link, 'junction');
  root = link;
}

const vite = join(root, 'node_modules', 'vite', 'bin', 'vite.js');
const result = spawnSync(process.execPath, [vite, ...process.argv.slice(2)], {
  cwd: root,
  stdio: 'inherit',
  env: { ...process.env, WWTOOL_WEB_ROOT: root },
});
process.exit(result.status ?? 1);
