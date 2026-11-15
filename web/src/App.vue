<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import NavIsland from './shell/NavIsland.vue';
import AccountCapsule from './shell/AccountCapsule.vue';
import ToastHost from './components/ToastHost.vue';
import Backdrop from './shell/Backdrop.vue';
import Fab from './shell/Fab.vue';
import { usePeek } from './shell/peek';
import WelcomeDialog from './shell/WelcomeDialog.vue';
import { readPref, writePref } from './prefs';
import { pages, islandPages } from './pages';
import { t } from './i18n';
import { refreshMe } from './account/session';
import { refreshLocal } from './gacha/localGacha';

const route = useRoute();
const router = useRouter();
const current = computed(() => pages.find(p => p.key === route.name) ?? pages[0]);
const title = computed(() => t(current.value.titleKey));
// 页头小字只在子页面显示上级页名（如详细图表 → 抽卡统计）
const parentTitle = computed(() => (current.value.parent ? t(current.value.parent) : ''));
const frame = ref<HTMLElement | null>(null);
usePeek(frame);

// 第一次进入网站时弹出说明（每个浏览器一次），页脚的「使用说明」可以再次打开
const WELCOME_KEY = 'welcome.v1';
const welcome = ref(false);
function closeWelcome() {
  welcome.value = false;
  writePref(WELCOME_KEY, '1');
}
// 悬浮岛选中项：详细图表归属抽卡统计；账号数据、隐私说明不选中任何项
const islandKey = computed(() => (current.value.key === 'charts' ? 'pulls' : current.value.key));

onMounted(() => {
  // 等路由就绪再判断：直接打开隐私说明页时不打扰
  router.isReady().then(() => {
    if (!readPref(WELCOME_KEY) && route.name !== 'privacy') welcome.value = true;
  });
  refreshMe().catch(() => undefined);
  refreshLocal();
});

// 页脚：作者与项目链接
const repo = 'https://github.com/conFess233/WwTool';
const author = { name: '告白', url: 'https://github.com/conFess233' };
const year = new Date().getFullYear();
</script>

<template>
  <a class="skip-link" href="#main">{{ t('Web_SkipToContent') }}</a>
  <Backdrop />
  <div ref="frame" class="frame">
    <!-- 页头：左 Logo 与页名、中悬浮岛、右账号胶囊（对应 MainWindow.axaml 的 Header） -->
    <header class="header">
      <div class="brand">
        <div class="logo"><img src="/favicon.ico" width="44" height="44" alt="" /></div>
        <div class="titles">
          <span v-if="parentTitle" class="parent-title">{{ parentTitle }}</span>
          <div class="title-host">
            <Transition name="title-swap">
              <h1 :key="title" class="page-title">{{ title }}</h1>
            </Transition>
          </div>
        </div>
      </div>
      <NavIsland :pages="islandPages" :current="islandKey" />
      <AccountCapsule />
    </header>

    <!-- 内容区：页面切换（旧页左移淡出、新页从右淡入，320ms） -->
    <main id="main" class="page-host">
      <RouterView v-slot="{ Component, route: r }">
        <Transition name="page-slide">
          <component :is="Component" :key="r.name" class="page" />
        </Transition>
      </RouterView>
    </main>

    <footer class="footer caption">
      <nav class="footer-links" aria-label="WwTool">
        <span>© {{ year }} WwTool</span>
        <span aria-hidden="true">·</span>
        <span>{{ t('Web_Author', '') }}<a :href="author.url" target="_blank" rel="noopener noreferrer">{{ author.name }}</a></span>
        <span aria-hidden="true">·</span>
        <a class="github" :href="repo" target="_blank" rel="noopener noreferrer"><span class="gh-icon" aria-hidden="true" />GitHub</a>
        <span aria-hidden="true">·</span>
        <a :href="`${repo}/blob/master/LICENSE.txt`" target="_blank" rel="noopener noreferrer">{{ t('Web_License') }}</a>
        <span aria-hidden="true">·</span>
        <RouterLink to="/privacy">{{ t('Web_Privacy') }}</RouterLink>
        <span aria-hidden="true">·</span>
        <button class="footer-btn" @click="welcome = true">{{ t('Web_WelcomeReopen') }}</button>
      </nav>
      <p class="disclaimer">{{ t('Web_Disclaimer') }}</p>
    </footer>
  </div>
  <Fab />
  <WelcomeDialog v-if="welcome" @close="closeWelcome" />
  <ToastHost />
