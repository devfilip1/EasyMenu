import { OrderStatus } from '../../../api/generated/models/order-status';
import { PartStatus } from '../../../api/generated/models/part-status';
import { Station } from '../../../api/generated/models/station';

/** Order status as shown to the owner. */
export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
  paid: 'Pago',
  in_preparation: 'Em preparo',
  ready: 'Pronto',
  picked_up: 'Retirado',
};

export const PART_STATUS_LABELS: Record<PartStatus, string> = {
  received: 'Recebido',
  in_preparation: 'Em preparo',
  ready: 'Pronto',
};

export const STATION_LABELS: Record<Station, string> = {
  bar: 'Bar',
  kitchen: 'Cozinha',
};
