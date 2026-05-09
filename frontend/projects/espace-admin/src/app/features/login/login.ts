import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastService } from '../../services/toast.service';
import { AuthService, AuthResponse } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.html',
  styleUrls: ['./login.css']
})
export class LoginComponent {
  username = '';
  password = '';
  errorMessage = '';
  isUsernameFocused = false;
  isLoading = false;

  constructor(
    private router: Router,
    private toastService: ToastService,
    private authService: AuthService
  ) { }

  login() {
    this.errorMessage = '';
    let email = this.username.trim();

    // Auto-complète avec @stbbank.tn si l'utilisateur ne l'a pas mis
    if (email && !email.includes('@')) {
      email += '@stbbank.tn';
    }

    if (!email || email === '@stbbank.tn') {
      this.errorMessage = "Identifiant requis (ex: prenom.nom)";
      return;
    }

    if (!this.password) {
      this.errorMessage = "Mot de passe requis";
      return;
    }

    this.isLoading = true;
    this.authService.login({ email, motDePasse: this.password }).subscribe({
      next: (res) => {
        this.isLoading = false;
        this.toastService.show(`Bienvenue, ${res.user.prenom} !`, "success");
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = err.error?.message || "Échec de l'authentification";
        this.toastService.show("Erreur de connexion", "error");
      }
    });
  }

  forgotPassword() {
    this.toastService.show("Instructions envoyées à votre adresse STB.", "info");
  }
}
