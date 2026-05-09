import { Component } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../services/toast.service';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { NotificationService } from '../../services/notification.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.css',
})
export class Sidebar {
  constructor(
    private router: Router, 
    private toastService: ToastService,
    private authService: AuthService,
    public themeService: ThemeService,
    public notifService: NotificationService
  ) {}

  toggleTheme() {
    this.themeService.toggleTheme();
  }

  // Utilisation d'un getter pour accéder au signal réactif
  get currentUser() {
    return this.authService.currentUser();
  }

  isActive(segment: string): boolean {
    return this.router.url.includes(segment);
  }

  logout() {
    this.toastService.show("Déconnexion en cours...", "info");
    this.authService.logout();
  }
}
