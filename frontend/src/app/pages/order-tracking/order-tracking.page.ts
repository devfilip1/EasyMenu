import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-order-tracking-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1>Acompanhar pedido</h1>`,
})
export class OrderTrackingPage {}
