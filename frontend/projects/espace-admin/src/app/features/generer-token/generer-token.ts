import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastService } from '../../services/toast.service';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-generer-token',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './generer-token.html',
  styleUrls: ['./generer-token.css']
})
export class GenererTokenComponent {

  nomCompletClient = '';
  emailClient      = '';
  selectedCanal    = 'E-mail';
  generatedLink    = 'https://recouvrement.stbbank.tn/formulaire/d8f3a9e...';

  clientTrouve:    any     = null;
  clientNonTrouve: boolean = false;
  recherche:       boolean = false;

  errorClient = false;
  errorEmail  = false;
  errorCanal  = false;

  constructor(
    private router:       Router,
    private toastService: ToastService,
    private apiService:   ApiService
  ) {}

  rechercherClient() {
    if (!this.emailClient.trim()) return;

    this.clientTrouve    = null;
    this.clientNonTrouve = false;
    this.recherche       = true;

    this.apiService.getClientsGestion('', '', 1).subscribe({
      next: res => {
        this.recherche = false;
        const found = res.items.find((c: any) =>
          c.email?.toLowerCase() === this.emailClient.trim().toLowerCase()
        );
        if (found) {
          this.clientTrouve    = found;
          this.clientNonTrouve = false;
          this.errorEmail      = false;
          // Auto-remplir nom complet si vide
          if (!this.nomCompletClient && found.client) {
            this.nomCompletClient = found.client;
          }
        } else {
          this.clientTrouve    = null;
          this.clientNonTrouve = true;
        }
      },
      error: () => {
        this.recherche = false;
        this.toastService.show('Erreur lors de la recherche du client.', 'error');
      }
    });
  }

  annuler() {
    this.router.navigate(['/relances']);
  }

  generer() {
    this.errorClient = !this.nomCompletClient.trim();
    this.errorEmail  = !this.emailClient.trim();
    this.errorCanal  = !this.selectedCanal;

    if (this.errorClient || this.errorEmail || this.errorCanal) {
      this.toastService.show('Veuillez remplir tous les champs obligatoires.', 'error');
      return;
    }

    if (!this.clientTrouve) {
      this.toastService.show('Veuillez valider le client via son email.', 'error');
      return;
    }

    const canal = this.selectedCanal === 'E-mail' ? 'email' : 'sms';

    this.apiService.envoyerToken(this.clientTrouve.idDossier, canal).subscribe({
      next: () => {
        this.toastService.show(`✅ Lien envoyé à ${this.emailClient}`, 'success');
        setTimeout(() => this.router.navigate(['/relances']), 1500);
      },
      error: () => this.toastService.show('❌ Erreur lors de l\'envoi.', 'error')
    });
  }
}