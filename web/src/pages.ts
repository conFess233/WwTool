import type { Component } from 'vue';

// 与桌面端 App.axaml.cs 注册的 PageDescriptor 一致：键、标题键、图标；设置前有分隔线。
// inIsland 为 false 的页面（账号数据、详细图表、隐私说明）不在悬浮岛中显示；parent 为页头小字的上级页面。
export interface PageDescriptor {
  key: string;
  path: string;
  titleKey: string;
  icon: string;
  startsGroup?: boolean;
  inIsland?: boolean;
  parent?: string;
  component: () => Promise<Component>;
}

export const pages: PageDescriptor[] = [
  { key: 'home', path: '/', titleKey: 'Nav_Home', icon: 'Home', inIsland: true, component: () => import('./pages/HomePage.vue') },
  { key: 'events', path: '/events', titleKey: 'Nav_Events', icon: 'Calendar', inIsland: true, component: () => import('./pages/events/EventsPage.vue') },
  { key: 'pulls', path: '/pulls', titleKey: 'Nav_Statistics', icon: 'Statistics', inIsland: true, component: () => import('./pages/pulls/PullsPage.vue') },
  { key: 'roles', path: '/roles', titleKey: 'Nav_RoleData', icon: 'Role', inIsland: true, component: () => import('./pages/RolesPage.vue') },
  { key: 'explore', path: '/explore', titleKey: 'Nav_Exploration', icon: 'Chest', inIsland: true, component: () => import('./pages/ExplorePage.vue') },
  { key: 'motor', path: '/motor', titleKey: 'Nav_Motor', icon: 'Motor', inIsland: true, component: () => import('./pages/MotorPage.vue') },
  { key: 'settings', path: '/settings', titleKey: 'Nav_Settings', icon: 'Settings', inIsland: true, startsGroup: true, component: () => import('./pages/SettingsPage.vue') },
  { key: 'account', path: '/account', titleKey: 'Nav_AccountData', icon: 'Account', component: () => import('./pages/account/AccountPage.vue') },
  { key: 'charts', path: '/pulls/charts', titleKey: 'Pulls_DetailedCharts', icon: 'Graph', parent: 'Nav_Statistics', component: () => import('./pages/pulls/ChartsPage.vue') },
  { key: 'privacy', path: '/privacy', titleKey: 'Web_Privacy', icon: 'Settings', component: () => import('./pages/PrivacyPage.vue') },
];

export const islandPages = pages.filter(p => p.inIsland);
