import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { forkJoin } from 'rxjs';

export interface NotificationCounts {
  intentions: number;
  relances: number;
  impayes: number;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private baseUrl = 'http://localhost:5000/api';

  private countsSig = signal<NotificationCounts>({
    intentions: 0,
    relances: 0,
    impayes: 0
  });

  counts = this.countsSig.asReadonly();

  constructor(private http: HttpClient) {
    this.refresh();
  }

  refresh() {
    forkJoin({
      intentions: this.http.get<any>(`${this.baseUrl}/Intention/dashboard`),
      relances: this.http.get<any>(`${this.baseUrl}/Relance/dashboard`),
      impayes: this.http.get<any>(`${this.baseUrl}/Impaye/gestion`)
    }).subscribe({
      next: res => {
        this.countsSig.set({
          intentions: res.intentions.kpis.nonTraitees,
          relances: res.relances.kpis.enAttenteReponse,
          impayes: res.impayes.kpis.totalItems ?? res.impayes.totalItems
        });
      },
      error: () => {} // silencieux si le back est down
    });
  }
}