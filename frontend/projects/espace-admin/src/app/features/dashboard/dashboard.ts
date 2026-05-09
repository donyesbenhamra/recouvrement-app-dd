import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';


interface Dossier {
  id: number;
  client: string;
  type: string;
  montant: string;
  score: number;
  risque: string;
  badgeClass: string;
  niveau: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard {
  constructor(private router: Router, private apiService: ApiService, private cdr: ChangeDetectorRef) { }

  activeFilter = 'tous';
  stats = { totalImpaye: 0, dossiersActifs: 0, dossiersUrgents: 0 };
  dossiers: Dossier[] = [];

  ngOnInit() {
    this.loadStats();
    this.loadDossiers();
  }

  loadStats() {
  this.apiService.getStats().subscribe(res => {
    this.stats = {
      totalImpaye: res.montantTotalImpaye,
      dossiersActifs: res.dossiersActifs,
      dossiersUrgents: res.intentionsATraiter
    };
    this.cdr.detectChanges(); // ← ajouter
  });
}
traiter(id: number) {
  this.router.navigate(['/scoring'], { queryParams: { dossierId: id } });
}
  loadDossiers() {
    this.apiService.getDossiers().subscribe(res => {
      this.dossiers = res.map(d => ({
        id: d.idDossier,
        client: d.client,
        type: d.typeCredit,
        montant: `${d.montant.toLocaleString()} DT`,
        score: d.score,
        risque: d.risque,
        badgeClass: d.risque === 'Élevé' ? 'bd2' : (d.risque === 'Moyen' ? 'bw' : 'bok'),
        niveau: d.risque === 'Élevé' ? 'eleve' : (d.risque === 'Moyen' ? 'moyen' : 'faible')
      }));
      this.cdr.detectChanges();
    });
  }

  chartData = [
    { month: 'Oct', height: '52%' },
    { month: 'Nov', height: '63%' },
    { month: 'Déc', height: '70%' },
    { month: 'Jan', height: '58%' },
    { month: 'Fév', height: '79%' },
    { month: 'Mar', height: '88%' },
  ];

  get filteredDossiers(): Dossier[] {
    if (this.activeFilter === 'tous') return this.dossiers;
    return this.dossiers.filter(d => d.niveau === this.activeFilter);
  }


  goClients() {
    this.router.navigate(['/clients']);
  }

  setFilter(filter: string, event: MouseEvent | null) {
    this.activeFilter = filter;
    const btns = document.querySelectorAll('.fb-btn');
    btns.forEach(b => b.classList.remove('active'));

    if (event) {
      (event.target as HTMLElement).classList.add('active');
    } else {
      btns.forEach(b => {
        if (b.textContent?.toLowerCase().includes(filter === 'eleve' ? 'élevé' : (filter === 'tous' ? 'tous' : filter))) {
          b.classList.add('active');
        }
      });
    }
  }

  filterByKpi(type: string) {
    if (type === 'urgents') this.setFilter('eleve', null);
    if (type === 'intentions') this.router.navigate(['/intentions']);
    if (type === 'impayes') this.router.navigate(['/impayes']);
    if (type === 'tous') this.setFilter('tous', null);
  }
}
