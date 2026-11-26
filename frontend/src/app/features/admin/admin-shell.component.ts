import { ChangeDetectionStrategy, Component } from '@angular/core';

// Stub (TDD red phase, T033). Implemented in T034.
@Component({
  selector: 'app-admin-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '',
})
export class AdminShellComponent {}
