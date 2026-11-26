import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-menu-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h2>Cardápio</h2>`,
})
export class MenuPage {}
