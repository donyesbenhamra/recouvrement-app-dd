import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface EcheanceDto {
  montant: number;
  dateEcheance: string;
  statut: string;
}

export interface RelanceDto {
  dateRelance: string;
  moyen: string;
  statut: string;
  reponseBanque?: string | null;
  dateReponseBanque?: string | null;
}

export interface DossierDto {
  idDossier: number;
  typeEmprunt: string;
  montantInitial: number;
  montantImpaye: number;
  fraisDossier: number;
  statutDossier: string;
  dateEcheance: string;
  echeances: EcheanceDto[];
  paiements: any[];
  relances: RelanceDto[];
  communications: any[];
}

export interface ClientHistoriqueDto {
  nomComplet: string;
  idAgence: number;
  villeAgence: string;
  dossiers: DossierDto[];
}

@Injectable({ providedIn: 'root' })
export class TokenService {

  private apiUrl = 'http://localhost:5203/api';

  constructor(private http: HttpClient) {}

  getClientData(token: string): Observable<ClientHistoriqueDto> {
    return this.http.get<ClientHistoriqueDto>(
      `${this.apiUrl}/client/historique/${token}`
    );
  }

  saveClientData(data: ClientHistoriqueDto): void {
    sessionStorage.setItem('clientData', JSON.stringify(data));
  }

  getClientDataFromSession(): ClientHistoriqueDto | null {
    const data = sessionStorage.getItem('clientData');
    return data ? JSON.parse(data) : null;
  }

  clearClientData(): void {
    sessionStorage.removeItem('clientData');
  }
}