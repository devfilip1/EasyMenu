import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Api } from '../../../api/generated/api';
import { getEstablishment } from '../../../api/generated/fn/establishment/get-establishment';
import { updateEstablishment } from '../../../api/generated/fn/establishment/update-establishment';
import { Establishment } from '../../../api/generated/models/establishment';
import { errorCode, problemFieldErrors, problemMessage } from '../../../core/errors/problem';

@Component({
  selector: 'app-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  template: `
    <h1>Estabelecimento</h1>

    @if (loadError()) {
      <p class="alert" role="alert">{{ loadError() }}</p>
    }

    @if (establishment(); as current) {
      <div class="card stack">
        <div class="field">
          <label for="settings-slug">Endereço do cardápio</label>
          <input id="settings-slug" type="text" [value]="current.slug" readonly />
          <span class="hint">O endereço não pode ser alterado.</span>
        </div>

        <p>
          Link público:
          <a [href]="publicUrl()" target="_blank" rel="noopener">{{ publicUrl() }}</a>
        </p>

        <form [formGroup]="form" (ngSubmit)="save()" novalidate>
          @if (saveError()) {
            <p class="alert" role="alert">{{ saveError() }}</p>
          }
          @if (saved()) {
            <p class="notice" role="status">Alterações salvas.</p>
          }

          <div class="field">
            <label for="settings-name">Nome do estabelecimento</label>
            <input
              id="settings-name"
              type="text"
              formControlName="name"
              (input)="nameServerError.set(null); saved.set(false)"
              [attr.aria-invalid]="!!nameError()"
            />
            @if (nameError(); as message) {
              <span class="error">{{ message }}</span>
            }
          </div>

          <div>
            <button class="btn btn-primary" type="submit" [disabled]="saving()">Salvar</button>
          </div>
        </form>
      </div>
    }
  `,
})
export class SettingsPage implements OnInit {
  private readonly api = inject(Api);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
  });

  protected readonly establishment = signal<Establishment | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saveError = signal<string | null>(null);
  protected readonly nameServerError = signal<string | null>(null);
  protected readonly submitted = signal(false);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);

  protected readonly publicUrl = computed(() => {
    const slug = this.establishment()?.slug ?? '';
    return `${location.origin}/m/${slug}`;
  });

  async ngOnInit(): Promise<void> {
    try {
      this.apply(await this.api.invoke(getEstablishment));
    } catch (err) {
      this.loadError.set(problemMessage(errorCode(err)));
    }
  }

  protected nameError(): string | null {
    const server = this.nameServerError();
    if (server) return server;
    return this.submitted() && this.form.controls.name.invalid
      ? 'Use de 2 a 100 caracteres.'
      : null;
  }

  protected async save(): Promise<void> {
    this.submitted.set(true);
    this.saveError.set(null);
    this.nameServerError.set(null);
    this.saved.set(false);
    if (this.form.invalid || this.saving()) return;

    this.saving.set(true);
    try {
      const name = this.form.controls.name.value.trim();
      this.apply(await this.api.invoke(updateEstablishment, { body: { name } }));
      this.saved.set(true);
    } catch (err) {
      const fieldError = problemFieldErrors(err)['name'];
      if (fieldError) this.nameServerError.set(fieldError);
      else this.saveError.set(problemMessage(errorCode(err)));
    } finally {
      this.saving.set(false);
    }
  }

  private apply(establishment: Establishment): void {
    this.establishment.set(establishment);
    this.form.setValue({ name: establishment.name });
  }
}
