import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, map } from 'rxjs';

export interface User {
  idAgent: number;
  email: string;
  nom: string;
  prenom: string;
  role: string;
}

export interface AuthResponse {
  token: string;
  user: User;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = 'http://localhost:5000/api/auth';
  
  // Utilisation d'un signal pour réactivité moderne
  currentUser = signal<User | null>(this.getStoredUser());

  constructor(private http: HttpClient, private router: Router) { }

  login(credentials: any): Observable<AuthResponse> {
    // Changement : On envoie les credentials et on mappe la réponse du back .NET (plate) 
    // vers la structure User (imbriquée) attendue par l'admin Angular.
    return this.http.post<any>(`${this.apiUrl}/login`, credentials).pipe(
      map(res => {
        const authData: AuthResponse = {
          token: res.token,
          user: {
            idAgent: res.agentId,
            email: credentials.email,
            nom: res.nom,
            prenom: res.prenom,
            role: res.role
          }
        };
        return authData;
      }),
      tap(res => {
        sessionStorage.setItem('stb_token', res.token);
        sessionStorage.setItem('stb_user', JSON.stringify(res.user));
        this.currentUser.set(res.user);
      })
    );
  }

  logout() {
    sessionStorage.removeItem('stb_token');
    sessionStorage.removeItem('stb_user');
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  isLoggedIn(): boolean {
    return !!sessionStorage.getItem('stb_token');
  }

  getToken(): string | null {
    return sessionStorage.getItem('stb_token');
  }

  private getStoredUser(): User | null {
    const userStr = sessionStorage.getItem('stb_user');
    return userStr ? JSON.parse(userStr) : null;
  }
}
