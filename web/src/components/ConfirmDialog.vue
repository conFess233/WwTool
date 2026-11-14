<script setup lang="ts">
import { ref } from 'vue';
import Modal from './Modal.vue';
import { t } from '../i18n';

// 危险操作确认：说明后果，确认按钮使用危险语义样式。
const props = defineProps<{ title: string; message: string; confirmText?: string; action: () => Promise<unknown> }>();
const emit = defineEmits<{ close: []; done: [] }>();
const busy = ref(false);
const error = ref('');

async function confirm() {
  busy.value = true;
  error.value = '';
  try {
    await props.action();
    emit('done');
    emit('close');
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <Modal :title="title" :busy="busy" :width="440" @close="emit('close')">
    <p class="message">{{ message }}</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <template #actions>
      <button class="btn" :disabled="busy" @click="emit('close')">{{ t('Common_Cancel') }}</button>
      <button class="btn danger" :disabled="busy" @click="confirm">{{ confirmText ?? t('Web_ConfirmDelete') }}</button>
    </template>
  </Modal>
</template>

<style scoped>
.message { margin: 0; color: var(--text-secondary); line-height: 1.7; }
.error { color: var(--danger); font-size: 13px; margin: 10px 0 0; }
</style>
