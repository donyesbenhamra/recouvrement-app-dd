import { inject } from '@angular/core';
import { CanActivateFn, ActivatedRouteSnapshot, Router } from '@angular/router';
import { TokenService } from '../services/token';
import { map, catchError, of } from 'rxjs';

export const tokenGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const tokenService = inject(TokenService);
  const router = inject(Router);

 const token = route.paramMap.get('token');
  if (!token) {
    router.navigate(['/token-invalide']);
    return of(false);
  }

  return tokenService.getClientData(token).pipe(
    map(response => {
      tokenService.saveClientData(response);
      return true;
    }),
    catchError(() => {
      router.navigate(['/token-invalide']);
      return of(false);
    })
  );
};