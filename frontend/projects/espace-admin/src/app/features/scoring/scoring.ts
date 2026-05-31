import { Component, OnInit, ChangeDetectorRef, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../services/toast.service';
import { ApiService } from '../../services/api.service';
import { NotificationService } from '../../services/notification.service';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-scoring',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './scoring.html',
  styleUrl: './scoring.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScoringComponent implements OnInit {

  isScanning = false;
  scanningStep = '';
  scanningProgress = 0;
  recherche = '';
 
  dossiers: any[] = [];
  kpis = { dossiersScores: 0, risqueEleve: 0, risqueMoyen: 0, risqueFaible: 0 };
  selectedDossier: any = null;
  selectedDossierId: number | null = null;

  constructor(
    private toastService: ToastService,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef,
    private notifService: NotificationService,
    private route: ActivatedRoute
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
          this.dossiers = tousLesDossiers.filter((d: any) => d.idDossier === Number(id));
          const found = this.dossiers[0];
          if (found) this.selectDossier(found);
        } else {
          this.dossiers = tousLesDossiers;
        }

        this.cdr.markForCheck();
      },
      error: (err: any) => {
        console.error('Erreur:', err);
        this.toastService.show('Erreur chargement scoring', 'error');
      }
    });
  }

  selectDossier(d: any) {
    this.selectedDossierId = d.idDossier;
    this.isScanning = true;
    this.scanningProgress = 0;
    this.scanningStep = 'Chargement des données historiques...';
    this.cdr.markForCheck();

   setTimeout(() => { this.scanningProgress = 30; this.scanningStep = 'Analyse des garanties...';         this.cdr.markForCheck(); }, 200);
setTimeout(() => { this.scanningProgress = 60; this.scanningStep = 'Calcul probabilité de défaut...'; this.cdr.markForCheck(); }, 400);
setTimeout(() => { this.scanningProgress = 90; this.scanningStep = 'Génération recommandations...';   this.cdr.markForCheck(); }, 600);

setTimeout(() => {
  this.apiService.getScoringDetails(d.idDossier).subscribe({
        next: details => {
          this.selectedDossier = details;
          console.log('details scoring:', details);
          this.isScanning = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.isScanning = false;
          this.cdr.markForCheck();
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
    if (pts > 0)  return '#f59e0b';
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
    next: res => {
      this.toastService.show(res.message, 'success');
      // Recharger le dashboard ET les détails du dossier recalculé
      this.loadDashboard();
      if (this.selectedDossierId === id) {
        this.apiService.getScoringDetails(id).subscribe({
          next: details => {
            this.selectedDossier = details;
            this.cdr.markForCheck();
          }
        });
      }
    },
    error: (err: any) => this.toastService.show(err.error?.message || 'Erreur recalcul', 'error')
  });
}
}