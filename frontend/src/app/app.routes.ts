import { Routes } from '@angular/router';
import { FormulaireComponent } from './pages/formulaire/formulaire';
import { ConfirmationComponent } from './pages/confirmation/confirmation';
import { TokenInvalideComponent } from './pages/token-invalide/token-invalide';
import { DossiersComponent } from './pages/dossiers/dossiers';
import { tokenGuard } from './guards/token-guard';



export const routes: Routes = [
  {
    path: 'client/:token',
    component: DossiersComponent,
    canActivate: [tokenGuard]  // ← manquait
  },
  { path: 'formulaire/:token/:idDossier', component: FormulaireComponent },
  { path: 'confirmation', component: ConfirmationComponent },
  { path: 'token-invalide', component: TokenInvalideComponent },

  { path: '', redirectTo: 'token-invalide', pathMatch: 'full' }, // ← plus 'client/test'
  { path: '**', redirectTo: 'token-invalide' },
]