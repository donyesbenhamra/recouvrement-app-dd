import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
 private baseUrl = 'http://localhost:5000/api';

  constructor(private http: HttpClient) { }

  getDossiers(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/Dashboard/prioritaires`);
  }

  getDossierById(id: number): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Dossier/${id}`); // à vérifier
  }

  getStats(): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Dashboard/kpi`);
  }
  getIntentions(idDossier: number): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/intention/${idDossier}`);
  }

  // --- CLIENT SIDE (Pour référence ou preview) ---
  getClientHistorique(token: string): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/client/historique/${token}`);
  }
  getImpayesGestion(filtre = 'Tous', page = 1): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Impaye/gestion?filtre=${filtre}&page=${page}`);
  }

  getClientsGestion(statut = '', agence = '', page = 1): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/ClientList/gestion?statut=${statut}&agence=${agence}&page=${page}`);
  }
  // RELANCES
  getRelancesDashboard(canal = 'Tous', statut = 'Tous', page = 1): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Relance/dashboard?canal=${canal}&statut=${statut}&page=${page}`);
  }
  createClient(data: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/ClientList/create`, data);
  }

  envoyerToken(idDossier: number, canal: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Relance/${idDossier}/envoyer-token`, { canal });
  }

  // INTENTIONS
  getIntentionsDashboard(typeIntention = 'Tous', statut = 'Tous', page = 1): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Intention/dashboard?typeIntention=${typeIntention}&statut=${statut}&page=${page}`);
  }

  decisionIntention(id: number, decision: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Intention/${id}/decision`, { decision });
  }

  // SCORING
  getScoringDashboard(etatDossier = 'Tous', recherche = '', page = 1): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Scoring/dashboard?etatDossier=${etatDossier}&recherche=${recherche}&page=${page}`);
  }

  getScoringDetails(idDossier: number): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Scoring/${idDossier}/details`);
  }

  recalculerTous(): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Scoring/recalculer-tous`, {});
  }

  recalculerDossier(id: number): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Scoring/${id}/recalculer`, {});
  }
  
archiverClient(idDossier: number): Observable<any> {
  return this.http.patch<any>(`${this.baseUrl}/client/${idDossier}/archiver`, {});
}
updateClient(idDossier: number, data: { telephone: string, statut: string }): Observable<any> {
  return this.http.put<any>(`${this.baseUrl}/ClientList/${idDossier}`, data);
}
envoyerMessageRelance(idDossier: number, message: string): Observable<any> {
  return this.http.post<any>(`${this.baseUrl}/Relance/${idDossier}/message`, { message });
}
exportImpayesPdf(): Observable<Blob> {
  return this.http.get(`${this.baseUrl}/Impaye/export-pdf`, {
    responseType: 'blob'  // ← obligatoire pour recevoir un fichier binaire
  });
}
}
