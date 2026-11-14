<script setup lang="ts">
import { ref } from 'vue';
import Modal from '../../components/Modal.vue';
import { t } from '../../i18n';
import { toast } from '../../components/toast';

// 只显示一次的恢复码：注册、用恢复码重设密码、重新生成后都会弹出。
defineProps<{ code: string }>();
const emit = defineEmits<{ close: [] }>();
const copied = ref(false);

async function copy(code: string) {
  try {
    await navigator.clipboard.writeText(code);
    copied.value = true;
  } catch {
    toast(t('Web_CopyFailed'), 'error');
  }
}
</script>

<template>
  <Modal :title="t('Web_RecoveryCodeTitle')" :width="460" @close="emit('close')">
    <p class="caption hint">{{ t('Web_RecoveryCodeHint') }}</p>
    <div class="code mono">{{ code }}</div>
    <div class="dialog-actions">
      <button type="button" class="btn" @click="copy(code)">{{ copied ? t('Web_Copied') : t('Web_CopyRecoveryCode') }}</button>
      <button type="button" class="btn primary" @click="emit('close')">{{ t('Web_RecoveryCodeSaved') }}</button>
    </div>
  </Modal>
</template>

<style scoped>
.hint { margin: 0 0 14px; line-height: 1.7; }
.code {
  padding: 14px; border-radius: var(--radius-control); background: var(--surface-sunken); border: 1px dashed var(--accent-line);
  font-size: 18px; font-weight: 700; letter-spacing: 1px; text-align: center; user-select: all; color: var(--accent-text);
}
.dialog-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 18px; }
</style>
