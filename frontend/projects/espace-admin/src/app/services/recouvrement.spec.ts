import { TestBed } from '@angular/core/testing';

import { Recouvrement } from './recouvrement';

describe('Recouvrement', () => {
  let service: Recouvrement;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(Recouvrement);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
