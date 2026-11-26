import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-orders-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h2>Pedidos</h2>`,
})
export class OrdersPage {}
