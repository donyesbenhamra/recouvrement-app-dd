import { Routes } from '@angular/router';
import { Dashboard } from './features/dashboard/dashboard';
import { ClientsComponent } from './features/clients/clients';
import { FicheClientComponent } from './features/clients/fiche-client';
import { LoginComponent } from './features/login/login';
import { ImpayesComponent } from './features/impayes/impayes';
import { RelancesComponent } from './features/relances/relances';
import { GenererTokenComponent } from './features/generer-token/generer-token';
import { IntentionsComponent } from './features/intentions/intentions';
import { ScoringComponent } from './features/scoring/scoring';
import { UsersComponent } from './features/users/users';
import { AuthGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'dashboard', component: Dashboard, canActivate: [AuthGuard] },
  { path: 'clients', component: ClientsComponent, canActivate: [AuthGuard] },
  { path: 'impayes', component: ImpayesComponent, canActivate: [AuthGuard] },
  { path: 'relances', component: RelancesComponent, canActivate: [AuthGuard] },
  { path: 'generer-token', component: GenererTokenComponent, canActivate: [AuthGuard] },
  { path: 'fiche/:id', component: FicheClientComponent, canActivate: [AuthGuard] },
  { path: 'dossier/:id', component: FicheClientComponent, canActivate: [AuthGuard] },
  { path: 'intentions', component: IntentionsComponent, canActivate: [AuthGuard] },
  { path: 'scoring', component: ScoringComponent, canActivate: [AuthGuard] },
  { path: 'users', component: UsersComponent, canActivate: [AuthGuard], data: { roles: ['Admin'] } },  // ✅
  { path: '**', redirectTo: 'login' },
];