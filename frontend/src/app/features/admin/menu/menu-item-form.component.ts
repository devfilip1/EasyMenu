import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Api } from '../../../api/generated/api';
import { createMenuItem } from '../../../api/generated/fn/menu-items/create-menu-item';
import { deleteMenuItemPhoto } from '../../../api/generated/fn/menu-items/delete-menu-item-photo';
import { updateMenuItem } from '../../../api/generated/fn/menu-items/update-menu-item';
import { uploadMenuItemPhoto } from '../../../api/generated/fn/menu-items/upload-menu-item-photo';
import { Category } from '../../../api/generated/models/category';
import { MenuItem } from '../../../api/generated/models/menu-item';
import { MenuItemRequest } from '../../../api/generated/models/menu-item-request';
import { Station } from '../../../api/generated/models/station';
import { errorCode, problemFieldErrors, problemMessage } from '../../../core/errors/problem';
import { formatCentsInput, parseBrlToCents } from './price-input';

const PROMO_RULE = 'O preço promocional deve ser maior que zero e menor que o preço.';
const MAX_PRICE_CENTS = 10_000_000;

type Field = 'name' | 'description' | 'price' | 'promo' | 'station' | 'category';

/** API field names (camelCase, from `validation_failed`) mapped to form fields. */
const API_FIELDS: Record<string, Field> = {
  name: 'name',
  description: 'description',
  priceCents: 'price',
  promotionalPriceCents: 'promo',
  station: 'station',
  categoryId: 'category',
};

