import { Component, OnInit, TemplateRef, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { CommonModule, NgIfContext } from '@angular/common';
import { TokenService, ClientHistoriqueDto, DossierDto } from '../../services/token';
import { RecouvrementService } from '../../services/recouvrement';
import { HttpClient } from '@angular/common/http';

type NewType = TemplateRef<NgIfContext<boolean>>;

@Component({
  selector: 'app-formulaire',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './formulaire.html',
  styleUrls: ['./formulaire.css']
})
export class FormulaireComponent implements OnInit {

  clientData: ClientHistoriqueDto | null = null;
  dossier: DossierDto | null = null;
  token!: string;
  idDossier!: number;
  form!: FormGroup;
  submitted = false;
  loading = false;
  dataLoading = true;

  ongletActif: 'overview' | 'paiements' | 'relances' | 'communications' | 'actions' = 'overview';
  fichierSelectionne: File | null = null;

  // Communications
  messageForm!: FormGroup;
  messageSending = false;

  // Relances
  relanceRepondreIndex: any = null;
  reponseRelanceTexte: string = '';
  relanceSending: boolean = false;
  relanceReponsesEnvoyees: number[] = [];

  // PDF
  pdfLoading: { recu: boolean; historique: boolean } = { recu: false, historique: false };
  pdfError: { recu: string | null; historique: string | null } = { recu: null, historique: null };

  actionsDisponibles = [
    { value: 'paiement_immediat',     label: 'Règlement total' },
    { value: 'paiement_partiel',      label: 'Règlement partiel' },
    { value: 'promesse_paiement',     label: 'Promesse de paiement' },
    { value: 'demande_echeance',      label: "Demande d'échéancier" },
    { value: 'demande_consolidation', label: 'Demande de consolidation' },
  ];

  confirmationBlock: NewType | null | undefined;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private tokenService: TokenService,
    private fb: FormBuilder,
    private recouvrementService: RecouvrementService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  // ============================================================
  // ✅ GETTER CORRIGÉ : contentieux si jours de retard > 180
  // NombreJoursRetard vient du backend qui calcule depuis la
  // DERNIÈRE échéance impayée dépassée (logique corrigée)
  // ============================================================
  get estContentieux(): boolean {
    return (this.dossier?.nombreJoursRetard ?? 0) > 180;
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token')!;
    this.idDossier = Number(this.route.snapshot.paramMap.get('idDossier'));
    this.tokenService.clearClientData();

    this.form = this.fb.group({
      typeIntention:      ['', Validators.required],
      datePaiementPrevue: [null],
      montantPropose:     [null],
      commentaire:        ['', Validators.maxLength(500)],
      email:              ['', Validators.email]
    });

    this.messageForm = this.fb.group({
      message: ['', [Validators.required, Validators.maxLength(500)]]
    });

    const cachedData = this.tokenService.getClientDataFromSession();
    if (cachedData) {
      this.clientData = cachedData;
      this.dossier = cachedData.dossiers.find(d => Number(d.idDossier) === Number(this.idDossier)) || null;
      if (!this.dossier) { this.router.navigate(['/token-invalide']); return; }
      this.dataLoading = false;
      this.verifierDoublon();
    } else {
      this.tokenService.getClientData(this.token).subscribe({
        next: data => {
          this.clientData = data;
          this.tokenService.saveClientData(data);
          this.dossier = data.dossiers.find(d => Number(d.idDossier) === Number(this.idDossier)) || null;
          if (!this.dossier) { this.router.navigate(['/token-invalide']); return; }
          this.dataLoading = false;
          this.verifierDoublon();
          this.cdr.detectChanges();
        },
        error: () => {
          this.dataLoading = false;
          this.router.navigate(['/token-invalide']);
        }
      });
    }

    this.form.get('typeIntention')?.valueChanges.subscribe(val => {
      const dateCtrl   = this.form.get('datePaiementPrevue');
      const montantCtrl = this.form.get('montantPropose');
      dateCtrl?.clearValidators();
      montantCtrl?.clearValidators();
      if (val === 'demande_echeance' || val === 'promesse_paiement') {
        dateCtrl?.setValidators([Validators.required]);
      }
      if (val === 'paiement_partiel') {
        montantCtrl?.setValidators([Validators.required]);
      }
      dateCtrl?.updateValueAndValidity();
      montantCtrl?.updateValueAndValidity();
    });

    this.form.get('email')?.valueChanges.subscribe(val => {
      this.emailInvalide = !!val && this.form.get('email')?.invalid === true;
    });
  }

  private verifierDoublon(): void {
    this.recouvrementService.verifierIntentionExistante(this.token, this.idDossier).subscribe({
      next: (res) => {
        if (res.existe) {
          this.intentionExistante = res;
          this.afficherAvertissementDoublon = true;
        }
      },
      error: (err) => { console.error('verifierDoublon error:', err); }
    });
  }

  annulerIntentionExistante(): void {
    if (!this.intentionExistante?.idIntention) return;
    this.intentionLoading = true;
    this.recouvrementService.annulerIntention(this.token, this.intentionExistante.idIntention).subscribe({
      next: () => {
        this.intentionLoading = false;
        this.intentionExistante = null;
        this.afficherAvertissementDoublon = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Erreur annulation:', err);
        this.intentionLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  modifierIntentionExistante(): void {
    this.afficherAvertissementDoublon = false;
    this.intentionExistante = null;
  }

  onSubmit(): void {
    this.form.get('typeIntention')?.markAsTouched();
    if (!this.form.get('typeIntention')?.value) return;
    if (this.form.invalid) return;

    const email = this.form.get('email')?.value;
    if (email && this.form.get('email')?.invalid) {
      this.emailInvalide = true;
    }

    this.loading = true;
    const payload = {
      idDossier:         this.idDossier,
      typeIntention:     this.form.value.typeIntention,
      commentaire:       this.form.value.commentaire,
      datePaiementPrevue: this.form.value.datePaiementPrevue,
      montantPropose:    this.form.value.montantPropose
    };

    this.recouvrementService.soumettreReponse(payload, this.token).subscribe({
      next: () => {
        this.loading = false;
        this.intentionExistante = null;
        this.afficherAvertissementDoublon = false;
        this.router.navigate(['/confirmation'], {
          state: {
            idDossier:             this.idDossier,
            typeIntention:         this.form.value.typeIntention,
            token:                 this.token,
            accuseParSmsUniquement: this.emailInvalide
          }
        });
      },
      error: () => { this.loading = false; }
    });
  }

  annulerFormulaire(): void {
    this.form.reset();
    this.emailInvalide = false;
  }

  // ── Getters ──────────────────────────────────────────
  get montantPaye(): number {
    return this.dossier?.montantPaye ?? 0;
  }

  get joursRetard(): number {
    return this.dossier?.nombreJoursRetard ?? 0;
  }

  get progressionPaiement(): number {
    if (!this.dossier?.montantInitial || this.dossier.montantInitial === 0) return 0;
    const paye = this.dossier.montantInitial - this.dossier.montantImpaye;
    return Math.round((paye / this.dossier.montantInitial) * 100);
  }

  contacterAgence(): void {
    this.ongletActif = 'communications';
    this.messageForm.patchValue({
      message: "Bonjour, je souhaite contacter l'agence concernant mon dossier."
    });
  }

  appelerAgence(): void {
    window.location.href = `tel:71340000`;
  }

  ouvrirWhatsApp(): void {
    const numero  = "21671340000";
    const message = encodeURIComponent(
      `Bonjour, je suis ${this.clientData?.nomComplet}, concernant mon dossier #${this.idDossier}.`
    );
    window.open(`https://wa.me/${numero}?text=${message}`, '_blank');
  }

  // ── Fichiers ──────────────────────────────────────────
  onFileSelected(event: any): void {
    this.fichierSelectionne = event.target.files[0] || null;
  }

  uploadFichier(): void {
    if (!this.fichierSelectionne) return;
    const formData = new FormData();
    formData.append('fichier', this.fichierSelectionne);
    this.http.post(`http://localhost:5000/api/client/upload/${this.token}`, formData).subscribe({
      next: () => { alert('Fichier envoyé !'); this.fichierSelectionne = null; },
      error: () => alert("Erreur lors de l'envoi.")
    });
  }

  // ── Téléchargements PDF ───────────────────────────────
  telechargerRecu(): void {
    this.pdfLoading.recu = true;
    this.pdfError.recu = null;
    this.http.get(
      `http://localhost:5000/api/client/recu/${this.token}?idDossier=${this.idDossier}`,
      { responseType: 'blob' }
    ).subscribe({
      next: (blob) => {
        this.pdfLoading.recu = false;
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `etat-situation-${this.idDossier}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        this.pdfLoading.recu = false;
        this.pdfError.recu = 'Erreur lors de la génération du PDF. Veuillez réessayer.';
      }
    });
  }

  telechargerHistorique(): void {
    this.pdfLoading.historique = true;
    this.pdfError.historique = null;
    this.http.get(
      `http://localhost:5000/api/client/historique-pdf/${this.token}/${this.idDossier}`,
      { responseType: 'blob' }
    ).subscribe({
      next: (blob) => {
        this.pdfLoading.historique = false;
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `historique-${this.idDossier}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        this.pdfLoading.historique = false;
        this.pdfError.historique = 'Erreur lors de la génération du PDF. Veuillez réessayer.';
      }
    });
  }

  retourDossiers(): void {
    this.router.navigate(['/client', this.token]);
  }

  // ── Communications ────────────────────────────────────
  envoyerMessage(): void {
    if (this.messageForm.invalid || this.messageSending) return;
    this.messageSending = true;
    const message = (this.messageForm.value.message ?? '').toString();
    this.recouvrementService.envoyerMessageClient(this.token, this.idDossier, message).subscribe({
      next: () => {
        this.messageForm.reset();
        this.rafraichirDossier(() => { this.messageSending = false; });
      },
      error: (err) => {
        console.error('Erreur lors de lenvoi du message', err);
        alert('Erreur serveur lors de lenvoi. Veuillez vérifier la connexion backend.');
        this.messageSending = false;
      }
    });
  }

  // ── Relances ──────────────────────────────────────────
  ouvrirReponseRelance(relance: any): void {
    this.relanceRepondreIndex = relance.idRelance;
    this.reponseRelanceTexte  = '';
  }

  annulerReponseRelance(): void {
    this.relanceRepondreIndex = null;
    this.reponseRelanceTexte  = '';
  }

  envoyerReponseRelance(r: any): void {
    if (!this.reponseRelanceTexte.trim()) return;
    this.relanceSending = true;
    this.recouvrementService.repondreRelance(this.token, r.idRelance, this.reponseRelanceTexte)
      .subscribe({
        next: () => {
          this.rafraichirDossier();
          this.relanceSending        = false;
          this.reponseRelanceTexte   = '';
          this.relanceRepondreIndex  = null;
          r.statut = 'repondu';
        },
        error: (err) => {
          console.error("Erreur lors de la réponse à la relance", err);
          alert('Erreur: impossible d\'envoyer la réponse à cette relance. Vérifiez la connexion au backend.');
          this.relanceSending = false;
        }
      });
  }

  // ── Rafraîchir dossier ────────────────────────────────
  private rafraichirDossier(callback?: () => void): void {
    this.tokenService.getClientData(this.token).subscribe({
      next: data => {
        this.clientData = data;
        this.tokenService.saveClientData(data);
        this.dossier = data.dossiers.find(d => Number(d.idDossier) === Number(this.idDossier)) || null;
        this.cdr.detectChanges();
        if (callback) callback();
      },
      error: () => {
        this.cdr.detectChanges();
        if (callback) callback();
      }
    });
  }

  changerOnglet(onglet: 'overview' | 'relances' | 'communications'): void {
    this.ongletActif = onglet;
  }

  isDarkMode = false;
  toggleTheme(): void {
    this.isDarkMode = !this.isDarkMode;
    if (this.isDarkMode) {
      document.body.classList.add('dark-theme');
    } else {
      document.body.classList.remove('dark-theme');
    }
    this.cdr.detectChanges();
  }

  intentionExistante: any = null;
  intentionLoading = false;
  afficherAvertissementDoublon = false;
  emailInvalide = false;
}