import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../services/toast.service';
import { ApiService } from '../../services/api.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-impayes',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './impayes.html',
  styleUrls: ['./impayes.css']
})
export class ImpayesComponent implements OnInit  {
  filterStatut = '';




  impayes: any[] = [];
  kpis = { totalImpaye: 0, interetsDus: 0, totalARecouvrer: 0, dejaRecupere: 0, tauxRecuperation: 0 };

  constructor(private toastService: ToastService, private apiService: ApiService,private cdr: ChangeDetectorRef, private router: Router ) { }

  ngOnInit() {
    this.apiService.getImpayesGestion().subscribe(res => {
      this.impayes = res.items;
      this.kpis = res.kpis;
      this.cdr.detectChanges();
    });
  }

  get filteredImpayes() {
    if (!this.filterStatut) return this.impayes;
    return this.impayes.filter(i => {
      if (this.filterStatut === 'Soldé') return i.principalDu <= 0;
      if (this.filterStatut === 'Sans intérêt') return i.retard < 90 && i.principalDu > 0;
      if (this.filterStatut === 'Avec intérêt >=90j') return i.retard >= 90;
      return true;
    });
  }

  exporterExcel() {
    this.toastService.show("Génération du rapport financier...", "info");

    setTimeout(() => {
      const headers = ['Client', 'Réf', 'Principal Dû', 'Frais', 'Retard', 'Intérêts', 'Total à Régler'];
      let csvContent = "\uFEFF";
      csvContent += headers.join(";") + "\n";

      this.filteredImpayes.forEach(i => {
        csvContent += `${i.nom};${i.ref};${i.principalDu};${i.frais};${i.retard};${i.interets};${i.totalAregler}\n`;
      });

      const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.setAttribute("href", url);
      link.setAttribute("download", `STB_Impayes_Detail_${new Date().getFullYear()}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      this.toastService.show("✅ Rapport financier exporté !", "success");
    }, 1000);
  }

  relancer(i: any) {
    this.toastService.show(`✅ Relance automatique envoyée à ${i.nom}`, "success");
  }
  voir(i: any) {
  this.toastService.show(`Ouverture du dossier #${i.idDossier}...`, "info");
  this.router.navigate(['/fiche', i.idDossier]);

  }

  exporterPDF() {
  this.toastService.show("Génération du document PDF en cours...", "info");
  this.apiService.exportImpayesPdf().subscribe({
    next: (blob: Blob) => {
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `STB_Impayes_${new Date().getFullYear()}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
      this.toastService.show("✅ PDF exporté avec succès !", "success");
    },
    error: () => {
      this.toastService.show("❌ Erreur lors de la génération du PDF.", "error");
    }
  });
}
}
