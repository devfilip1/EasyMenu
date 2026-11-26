import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-kitchen-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1>Painel da cozinha</h1>`,
})
export class KitchenPage {}