@Component({
  selector: 'app-menu-item-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      <div class="field">
        <label for="item-name">Nome</label>
        <input
          id="item-name"
          type="text"
          formControlName="name"
          maxlength="100"
          [attr.aria-invalid]="!!errorFor('name')"
        />
        @if (errorFor('name'); as message) {
          <span class="error">{{ message }}</span>
        }
      </div>

      <div class="field">
        <label for="item-description">Descrição</label>
        <textarea
          id="item-description"
          formControlName="description"
          maxlength="500"
          rows="2"
          [attr.aria-invalid]="!!errorFor('description')"
        ></textarea>
        @if (errorFor('description'); as message) {
          <span class="error">{{ message }}</span>
        }
      </div>

      <div class="field-grid">
        <div class="field">
          <label for="item-price">Preço</label>
          <input
            id="item-price"
            type="text"
            inputmode="decimal"
            placeholder="0,00"
            formControlName="price"
            [attr.aria-invalid]="!!errorFor('price')"
          />
          @if (errorFor('price'); as message) {
            <span class="error">{{ message }}</span>
          }
        </div>

        <div class="field">
          <label for="item-promo">Preço promocional</label>
          <input
            id="item-promo"
            type="text"
            inputmode="decimal"
            placeholder="Opcional"
            formControlName="promo"
            [attr.aria-invalid]="!!errorFor('promo')"
          />
          @if (errorFor('promo'); as message) {
            <span class="error">{{ message }}</span>
          }
        </div>

        <div class="field">
          <label for="item-station">Setor</label>
          <select
            id="item-station"
            formControlName="station"
            [attr.aria-invalid]="!!errorFor('station')"
          >
            <option [ngValue]="null" disabled>Selecione</option>
            <option ngValue="bar">Bar</option>
            <option ngValue="kitchen">Cozinha</option>
          </select>
          @if (errorFor('station'); as message) {
            <span class="error">{{ message }}</span>
          }
        </div>

        <div class="field">
          <label for="item-category">Categoria</label>
          <select
            id="item-category"
            formControlName="category"
            [attr.aria-invalid]="!!errorFor('category')"
          >
            <option [ngValue]="null" disabled>Selecione</option>
            @for (category of categories(); track category.id) {
              <option [ngValue]="category.id">{{ category.name }}</option>
            }
          </select>
          @if (errorFor('category'); as message) {
            <span class="error">{{ message }}</span>
          }
        </div>
      </div>

      <div class="field">
        <label for="item-photo">Foto</label>
        <input
          id="item-photo"
          type="file"
          accept="image/jpeg,image/png,image/webp"
          (change)="onFile($event)"
        />
        <span class="hint">JPEG, PNG ou WebP, até 5 MB.</span>
        @if (photoError()) {
          <span class="error" role="alert">{{ photoError() }}</span>
        }
        @if (current()?.photoUrl) {
          <div>
            <button
              class="btn btn-sm btn-danger"
              type="button"
              [disabled]="busy()"
              (click)="removePhoto()"
            >
              Remover foto
            </button>
          </div>
        }
      </div>

      <div class="field">
        <label class="check">
          <input type="checkbox" formControlName="isAvailable" />
          Disponível
        </label>
      </div>

      <div>
        <button class="btn btn-primary" type="submit" [disabled]="busy()">Salvar</button>
      </div>
    </form>
  `,
  styles: `
    :host {
      display: block;
    }
  `,
})
export class MenuItemFormComponent {
  private readonly api = inject(Api);

  /** Categories offered in the "Categoria" select. */
  readonly categories = input<Category[]>([]);
  /** Item being edited; `null` means create. */
  readonly item = input<MenuItem | null>(null);
  /** Emitted with the saved item (also when only the photo upload failed). */
  readonly saved = output<MenuItem>();

  /** Last photo upload error; the item itself was saved. */
  readonly photoError = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.maxLength(500)]],
    price: ['', [Validators.required]],
    promo: [''],
    station: this.nullable<Station>(),
    category: this.nullable<string>(),
    isAvailable: [true],
  });

  /** The item as last saved (switches the form to edit mode after a create). */
  protected readonly current = signal<MenuItem | null>(null);
  protected readonly submitted = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Partial<Record<Field, string>>>({});
  private file: File | null = null;

  constructor() {
    effect(() => {
      const item = this.item();
      untracked(() => this.reset(item));
    });
  }

  protected onFile(event: Event): void {
    const files = (event.target as HTMLInputElement).files;
    this.file = files && files.length > 0 ? files[0] : null;
    this.photoError.set(null);
  }

  protected errorFor(field: Field): string | null {
    const server = this.serverErrors()[field];
    if (server) return server;
    if (!this.submitted()) return null;
    const { controls } = this.form;
    switch (field) {
      case 'name':
        return controls.name.invalid ? 'Informe o nome (até 100 caracteres).' : null;
      case 'description':
        return controls.description.invalid ? 'Use até 500 caracteres.' : null;
      case 'price':
        return this.priceCents() === null ? 'Informe um preço como 19,90.' : null;
      case 'promo':
        return this.promoError();
      case 'station':
        return controls.station.invalid ? 'Escolha o setor.' : null;
      case 'category':
        return controls.category.invalid ? 'Escolha a categoria.' : null;
    }
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.error.set(null);
    this.serverErrors.set({});
    const body = this.request();
    if (!body || this.busy()) return;

    this.busy.set(true);
    try {
      const current = this.current();
      let saved = current
        ? await this.api.invoke(updateMenuItem, { id: current.id, body })
        : await this.api.invoke(createMenuItem, { body });
      this.current.set(saved);
      saved = await this.uploadPhoto(saved);
      this.saved.emit(saved);
    } catch (err) {
      this.showError(err);
    } finally {
      this.busy.set(false);
    }
  }

  protected async removePhoto(): Promise<void> {
    const current = this.current();
    if (!current) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.api.invoke(deleteMenuItemPhoto, { id: current.id });
      const updated = { ...current, photoUrl: null };
      this.current.set(updated);
      this.saved.emit(updated);
    } catch (err) {
      this.error.set(problemMessage(errorCode(err)));
    } finally {
      this.busy.set(false);
    }
  }

  /** Uploads the selected photo; a failure is shown but keeps the saved item. */
  private async uploadPhoto(item: MenuItem): Promise<MenuItem> {
    const file = this.file;
    this.photoError.set(null);
    if (!file) return item;
    try {
      const updated = await this.api.invoke(uploadMenuItemPhoto, {
        id: item.id,
        body: { file },
      });
      this.file = null;
      this.current.set(updated);
      return updated;
    } catch (err) {
      this.photoError.set(problemMessage(errorCode(err)));
      return item;
    }
  }

  private request(): MenuItemRequest | null {
    const priceCents = this.priceCents();
    const promo = this.form.controls.promo.value.trim();
    if (this.form.invalid || priceCents === null || this.promoError()) return null;
    const { name, description, station, category, isAvailable } = this.form.getRawValue();
    return {
      categoryId: category!,
      name: name.trim(),
      description: description.trim() || null,
      priceCents,
      promotionalPriceCents: promo ? parseBrlToCents(promo) : null,
      isAvailable,
      station: station!,
    };
  }

  private priceCents(): number | null {
    const cents = parseBrlToCents(this.form.controls.price.value);
    return cents !== null && cents >= 1 && cents <= MAX_PRICE_CENTS ? cents : null;
  }

  private promoError(): string | null {
    const text = this.form.controls.promo.value.trim();
    if (!text) return null;
    const promo = parseBrlToCents(text);
    if (promo === null) return 'Informe um valor como 19,90 ou deixe em branco.';
    const price = this.priceCents();
    return promo <= 0 || (price !== null && promo >= price) ? PROMO_RULE : null;
  }

  private showError(err: unknown): void {
    const code = errorCode(err);
    if (code === 'invalid_promotional_price') {
      this.serverErrors.set({ promo: PROMO_RULE });
      return;
    }
    if (code === 'invalid_price') {
      this.serverErrors.set({ price: problemMessage(code) });
      return;
    }
    const fields = Object.entries(problemFieldErrors(err))
      .filter(([key]) => key in API_FIELDS)
      .map(([key, message]) => [API_FIELDS[key], message]);
    if (fields.length > 0) this.serverErrors.set(Object.fromEntries(fields));
    else this.error.set(problemMessage(code));
  }

  private reset(item: MenuItem | null): void {
    this.current.set(item);
    this.file = null;
    this.submitted.set(false);
    this.error.set(null);
    this.photoError.set(null);
    this.serverErrors.set({});
    this.form.reset({
      name: item?.name ?? '',
      description: item?.description ?? '',
      price: item ? formatCentsInput(item.priceCents) : '',
      promo:
        item?.promotionalPriceCents != null ? formatCentsInput(item.promotionalPriceCents) : '',
      station: item?.station ?? null,
      category: item?.categoryId ?? null,
      isAvailable: item?.isAvailable ?? true,
    });
  }

  private nullable<T>() {
    return inject(NonNullableFormBuilder).control<T | null>(null, Validators.required);
  }
}
