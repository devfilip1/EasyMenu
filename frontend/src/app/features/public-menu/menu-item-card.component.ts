import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { PublicMenuItem } from '../../api/generated/models/public-menu-item';
import { BrlPipe } from '../../shared/brl.pipe';
import { MediaUrlPipe } from '../../shared/media-url.pipe';

/** One item of the public menu. All content is interpolated (never bound as HTML). */
@Component({
  selector: 'app-menu-item-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BrlPipe, MediaUrlPipe],
  template: `
    <article class="item" data-testid="menu-item-card" [class.sold-out]="!item().isAvailable">
      <img [src]="item().photoUrl | mediaUrl" [alt]="item().name" loading="lazy" />
      <div class="body">
        <h3>{{ item().name }}</h3>
        @if (item().description) {
          <p class="muted description">{{ item().description }}</p>
        }
        <div class="footer">
          <p class="price">
            <!-- The sr-only labels carry their own spaces: whitespace between tags is dropped. -->
            @if (item().promotionalPriceCents != null) {
              <span class="sr-only">Preço original </span>
              <s class="muted">{{ item().priceCents | brl }}</s>
              <span class="sr-only"> Preço promocional </span>
            }
            <strong>{{ item().effectivePriceCents | brl }}</strong>
          </p>
          @if (!item().isAvailable) {
            <span class="badge">Esgotado</span>
          }
          <button
            class="btn btn-sm btn-primary"
            type="button"
            [attr.aria-label]="'Adicionar ' + item().name"
            [disabled]="!item().isAvailable"
            (click)="add.emit(item())"
          >
            Adicionar
          </button>
        </div>
      </div>
    </article>
  `,
  styles: `
    .item {
      display: flex;
      gap: 0.75rem;
      padding: 0.75rem;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: var(--radius);
    }
    .sold-out img {
      filter: grayscale(1);
      opacity: 0.6;
    }
    img {
      flex: none;
      width: 88px;
      height: 88px;
      object-fit: cover;
      border-radius: var(--radius-sm);
      background: var(--surface-muted);
    }
    .body {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    h3 {
      margin: 0;
      overflow-wrap: anywhere;
    }
    .description {
      margin: 0;
      overflow-wrap: anywhere;
    }
    .footer {
      margin-top: auto;
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
    }
    .price {
      margin: 0 auto 0 0;
      display: flex;
      gap: 0.4rem;
      align-items: baseline;
    }
  `,
})
export class MenuItemCardComponent {
  readonly item = input.required<PublicMenuItem>();
  readonly add = output<PublicMenuItem>();
}
