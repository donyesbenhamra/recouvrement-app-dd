import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService, Toast } from '../../services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container">
      <div *ngFor="let t of toasts" class="toast" [ngClass]="t.type">
        <div class="toast-icon">
          <span *ngIf="t.type === 'success'">✅</span>
          <span *ngIf="t.type === 'error'">🚨</span>
          <span *ngIf="t.type === 'info'">ℹ️</span>
        </div>
        <div class="toast-message">{{ t.message }}</div>
        <button class="toast-close" (click)="remove(t.id)">×</button>
      </div>
    </div>
  `,
  styleUrls: ['./toast.component.css']
})
export class ToastComponent implements OnInit {
  toasts: Toast[] = [];

  constructor(private toastService: ToastService, private cdr: ChangeDetectorRef) {}

  ngOnInit() {
    this.toastService.toasts$.subscribe(t => {
      this.toasts = t;
      this.cdr.detectChanges();
    });
  }

  remove(id: number) {
    this.toastService.remove(id);
  }
}