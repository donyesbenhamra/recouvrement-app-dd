import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface RiskScoreResult {
  score: number;
  niveau: string;
  justification: string;
}

@Injectable({ providedIn: 'root' })
export class RiskService {
  private api = 'http://localhost:5000/api/risk';
  constructor(private http: HttpClient) {}

  calculerScore(dto: any): Observable<RiskScoreResult> {
    return this.http.post<RiskScoreResult>(`${this.api}/score`, dto);
  }
}