import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../services/toast.service';
import { ApiService } from '../../services/api.service';
import { NotificationService } from '../../services/notification.service';


@Component({
  selector: 'app-intentions',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './intentions.html',
  styleUrl: './intentions.css'
})
export class IntentionsComponent implements OnInit {

  filterType = 'Tous';
  filterStatut = 'Tous';
  intentions: any[] = [];
  kpis = { totalRecues: 0, nonTraitees: 0, paiementImmediat: 0, reclamations: 0 };

  constructor(
    private toastService: ToastService,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef,
     private notifService: NotificationService ,
  ) {}

  ngOnInit() {
    this.loadDashboard();
  }

loadDashboard() {
  this.apiService.getIntentionsDashboard(this.filterType, this.filterStatut).subscribe({
    next: res => {
      this.intentions = res.items;
      this.kpis = res.kpis;
      this.cdr.detectChanges();
    },
    error: err => this.toastService.show(err.error?.message || 'Erreur chargement intentions', 'error')
  });
}
traiter(intention: any, decision: string) {
  this.apiService.decisionIntention(intention.idIntention, decision).subscribe({
    next: res => {
      this.toastService.show(res.message, 'success');
      this.loadDashboard();
      this.notifService.refresh(); // ← ajouter
    },
    error: err => this.toastService.show(err.error?.message || 'Erreur', 'error')
  });
}

  actualiser() {
    this.loadDashboard();
    this.toastService.show('Actualisation en cours...', 'info');
  }

  exporterExcel() {
    this.toastService.show('Exportation...', 'info');
    setTimeout(() => {
      const headers = ['Client', 'Type', 'Commentaire', 'Statut', 'Confiance IA', 'Date'];
      let csv = '\uFEFF' + headers.join(';') + '\n';
      this.intentions.forEach(i => {
        csv += `${i.client};${i.typeIntention};${i.commentaireClient};${i.statut};${i.confianceIa}%;${i.dateSoumission}\n`;
      });
      const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = `STB_Intentions_${new Date().getFullYear()}.csv`;
      document.body.appendChild(a); a.click(); document.body.removeChild(a);
      this.toastService.show('✅ Export réussi', 'success');
    }, 800);
  }
  formatIntention(type: string): string {
  const map: any = {
    'paiement_immediat': 'Paiement immédiat',
    'promesse_paiement': 'Promesse de paiement',
    'paiement_partiel': 'Paiement partiel',
    'demande_consolidation': 'Demande de consolidation',
    'reclamation': 'Réclamation'
  };
  return map[type] || type;
}
}