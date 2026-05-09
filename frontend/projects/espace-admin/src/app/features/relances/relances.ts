import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../services/toast.service';
import { ApiService } from '../../services/api.service';
import { NotificationService } from '../../services/notification.service';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-relances',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './relances.html',
  styleUrls: ['./relances.css']
})
export class RelancesComponent implements OnInit {

  filterCanal = 'Tous';
  filterStatut = 'Tous';
  historique: any[] = [];
  kpis = { totalEnvoyees: 0, enAttenteReponse: 0, formulairesSoumis: 0, tauxReponse: 0 };
  canaux: any = {};

  // ── Modales ──────────────────────────────────────────
  showModalAppel = false;
  showModalSMS   = false;
  showModalEmail = false;

  // ── Formulaires ───────────────────────────────────────
  formAppel  = { idDossier: '', resultat: '', note: '' };
  formSMS    = { idDossier: '', message: '' };
  formEmail  = { idDossier: '', objet: '', contenu: '' };

  // ── Erreurs de validation ─────────────────────────────
  errorsAppel  = { idDossier: false, resultat: false };
  errorsSMS    = { idDossier: false, message: false };
  errorsEmail  = { idDossier: false, objet: false, contenu: false };
  showModalRelance = false;
relanceSelectionnee: any = null;
messageRelance = '';
erreurMessageRelance = false;
  constructor(
    private toastService: ToastService,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef,
    private notifService: NotificationService,
  ) {}

  ngOnInit() {
    this.loadDashboard();
  }

  loadDashboard() {
    this.apiService.getRelancesDashboard(this.filterCanal, this.filterStatut).subscribe({
      next: res => {
        this.historique = res.items;
        this.kpis = res.kpis;
        this.canaux = res.canaux;
        this.cdr.detectChanges();
      },
      error: () => this.toastService.show('Erreur chargement relances', 'error')
    });
  }
ouvrirModalRelance(h: any) {
  this.relanceSelectionnee = h;
  this.messageRelance = '';
  this.erreurMessageRelance = false;
  this.showModalRelance = true;
}

envoyerRelanceMessage() {
  this.erreurMessageRelance = !this.messageRelance.trim();
  if (this.erreurMessageRelance) return;

  this.apiService.envoyerMessageRelance(
    this.relanceSelectionnee.idDossier,
    this.messageRelance
  ).subscribe({
    next: () => {
      this.toastService.show('✅ Message envoyé au client.', 'success');
      this.annulerModal();
      this.loadDashboard();
    },
    error: (err: any) => {
      this.toastService.show(err.error?.message || '❌ Erreur envoi message', 'error');
    }
  });
}

  // ── Ouvrir les modales ────────────────────────────────
  enregistrerAppel() {
    this.formAppel  = { idDossier: '', resultat: '', note: '' };
    this.errorsAppel = { idDossier: false, resultat: false };
    this.showModalAppel = true;
  }

  envoyerSMS() {
    this.formSMS  = { idDossier: '', message: '' };
    this.errorsSMS = { idDossier: false, message: false };
    this.showModalSMS = true;
  }

  envoyerEmail() {
    this.formEmail  = { idDossier: '', objet: '', contenu: '' };
    this.errorsEmail = { idDossier: false, objet: false, contenu: false };
    this.showModalEmail = true;
  }

  // ── Annuler (ferme toutes les modales sans enregistrer) ──
  annulerModal() {
    this.showModalAppel = false;
    this.showModalSMS   = false;
    this.showModalEmail = false;
     this.showModalRelance = false;
  }

  // ── Soumettre Appel ───────────────────────────────────
  soumettreAppel() {
    this.errorsAppel.idDossier = !this.formAppel.idDossier.trim();
    this.errorsAppel.resultat  = !this.formAppel.resultat;
    if (this.errorsAppel.idDossier || this.errorsAppel.resultat) return;

    this.toastService.show(
      `Appel enregistré — Dossier #${this.formAppel.idDossier} (${this.formAppel.resultat})`,
      'success'
    );
    this.annulerModal();
    this.loadDashboard();
  }

  // ── Soumettre SMS ─────────────────────────────────────
  soumettresSMS() {
    this.errorsSMS.idDossier = !this.formSMS.idDossier.trim();
    this.errorsSMS.message   = !this.formSMS.message.trim();
    if (this.errorsSMS.idDossier || this.errorsSMS.message) return;

    this.apiService.envoyerToken(+this.formSMS.idDossier, 'sms').subscribe({
      next: res => {
        this.toastService.show(res.message || 'SMS envoyé avec succès', 'success');
        this.annulerModal();
        this.loadDashboard();
      },
      error: err => this.toastService.show(err.error?.message || 'Erreur envoi SMS', 'error')
    });
  }

  // ── Soumettre Email ───────────────────────────────────
  soumettreEmail() {
    this.errorsEmail.idDossier = !this.formEmail.idDossier.trim();
    this.errorsEmail.objet     = !this.formEmail.objet.trim();
    this.errorsEmail.contenu   = !this.formEmail.contenu.trim();
    if (this.errorsEmail.idDossier || this.errorsEmail.objet || this.errorsEmail.contenu) return;

    this.apiService.envoyerToken(+this.formEmail.idDossier, 'email').subscribe({
      next: res => {
        this.toastService.show(res.message || 'Email envoyé avec succès', 'success');
        this.annulerModal();
        this.loadDashboard();
      },
      error: err => this.toastService.show(err.error?.message || 'Erreur envoi email', 'error')
    });
  }

  // ── Actions tableau ───────────────────────────────────
  relancer(h: any) {
    this.apiService.envoyerToken(h.idDossier, h.canal).subscribe({
      next: res => {
        this.toastService.show(res.message, 'success');
        this.loadDashboard();
      },
      error: err => this.toastService.show(err.error?.message || 'Erreur envoi token', 'error')
    });
  }

  archiver(h: any) {
    this.toastService.show(`Dossier #${h.idDossier} archivé avec succès`, 'success');
  }

  voir(h: any) {
    this.toastService.show(`Consultation dossier #${h.idDossier}...`, 'info');
  }

  exporterExcel() {
    this.toastService.show('Exportation en cours...', 'info');
    setTimeout(() => {
      const headers = ['Client', 'Canal', 'Date', 'Token', 'Statut', 'Réponse'];
      let csv = '\uFEFF' + headers.join(';') + '\n';
      this.historique.forEach(h => {
        csv += `${h.client};${h.canal};${h.dateRelance};${h.token};${h.statut};${h.reponse}\n`;
      });
      const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = `STB_Relances_${new Date().getFullYear()}.csv`;
      document.body.appendChild(a); a.click(); document.body.removeChild(a);
      this.toastService.show('✅ Export réussi', 'success');
    }, 800);
  }

  isExpired(date: any): boolean {
    if (!date) return false;
    return new Date(date) < new Date();
  }
}