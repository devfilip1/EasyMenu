import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-staff-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h2>Equipe</h2>`,
})
export class StaffPage {}
