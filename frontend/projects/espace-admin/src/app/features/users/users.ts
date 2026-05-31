import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { ToastService } from '../../services/toast.service';
import { AuthService } from '../../services/auth.service';
@Component({
  selector: 'app-users',
  standalone: true,
  imports: [CommonModule, FormsModule, HttpClientModule],
  templateUrl: './users.html',
  styleUrl: './users.css'
})
export class UsersComponent implements OnInit {
  private api = 'http://localhost:5000/api/Utilisateur';

  showModal = false;
  isEditing = false;
  editingIndex: number = -1;

  formData = {
    nom: '',
    email: '',
    role: 'Agent',
    acces: 'Dossiers, Relances',
    statut: 'Actif',
    badge: 'bok',
    date: ''
  };

  users: any[] = [];

  constructor(
    private http: HttpClient,
    private toastService: ToastService,
    private cdr: ChangeDetectorRef,
     private authService: AuthService
  ) {}

  ngOnInit() {
    this.loadUsers();
  }

  // ✅ Méthode centralisée pour récupérer le token
  private getHeaders() {
    const token = localStorage.getItem('token');
    return { Authorization: `Bearer ${token}` };
  }

  loadUsers() {
    this.http.get<any>(`${this.api}/gestion`, { headers: this.getHeaders() }).subscribe({
      next: (res) => {
        setTimeout(() => {
          this.users = res.items.map((u: any) => ({
            id: u.idAgent,
            nom: u.nomComplet,
            email: u.email,
            role: u.role,
            acces: u.niveauAcces ?? 'Dossiers, Relances',
            statut: u.statut,
            badge: u.statut === 'Actif' ? 'bok' : 'bg',
            date: u.derniereConnexion
          }));
          this.cdr.detectChanges();
        }, 0);
      },
      error: (err) => {
        console.log('ERREUR:', err);
        this.toastService.show('Erreur chargement utilisateurs', 'error');
      }
    });
  }

  getActiveUsersCount(): number {
    return this.users.filter(u => u.statut === 'Actif').length;
  }

  getInactiveUsersCount(): number {
    return this.users.filter(u => u.statut === 'Inactif').length;
  }

  ajouter() {
    this.isEditing = false;
    this.formData = { nom: '', email: '', role: 'Agent', acces: 'Dossiers, Relances', statut: 'Actif', badge: 'bok', date: '' };
    this.showModal = true;
  }

  editer(u: any) {
    this.isEditing = true;
    this.editingIndex = this.users.indexOf(u);
    this.formData = { ...u };
    this.showModal = true;
  }

  saveUser() {
    const [nom, ...prenomParts] = this.formData.nom.trim().split(' ');
    const prenom = prenomParts.join(' ');

    if (this.isEditing && this.editingIndex !== -1) {
      const id = this.users[this.editingIndex].id;
      const payload = { nom, prenom, telephone: '', role: this.formData.role, idAgence: null };

      this.http.put(`${this.api}/${id}`, payload, { headers: this.getHeaders() }).subscribe({
        next: () => {
          this.loadUsers();
          this.toastService.show('Utilisateur modifié avec succès', 'success');
          this.closeModal();
        },
        error: (err) => {
          const msg = err.error?.message ?? 'Erreur lors de la modification';
          this.toastService.show(msg, 'error');
        }
      });

    } else {
      const payload = {
        nom, prenom,
        email: this.formData.email,
        telephone: '',
        motDePasse: 'Stb@2026',
        role: this.formData.role,
        idAgence: null
      };

      this.http.post(this.api, payload, { headers: this.getHeaders() }).subscribe({
        next: () => {
          this.loadUsers();
          this.toastService.show('Utilisateur créé avec succès', 'success');
          this.closeModal();
        },
        error: (err) => {
          const msg = err.error?.message ?? 'Erreur lors de la création';
          this.toastService.show(msg, 'error');
        }
      });
    }
  }

  suspendre(u: any) {
    this.http.put(`${this.api}/${u.id}/statut`, {}, { headers: this.getHeaders() }).subscribe({
      next: () => {
        this.loadUsers();
        this.toastService.show(`Statut de ${u.nom} mis à jour`, 'success'); // ✅ 'success' pas 'error'
      },
      error: () => this.toastService.show('Erreur lors de la suspension', 'error')
    });
  }

  closeModal() {
    this.showModal = false;
    this.isEditing = false;
    this.editingIndex = -1;
  }

  closeModalOnBackdrop(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('modal')) {
      this.closeModal();
    }
  }
}