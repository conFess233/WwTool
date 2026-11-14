import { ref, onMounted, onBeforeUnmount, watchEffect } from 'vue';

// 右下角悬浮按钮（FabMenu）：每个页面登记自己的操作。
// 有 trigger 时按钮展开为「图标 + 文字」并直接执行第一个操作（如抽卡页的「获取记录」），否则为「+」并展开菜单。
export interface FabAction {
  key: string;
  title: string;
  /** StrokeIcon 名称（Icons.axaml 的键）或 /icons/ 下的图片名（带 .png） */
  icon: string;
  run: () => unknown;
  danger?: boolean;
}
export interface FabTrigger { title: string; icon: string }
export interface FabState { actions: FabAction[]; trigger?: FabTrigger; busy?: boolean }

export const fab = ref<FabState | null>(null);

/** 在页面 setup 中调用；传入函数以便文案随语言更新。页面卸载时自动清除。 */
export function useFab(build: () => FabState | null) {
  let stop: (() => void) | undefined;
  onMounted(() => {
    stop = watchEffect(() => (fab.value = build()));
  });
  onBeforeUnmount(() => {
    stop?.();
    fab.value = null;
  });
}
