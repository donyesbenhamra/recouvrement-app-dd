import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private isDarkModSig = signal<boolean>(localStorage.getItem('theme') === 'dark');
  isDarkMode = this.isDarkModSig.asReadonly();

  constructor() {
    this.applyTheme();
  }

  toggleTheme() {
    this.isDarkModSig.update(v => !v);
    localStorage.setItem('theme', this.isDarkModSig() ? 'dark' : 'light');
    this.applyTheme();
  }

  private applyTheme() {
    if (this.isDarkModSig()) {
      document.body.classList.add('dark-theme');
    } else {
      document.body.classList.remove('dark-theme');
    }
  }
}
