<script setup lang="ts">
import { useRouter } from 'vue-router';
import { t } from '../i18n';
import Modal from '../components/Modal.vue';
import StrokeIcon from '../components/StrokeIcon.vue';

// 第一次进入网站时弹出的说明：能做什么、数据存在哪里、游戏凭据如何处理、界面操作提示与免责声明。
const emit = defineEmits<{ close: [] }>();
const router = useRouter();
const points = [
  { icon: 'Link', title: 'Web_WelcomeLocalTitle', text: 'Web_WelcomeLocalText' },
  { icon: 'Cloud', title: 'Web_WelcomeCloudTitle', text: 'Web_WelcomeCloudText' },
  { icon: 'Lock', title: 'Web_WelcomeGameTitle', text: 'Web_WelcomeGameText' },
  { icon: 'Plus', title: 'Web_WelcomeUiTitle', text: 'Web_WelcomeUiText' },
];

function privacy() {
  emit('close');
  router.push('/privacy');
}
</script>

<template>
  <Modal :title="t('Web_WelcomeTitle')" :width="580" @close="emit('close')">
    <div class="welcome">
      <div class="hero">
        <span class="logo"><img src="/favicon.ico" alt="" /></span>
        <p class="lead">{{ t('Web_WelcomeLead') }}</p>
      </div>
      <ul class="points">
        <li v-for="p in points" :key="p.title">
          <span class="disc"><StrokeIcon :name="p.icon" :size="18" /></span>
          <div><strong>{{ t(p.title) }}</strong><p>{{ t(p.text) }}</p></div>
        </li>
      </ul>
      <p class="caption disclaimer">{{ t('About_DisclaimerText') }}</p>
    </div>
    <template #actions>
      <button class="btn" @click="privacy">{{ t('Web_Privacy') }}</button>
      <button class="btn primary" autofocus @click="emit('close')">{{ t('Web_WelcomeStart') }}</button>
    </template>
  </Modal>
</template>

<style scoped>
.welcome { display: flex; flex-direction: column; gap: 16px; }
.hero { display: flex; align-items: center; gap: 14px; }
.logo { width: 52px; height: 52px; border-radius: 50%; overflow: hidden; background: #efe7d8; box-shadow: var(--shadow-pill); flex: none; }
.logo img { width: 100%; height: 100%; object-fit: cover; }
.lead { margin: 0; line-height: 1.7; color: var(--text-secondary); }
.points { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 12px; }
.points li { display: grid; grid-template-columns: 38px 1fr; gap: 12px; align-items: start; padding: 12px 14px; border-radius: 10px; background: var(--surface-raised); }
.disc { width: 38px; height: 38px; border-radius: 10px; display: grid; place-items: center; background: var(--accent-container); color: var(--accent-text); }
.points strong { font-size: 14px; }
.points p { margin: 2px 0 0; font-size: 13px; line-height: 1.65; color: var(--text-secondary); }
.disclaimer { margin: 0; line-height: 1.7; }
</style>
