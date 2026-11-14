<script setup lang="ts">
import { computed } from 'vue';
import DOMPurify from 'dompurify';

// 游戏公告正文（Controls/NoticeContent.cs）。正文是库洛下发的 HTML：一律经 DOMPurify 净化，
// 只保留排版、图片与链接；链接在新窗口打开并切断 opener 与来源。
const props = defineProps<{ html: string }>();

const ALLOWED_TAGS = ['p', 'br', 'span', 'strong', 'b', 'em', 'i', 'u', 's', 'a', 'img', 'ul', 'ol', 'li', 'h1', 'h2', 'h3', 'h4', 'table', 'thead', 'tbody', 'tr', 'td', 'th', 'div', 'hr', 'blockquote'];
const ALLOWED_ATTR = ['href', 'src', 'alt', 'title', 'colspan', 'rowspan'];

DOMPurify.addHook('afterSanitizeAttributes', node => {
  if (node.tagName === 'A') {
    const href = node.getAttribute('href') ?? '';
    if (!/^https:\/\//i.test(href)) node.removeAttribute('href');
    node.setAttribute('target', '_blank');
    node.setAttribute('rel', 'noopener noreferrer');
  }
  if (node.tagName === 'IMG') {
    const src = node.getAttribute('src') ?? '';
    if (!/^https:\/\//i.test(src)) node.remove();
    else {
      node.setAttribute('loading', 'lazy');
      node.setAttribute('referrerpolicy', 'no-referrer');
    }
  }
});

const safe = computed(() => DOMPurify.sanitize(props.html, { ALLOWED_TAGS, ALLOWED_ATTR }));
</script>

<template>
  <!-- eslint-disable-next-line vue/no-v-html -- 已经过 DOMPurify 白名单净化 -->
  <div class="notice-content" v-html="safe" />
</template>

<style scoped>
.notice-content { font-size: 13px; line-height: 1.75; color: var(--text-secondary); overflow-wrap: anywhere; user-select: text; }
.notice-content :deep(p) { margin: 0 0 8px; }
.notice-content :deep(img) { max-width: 100%; height: auto; border-radius: 8px; margin: 6px 0; }
.notice-content :deep(a) { color: var(--accent-text); }
.notice-content :deep(table) { border-collapse: collapse; width: 100%; margin: 8px 0; }
.notice-content :deep(td), .notice-content :deep(th) { border: 1px solid var(--border); padding: 4px 8px; }
.notice-content :deep(strong), .notice-content :deep(b) { color: var(--text-primary); }
</style>
