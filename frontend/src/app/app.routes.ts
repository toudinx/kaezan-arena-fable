import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./shell/shell').then((m) => m.Shell),
    children: [
      { path: '', loadComponent: () => import('./pages/home/home').then((m) => m.HomePage) },
      { path: 'hunt', loadComponent: () => import('./pages/hunt/hunt').then((m) => m.HuntPage) },
      { path: 'hunt/:modeId', loadComponent: () => import('./pages/mode/mode').then((m) => m.ModeSelectPage) },
      { path: 'play/:tier', loadComponent: () => import('./pages/prerun/prerun').then((m) => m.PrerunPage) },
      { path: 'recruit', loadComponent: () => import('./pages/recruit/recruit').then((m) => m.RecruitPage) },
      { path: 'kaelis', loadComponent: () => import('./pages/kaelis/kaelis').then((m) => m.KaelisPage) },
      { path: 'backpack', loadComponent: () => import('./pages/backpack/backpack').then((m) => m.BackpackPage) },
      { path: 'bestiary', loadComponent: () => import('./pages/bestiary/bestiary').then((m) => m.BestiaryPage) },
      { path: 'admin', loadComponent: () => import('./pages/admin/admin').then((m) => m.AdminPage) },
    ],
  },
  { path: 'game/:tier', loadComponent: () => import('./pages/game/game').then((m) => m.GamePage) },
  { path: '**', redirectTo: '' },
];
