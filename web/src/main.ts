import { createApp } from 'vue';
import { createRouter, createWebHistory } from 'vue-router';
import './styles/base.css';
import './theme';
import App from './App.vue';
import { pages } from './pages';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    ...pages.map(p => ({ path: p.path, name: p.key, component: p.component })),
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
});

createApp(App).use(router).mount('#app');
