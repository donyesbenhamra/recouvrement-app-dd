import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  constructor(private router: Router, private authService: AuthService) {}

  canActivate(route: ActivatedRouteSnapshot): boolean {
    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return false;
    }

    const user = this.authService.currentUser();
    const allowedRoles: string[] = route.data['roles'] || [];

    if (allowedRoles.length && (!user || !allowedRoles.includes(user.role))) {
      this.router.navigate(['/dashboard']);
      return false;
    }

    return true;
  }
}