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

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'dashboard', component: Dashboard },
  { path: 'clients', component: ClientsComponent },
  { path: 'impayes', component: ImpayesComponent },
  { path: 'relances', component: RelancesComponent },
  { path: 'generer-token', component: GenererTokenComponent },
  { path: 'fiche/:id', component: FicheClientComponent },
  { path: 'intentions', component: IntentionsComponent },
  { path: 'scoring', component: ScoringComponent },
  { path: 'users', component: UsersComponent },
  { path: '**', redirectTo: 'login' }
];