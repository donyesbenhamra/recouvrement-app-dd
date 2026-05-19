import { TestBed } from '@angular/core/testing';
import * as tokenModule from './token';

describe('Token', () => {
  let service: any;
  const ServiceClass: any = (tokenModule as any).Token || (tokenModule as any).TokenService || (tokenModule as any).default || tokenModule;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ServiceClass);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
