import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-public-menu-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1>Cardápio</h1>`,
})
export class PublicMenuPage {}
