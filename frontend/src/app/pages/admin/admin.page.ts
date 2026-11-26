import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-admin-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1>Painel do estabelecimento</h1>`,
})
export class AdminPage {}
