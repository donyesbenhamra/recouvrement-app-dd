import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastService } from '../../services/toast.service';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-clients',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './clients.html',
  styleUrl: './clients.css',
})
export class ClientsComponent implements OnInit {
  filterStatut = '';
  filterAgence = '';
  filterSearch = '';

  clients: any[] = [];
  stats = { total: 0, montant: 0, contentieux: 0, amiable: 0, regularise: 0 };
  totalPages = 1;
  currentPage = 1;

  selectedClient: any = null;
  selectedDossierDetails: any = null;
  drawerOpen = false;
  // ✅ Après
  newClient: any = {
    client: '',
    telephone: '',
    email: '',
    agence: '',
    typeCredit: '',
    montantDu: 0,
    retard: 0,
    statut: 'Amiable'
  };

  constructor(
    private router: Router,
    private toastService: ToastService,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef,
  ) { }

  ngOnInit() {
    this.loadClients();
  }

  loadClients() {
    this.apiService.getClientsGestion(this.filterStatut, this.filterAgence, this.currentPage).subscribe(res => {
      this.clients = res.items;
       console.log('statuts:', res.items.map((c: any) => c.statut));
      this.totalPages = res.totalPages;
      this.currentPage = res.currentPage;
      this.stats = {
        total: res.kpis.totalClients,
        montant: res.kpis.montantTotalEmprunte,
        contentieux: res.kpis.contentieux,
        amiable: res.kpis.amiable,
        regularise: res.kpis.regularise
      };
      this.cdr.detectChanges();
    });
  }
  

  get filteredClients(): any[] {
    return this.clients.filter(c => {
      const matchSearch = !this.filterSearch ||
        c.client?.toLowerCase().includes(this.filterSearch.toLowerCase());
      return matchSearch;
    });
  }

  traiter(id: number) {
    this.router.navigate(['/clients']);
  }

  voirFiche(client: any) {
    this.selectedClient = client;
    this.drawerOpen = true;
  }

  fermerTiroir() {
    this.drawerOpen = false;
    setTimeout(() => { this.selectedClient = null; }, 300);
  }

  exporterExcel() {
    this.toastService.show("Préparation de l'export Excel...", "info");
    setTimeout(() => {
      const headers = ['N° Dossier', 'Client', 'Téléphone', 'Email', 'Agence STB', 'Type Crédit', 'Montant Dû', 'Retard', 'Statut'];
      let csvContent = "\uFEFF";
      csvContent += headers.join(";") + "\n";
      this.clients.forEach(c => {
        csvContent += `${c.idDossier};${c.client};${c.telephone};${c.email};${c.agence};${c.typeCredit};${c.montantDu};${c.retard}j;${c.statut}\n`;
      });
      const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.setAttribute("href", url);
      link.setAttribute("download", `STB_Liste_Clients_${new Date().getFullYear()}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      this.toastService.show("✅ Fichier Excel téléchargé avec succès !", "success");
    }, 1000);
  }

  showCreateForm = false;
  creerClient() {
    this.newClient = {
      client: '',
      telephone: '',
      email: '',
      agence: '',
       typeEmprunt: '',
      montantDu: 0,
      retard: 0,
      statut: 'Amiable'
    };
    this.showCreateForm = true;
  }


  fermerFormulaire() {
    this.showCreateForm = false;
  }
  actionRelance(type: string) {
  if (!this.selectedClient) return;

  if (type === 'token') {
    this.apiService.envoyerToken(this.selectedClient.idDossier, 'email').subscribe({
      next: (res) => {
        this.toastService.show(`✅ Email envoyé ! Lien : ${res.lienPaiement}`, 'success');
        this.fermerTiroir();
      },
      error: () => {
        this.toastService.show('❌ Erreur lors de l\'envoi du token.', 'error');
      }
    });
  } else if (type === 'sms') {
    this.apiService.envoyerToken(this.selectedClient.idDossier, 'sms').subscribe({
      next: () => {
        this.toastService.show(`✅ SMS de relance envoyé à ${this.selectedClient.client}`, 'success');
        this.fermerTiroir();
      },
      error: () => {
        this.toastService.show('❌ Erreur lors de l\'envoi SMS.', 'error');
      }
    });
  } else if (type === 'email') {
    this.apiService.envoyerToken(this.selectedClient.idDossier, 'email').subscribe({
      next: () => {
        this.toastService.show(`✅ E-mail envoyé à ${this.selectedClient.email}`, 'success');
        this.fermerTiroir();
      },
      error: () => {
        this.toastService.show('❌ Erreur lors de l\'envoi email.', 'error');
      }
    });
  }
}
  sauvegarderClient() {
    if (!this.newClient.client || !this.newClient.telephone) {
      this.toastService.show('⚠️ Veuillez remplir les champs obligatoires.', 'error');
      return;
    }

    this.apiService.createClient(this.newClient).subscribe({
      next: () => {
        this.toastService.show('✅ Dossier créé avec succès !', 'success');
        this.fermerFormulaire();
        this.loadClients();
      },
      error: (err) => {
    const msg = err.error?.message ?? 'Erreur lors de la création du dossier.';
    this.toastService.show(`❌ ${msg}`, 'error');
    console.error(err);
}
    });
  }
  archiver(idDossier: number): void {
  if (!confirm('Confirmer l\'archivage de ce client ?')) return;
  
  this.apiService.archiverClient(idDossier).subscribe({
    next: () => {
      this.toastService.show('✅ Client archivé avec succès.', 'success');
      this.loadClients();
    },
    error: (err) => {
      this.toastService.show(`❌ ${err.error?.message || 'Erreur lors de l\'archivage.'}`, 'error');
    }
  });
}
editClient: any = null;
editDrawerOpen = false;

ouvrirEdit(client: any) {
  this.editClient = { ...client }; // copie pour ne pas modifier l'original
  this.editDrawerOpen = true;
}

fermerEdit() {
  this.editDrawerOpen = false;
  setTimeout(() => { this.editClient = null; }, 300);
}

sauvegarderEdit() {
  this.apiService.updateClient(this.editClient.idDossier, {
    telephone: this.editClient.telephone,
    statut: this.editClient.statut
  }).subscribe({
    next: () => {
      this.toastService.show('✅ Client mis à jour', 'success');
      this.fermerEdit();
      this.loadClients();
    },
    error: (err: any) => {
      this.toastService.show(err.error?.message || '❌ Erreur mise à jour', 'error');
    }
  });
}
  
}