</template>

<style scoped>
.frame { padding: var(--page-padding-top) var(--page-padding-x) 0; max-width: 1520px; margin: 0 auto; transition: opacity var(--motion-standard) var(--ease-standard); }
.header { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; height: var(--header-height); }
.brand { display: flex; align-items: center; gap: 12px; min-width: 0; }
.logo { width: 44px; height: 44px; border-radius: 22px; overflow: hidden; background: #efe7d8; box-shadow: var(--shadow-pill); flex: none; }
.logo img { width: 100%; height: 100%; object-fit: cover; }
.titles { display: flex; flex-direction: column; min-width: 0; }
.parent-title { margin-bottom: 2px; }
.title-host { display: grid; height: 29px; }
.title-host > * { grid-area: 1 / 1; white-space: nowrap; }
.footer a { color: inherit; text-decoration: none; }
.footer a:hover, .footer-btn:hover { color: var(--accent-text); text-decoration: underline; }
.footer-btn { padding: 0; border: 0; background: transparent; color: inherit; font: inherit; cursor: pointer; }

.page-host { display: grid; margin-top: 18px; padding-bottom: 26px; }
.page-host > .page { grid-area: 1 / 1; min-width: 0; }
.footer { padding: 18px 0 24px; text-align: center; border-top: 1px solid var(--divider); }
.footer-links { display: flex; flex-wrap: wrap; justify-content: center; align-items: center; gap: 6px 10px; }
.github { display: inline-flex; align-items: center; gap: 5px; }
/* GitHub 标志（Octicons mark-github，MIT），用 mask 跟随文字颜色 */
.gh-icon {
  width: 14px; height: 14px; background: currentColor;
  -webkit-mask: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'%3E%3Cpath d='M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z'/%3E%3C/svg%3E") center / contain no-repeat;
  mask: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'%3E%3Cpath d='M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z'/%3E%3C/svg%3E") center / contain no-repeat;
}
.disclaimer { margin: 6px 0 0; color: var(--text-muted); }

/* 页面切换：旧页向左 24 淡出，新页从右侧 24 淡入，320ms 标准缓动（ShellTransitions.PageSlideTransition） */
.page-slide-enter-active, .page-slide-leave-active { transition: opacity 320ms var(--ease-standard), transform 320ms var(--ease-standard); }
.page-slide-enter-from { opacity: 0; transform: translateX(24px); }
.page-slide-leave-to { opacity: 0; transform: translateX(-24px); }
.page-slide-leave-active { pointer-events: none; }

/* 页名切换：旧标题上移 10 淡出，新标题从下方 10 淡入（TitleSwapTransition） */
.title-swap-enter-active, .title-swap-leave-active { transition: opacity 320ms var(--ease-standard), transform 320ms var(--ease-standard); }
.title-swap-enter-from { opacity: 0; transform: translateY(10px); }
.title-swap-leave-to { opacity: 0; transform: translateY(-10px); }

:global(.reduced-motion) .page-slide-enter-from, :global(.reduced-motion) .page-slide-leave-to,
:global(.reduced-motion) .title-swap-enter-from, :global(.reduced-motion) .title-swap-leave-to { transform: none; }

/* 手机：页头只留 Logo、页名与账号胶囊，悬浮岛移到屏幕底部；页脚给底部导航让出位置 */
@media (max-width: 768px) {
  .header { grid-template-columns: minmax(0, 1fr) auto; gap: 10px; }
  .brand { gap: 10px; }
  .logo { width: 36px; height: 36px; }
  .title-host { height: 24px; }
  .parent-title { margin-bottom: 0; font-size: 11px; }
  .page-host { margin-top: 12px; padding-bottom: 18px; }
  .footer { padding: 14px 0 calc(var(--mobile-nav-space) + 14px); }
  .footer-links { gap: 4px 8px; }
  .page-slide-enter-from { transform: translateX(12px); }
  .page-slide-leave-to { transform: translateX(-12px); }
}
</style>
