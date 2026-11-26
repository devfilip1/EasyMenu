import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-order-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h2>Detalhe do pedido</h2>`,
})
export class OrderDetailPage {}
