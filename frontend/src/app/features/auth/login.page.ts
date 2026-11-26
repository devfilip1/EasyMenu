import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api } from '../../api/generated/api';
import { login } from '../../api/generated/fn/auth/login';
import { AuthStore } from '../../core/auth/auth.store';
import { homeFor } from '../../core/auth/home';
import { errorCode, problemMessage } from '../../core/errors/problem';
import { FieldErrorComponent } from '../../shared/forms/field-error.component';
import { fieldErrorId, focusFirstInvalid } from '../../shared/forms/form-a11y';

type Field = 'email' | 'password';

const MESSAGES: Record<Field, string> = {
  email: 'Informe seu e-mail.',
  password: 'Informe sua senha.',
};

@Component({
  selector: 'app-login-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, FieldErrorComponent],
  template: `
    <main class="auth">
      <div class="card stack">
        <div>
          <p class="brand">EasyMenu</p>
          <h1>Entrar</h1>
          <p class="muted">Acesse o painel do seu estabelecimento.</p>
        </div>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (error()) {
            <p class="alert" role="alert">{{ error() }}</p>
          }

          <div class="field">
            <label for="login-email">E-mail</label>
            <input
              id="login-email"
              type="email"
              formControlName="email"
              autocomplete="email"
              inputmode="email"
              aria-required="true"
              [attr.aria-invalid]="invalid('email')"
              [attr.aria-describedby]="errorId('email')"
            />
            <app-field-error [id]="errorId('email')" [message]="errorFor('email')" />
          </div>

          <div class="field">
            <label for="login-password">Senha</label>
            <input
              id="login-password"
              type="password"
              formControlName="password"
              autocomplete="current-password"
              aria-required="true"
              [attr.aria-invalid]="invalid('password')"
              [attr.aria-describedby]="errorId('password')"
            />
            <app-field-error [id]="errorId('password')" [message]="errorFor('password')" />
          </div>

          <button class="btn btn-primary btn-block" type="submit" [disabled]="submitting()">
            Entrar
          </button>
        </form>

        <p class="muted">
          Ainda não tem conta? <a routerLink="/cadastro">Cadastre seu estabelecimento</a>
        </p>
      </div>
    </main>
  `,
})
export class LoginPage {
  private readonly api = inject(Api);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly host: HTMLElement = inject(ElementRef).nativeElement;

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.maxLength(254)]],
    password: ['', [Validators.required, Validators.maxLength(100)]],
  });
  protected readonly submitted = signal(false);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected errorId(field: Field): string {
    return fieldErrorId('login', field);
  }

  protected invalid(field: Field): boolean {
    return this.submitted() && this.form.controls[field].invalid;
  }

  protected errorFor(field: Field): string | null {
    return this.invalid(field) ? MESSAGES[field] : null;
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.error.set(null);
    if (this.form.invalid) {
      focusFirstInvalid(this.form, this.host);
      return;
    }
    if (this.submitting()) return;

    this.submitting.set(true);
    try {
      const { email, password } = this.form.getRawValue();
      const session = await this.api.invoke(login, { body: { email: email.trim(), password } });
      this.auth.setSession(session);
      await this.router.navigateByUrl(homeFor(session.user.role));
    } catch (err) {
      this.error.set(problemMessage(errorCode(err)));
    } finally {
      this.submitting.set(false);
    }
  }
}
