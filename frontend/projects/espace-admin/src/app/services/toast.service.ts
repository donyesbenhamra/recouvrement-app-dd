import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface Toast {
  id: number;
  message: string;
  type: 'success' | 'error' | 'info';
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  private toasts: Toast[] = [];
  public toasts$ = new BehaviorSubject<Toast[]>([]);
  private idCounter = 0;

  show(message: string, type: 'success' | 'error' | 'info' = 'success') {
    const id = this.idCounter++;
    this.toasts.push({ id, message, type });
    this.toasts$.next([...this.toasts]);

    if (type === 'success') {
      this.playBeep();
    }

    // Auto removal after 4 seconds
    setTimeout(() => {
      this.remove(id);
    }, 4000);
  }

  remove(id: number) {
    this.toasts = this.toasts.filter(toast => toast.id !== id);
    this.toasts$.next([...this.toasts]);
  }

  private playBeep() {
    try {
      // Un petit 'bip' léger généré via AudioContext (très fluide, pas de fichier externe requis)
      const AudioContext = window.AudioContext || (window as any).webkitAudioContext;
      if (AudioContext) {
        const ctx = new AudioContext();
        const osc = ctx.createOscillator();
        const gainNode = ctx.createGain();
        
        osc.type = 'sine';
        // Fréquence agréable, courte et dynamique
        osc.frequency.setValueAtTime(880, ctx.currentTime); 
        osc.frequency.exponentialRampToValueAtTime(1760, ctx.currentTime + 0.1); 
        
        gainNode.gain.setValueAtTime(0.05, ctx.currentTime);
        gainNode.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.15);
        
        osc.connect(gainNode);
        gainNode.connect(ctx.destination);
        
        osc.start();
        osc.stop(ctx.currentTime + 0.15);
      }
    } catch (e) {
      console.warn('Audio API non supportée sur ce navigateur');
    }
  }
}
