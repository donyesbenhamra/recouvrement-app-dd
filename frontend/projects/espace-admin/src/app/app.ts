import { Component } from '@angular/core';
import { RouterOutlet, Router } from '@angular/router';
import { Sidebar } from './components/sidebar/sidebar'; // Utilise le nom exact ici
import { CommonModule } from '@angular/common';
import { ToastComponent } from './components/toast/toast.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    Sidebar,
    CommonModule,
    ToastComponent
  ],
  templateUrl: './app.html',
  styleUrls: ['./app.css']
})
export class AppComponent {
  title = 'espace-admin';

  constructor(private router: Router) { }

  isLoginPage(): boolean {
    return this.router.url === '/login' || this.router.url === '/';
  }
}
