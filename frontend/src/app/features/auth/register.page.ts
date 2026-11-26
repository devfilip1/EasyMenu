import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api } from '../../api/generated/api';
import { registerOwner } from '../../api/generated/fn/auth/register-owner';
import { AuthStore } from '../../core/auth/auth.store';
import { homeFor } from '../../core/auth/home';
import { errorCode, problemFieldErrors, problemMessage } from '../../core/errors/problem';
import { FieldErrorComponent } from '../../shared/forms/field-error.component';
import { fieldErrorId, focusFirstInvalid } from '../../shared/forms/form-a11y';
import { SLUG_PATTERN, slugify } from './slug';

type Field = 'establishmentName' | 'slug' | 'email' | 'password';

const CLIENT_MESSAGES: Record<Field, string> = {
  establishmentName: 'Use de 2 a 100 caracteres.',
  slug: 'Use de 3 a 50 letras minúsculas, números ou hífens, sem hífen no começo ou no fim.',
  email: 'Informe um e-mail válido.',
  password: 'A senha precisa ter pelo menos 8 caracteres.',
};

@Component({
  selector: 'app-register-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, FieldErrorComponent],
  template: `
    <main class="auth">
      <div class="card stack">
        <div>
          <p class="brand">EasyMenu</p>
          <h1>Cadastrar estabelecimento</h1>
          <p class="muted">Crie a conta do dono e o endereço do seu cardápio digital.</p>
        </div>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (error()) {
            <p class="alert" role="alert">{{ error() }}</p>
          }

          <div class="field">
            <label for="reg-name">Nome do estabelecimento</label>
            <input
              id="reg-name"
              type="text"
              formControlName="establishmentName"
              autocomplete="organization"
              aria-required="true"
              (input)="clearServerError('establishmentName')"
              [attr.aria-invalid]="!!errorFor('establishmentName')"
              [attr.aria-describedby]="errorId('establishmentName')"
            />
            <app-field-error
              [id]="errorId('establishmentName')"
              [message]="errorFor('establishmentName')"
            />
          </div>

          <div class="field">
            <label for="reg-slug">Endereço do cardápio</label>
            <input
              id="reg-slug"
              type="text"
              formControlName="slug"
              autocapitalize="none"
              spellcheck="false"
              aria-required="true"
              (input)="onSlugInput()"
              [attr.aria-invalid]="!!errorFor('slug')"
              [attr.aria-describedby]="'reg-slug-hint ' + errorId('slug')"
            />
            <span class="hint" id="reg-slug-hint"
              >Seu cardápio ficará em /m/{{ slugPreview() }}</span
            >
            <app-field-error [id]="errorId('slug')" [message]="errorFor('slug')" />
          </div>

          <div class="field">
            <label for="reg-email">E-mail</label>
            <input
              id="reg-email"
              type="email"
              formControlName="email"
              autocomplete="email"
              inputmode="email"
              aria-required="true"
              (input)="clearServerError('email')"
              [attr.aria-invalid]="!!errorFor('email')"
              [attr.aria-describedby]="errorId('email')"
            />
            <app-field-error [id]="errorId('email')" [message]="errorFor('email')" />
          </div>

          <div class="field">
            <label for="reg-password">Senha</label>
            <input
              id="reg-password"
              type="password"
              formControlName="password"
              autocomplete="new-password"
              aria-required="true"
              (input)="clearServerError('password')"
              [attr.aria-invalid]="!!errorFor('password')"
              [attr.aria-describedby]="errorId('password')"
            />
            <app-field-error [id]="errorId('password')" [message]="errorFor('password')" />
          </div>

          <button class="btn btn-primary btn-block" type="submit" [disabled]="submitting()">
            Criar conta
          </button>
        </form>

        <p class="muted">Já tem conta? <a routerLink="/login">Entrar</a></p>
      </div>
    </main>
  `,
})
export class RegisterPage {
  private readonly api = inject(Api);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly host: HTMLElement = inject(ElementRef).nativeElement;

  protected readonly form = inject(NonNullableFormBuilder).group({
    establishmentName: [
      '',
      [Validators.required, Validators.minLength(2), Validators.maxLength(100)],
    ],
    slug: ['', [Validators.required, Validators.pattern(SLUG_PATTERN)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(100)]],
  });

  protected readonly submitted = signal(false);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Partial<Record<Field, string>>>({});
  protected readonly slugPreview = signal('seu-bar');

  /** Once the owner types a slug, the name stops suggesting one. */
  private slugEdited = false;

  constructor() {
    this.form.controls.establishmentName.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((name) => {
        if (!this.slugEdited) this.form.controls.slug.setValue(slugify(name));
      });
    this.form.controls.slug.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((slug) => this.slugPreview.set(slug || 'seu-bar'));
  }

  protected errorId(field: Field): string {
    return fieldErrorId('reg', field);
  }

  protected errorFor(field: Field): string | null {
    const server = this.serverErrors()[field];
    if (server) return server;
    return this.submitted() && this.form.controls[field].invalid ? CLIENT_MESSAGES[field] : null;
  }

  protected onSlugInput(): void {
    this.slugEdited = true;
    this.clearServerError('slug');
  }

  protected clearServerError(field: Field): void {
    if (!this.serverErrors()[field]) return;
    this.serverErrors.update(({ [field]: _removed, ...rest }) => rest);
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.error.set(null);
    this.serverErrors.set({});
    if (this.form.invalid) {
      focusFirstInvalid(this.form, this.host);
      return;
    }
    if (this.submitting()) return;

    this.submitting.set(true);
    try {
      const { establishmentName, slug, email, password } = this.form.getRawValue();
      const session = await this.api.invoke(registerOwner, {
        body: { establishmentName: establishmentName.trim(), slug, email: email.trim(), password },
      });
      this.auth.setSession(session);
      await this.router.navigateByUrl(homeFor(session.user.role));
    } catch (err) {
      this.showError(err);
    } finally {
      this.submitting.set(false);
    }
  }

  private showError(err: unknown): void {
    const code = errorCode(err);
    switch (code) {
      case 'slug_taken':
      case 'invalid_slug':
        this.serverErrors.set({ slug: problemMessage(code) });
        return;
      case 'email_taken':
        this.serverErrors.set({ email: problemMessage(code) });
        return;
      case 'validation_failed': {
        const fields = problemFieldErrors(err);
        const known = Object.keys(fields).filter((f): f is Field => f in CLIENT_MESSAGES);
        if (known.length > 0) {
          this.serverErrors.set(Object.fromEntries(known.map((f) => [f, fields[f]])));
          return;
        }
        break;
      }
    }
    this.error.set(problemMessage(code));
  }
}
