import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getToken();

  // Ajouter l'en-tête Authorization si le token existe
  if (token) {
     req = req.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
      });
    return next(req);
  }

  return next(req);
};
