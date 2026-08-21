import { Routes } from '@angular/router';
import { TakeTicket } from './pages/take-ticket/take-ticket';
import { ShowTicket } from './pages/show-ticket/show-ticket';
import { ClearQueue } from './pages/clear-queue/clear-queue';

export const routes: Routes = [
  { path: '', component: TakeTicket },
  { path: 'ticket', component: ShowTicket },
  { path: 'clear', component: ClearQueue },
  { path: '**', redirectTo: '' },
];
