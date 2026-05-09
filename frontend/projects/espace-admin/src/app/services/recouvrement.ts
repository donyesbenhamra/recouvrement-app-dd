import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface IntentionPayload {
  idDossier: number;
  typeIntention: string;
  commentaire?: string;
  datePaiementPrevue?: string;
}

export interface ClientMessagePayload {
  message: string;
}

@Injectable({ providedIn: 'root' })
export class RecouvrementService {

  private apiUrl = 'http://localhost:5000/api';

  constructor(private http: HttpClient) {}

  soumettreReponse(payload: IntentionPayload): Observable<any> {
    return this.http.post(`${this.apiUrl}/intention`, payload);
  }

  envoyerMessageClient(token: string, idDossier: number, message: string): Observable<any> {
    const payload: ClientMessagePayload = { message };
    return this.http.post(`${this.apiUrl}/client/message/${token}/${idDossier}`, payload);
  }
}