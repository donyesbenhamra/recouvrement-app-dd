import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../services/toast.service';
import { ApiService } from '../../services/api.service';
import { NotificationService } from '../../services/notification.service';
import { ActivatedRoute } from '@angular/router';
import { RiskService } from '../../services/risk.service';

@Component({
  selector: 'app-scoring',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './scoring.html',
  styleUrl: './scoring.css'
})
export class ScoringComponent implements OnInit {

  isScanning = false;
  scanningStep = '';
  scanningProgress = 0;
  recherche = '';

  dossiers: any[] = [];
  kpis = { dossiersScores: 0, risqueEleve: 0, risqueMoyen: 0, risqueFaible: 0 };
  selectedDossier: any = null;

  constructor(
    private toastService: ToastService,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef,
    private notifService: NotificationService,
    private route: ActivatedRoute,
     private riskService: RiskService
  ) {}

  ngOnInit() {
    this.loadDashboard();
  }

  loadDashboard() {
  this.apiService.getScoringDashboard('Tous', this.recherche, 1).subscribe({
    next: res => {
      const tousLesDossiers = res.items ?? [];
      this.kpis = res.kpis ?? this.kpis;

      const id = this.route.snapshot.queryParamMap.get('dossierId');
      if (id) {
        // ← filtrer pour n'afficher que ce client
        this.dossiers = tousLesDossiers.filter((d: any) => d.idDossier === Number(id));
        const found = this.dossiers[0];
        if (found) {
          this.selectDossier(found);
        }
      } else {
        // page scoring normale — tous les clients
        this.dossiers = tousLesDossiers;
      }

      this.cdr.detectChanges();
    },
    error: (err: any) => {
      console.error('Erreur:', err);
      this.toastService.show('Erreur chargement scoring', 'error');
    }
  });
}
selectedDossierId: number | null = null;
  selectDossier(d: any) {
    this.selectedDossierId = d.idDossier; //
    this.isScanning = true;
    this.scanningProgress = 0;
    this.scanningStep = 'Chargement des données historiques...';
    this.cdr.detectChanges();
   setTimeout(() => { this.scanningProgress = 30; this.scanningStep = 'Analyse des garanties...'; this.cdr.detectChanges(); }, 600);
setTimeout(() => { this.scanningProgress = 60; this.scanningStep = 'Calcul probabilité de défaut...'; this.cdr.detectChanges(); }, 1200);
setTimeout(() => { this.scanningProgress = 90; this.scanningStep = 'Génération recommandations...'; this.cdr.detectChanges(); }, 1800);
    setTimeout(() => {
      this.apiService.getScoringDetails(d.idDossier).subscribe({
        next: details => {
          this.selectedDossier = details;
           console.log('details scoring:', details);

          const dto = {
  montantDu: details.ptsRetard >= 30 ? 50001 : details.ptsRetard >= 20 ? 15000 : 3000,
  echeancesImpayees: details.ptsHistorique >= 25 ? 6 : details.ptsHistorique >= 20 ? 4 : 1,
  joursRetard: details.ptsRetard >= 30 ? 120 : details.ptsRetard >= 20 ? 60 : 15,
  phase: details.detailGarantie === 'Aucune garantie' ? 'contentieux' : 'amiable',
  nombreRelances: details.ptsIntention >= 15 ? 3 : 1,
  intentionsAnnulees: 0
};
        

          this.riskService.calculerScore(dto).subscribe({
            next: risk => {
              this.selectedDossier.recommandation = risk.justification;
              this.isScanning = false;
              this.cdr.detectChanges();
            },
            error: () => {
              this.isScanning = false;
              this.cdr.detectChanges();
            }
          });
        },
        error: () => {
          this.isScanning = false;
          this.toastService.show('Erreur chargement détails', 'error');
        }
      });
    }, 2200);
  }

  exporterExcel() {
    this.toastService.show('Exportation scoring...', 'info');
    setTimeout(() => {
      const headers = ['Client', 'Retard', 'Score', 'Niveau'];
      let csv = '\uFEFF' + headers.join(';') + '\n';
      this.dossiers.forEach(d => {
        csv += `${d.client};${d.retardTexte};${d.scoreTotal};${d.niveau}\n`;
      });
      const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = `STB_Scoring_${new Date().getFullYear()}.csv`;
      document.body.appendChild(a); a.click(); document.body.removeChild(a);
      this.toastService.show('✅ Export réussi', 'success');
    }, 800);
  }

  formatPts(pts: number): string {
    return pts > 0 ? `+${pts}` : pts.toString();
  }

  getPtsColor(pts: number): string {
    if (pts > 20) return '#ef4444';
    if (pts > 0) return '#f59e0b';
    return '#10b981';
  }

  recalculerTout() {
    this.apiService.recalculerTous().subscribe({
      next: res => {
        this.isScanning = false;
        this.toastService.show(res.message, 'success');
        this.loadDashboard();
      },
      error: (err: any) => {
        this.isScanning = false;
        this.toastService.show(err.error?.message || 'Erreur recalcul', 'error');
      }
    });
  }

  recalculerDossier(id: number) {
    this.apiService.recalculerDossier(id).subscribe({
      next: res => this.toastService.show(res.message, 'success'),
      error: (err: any) => this.toastService.show(err.error?.message || 'Erreur recalcul', 'error')
    });
  }
}