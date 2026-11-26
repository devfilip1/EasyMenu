import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api } from '../../api/generated/api';
import { login } from '../../api/generated/fn/auth/login';
import { homeFor } from '../../core/auth/home';
import { AuthStore } from '../../core/auth/auth.store';
import { errorCode, problemMessage } from '../../core/errors/problem';

@Component({
  selector: 'app-login-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
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
              [attr.aria-invalid]="invalid('email')"
            />
            @if (invalid('email')) {
              <span class="error">Informe seu e-mail.</span>
            }
          </div>

          <div class="field">
            <label for="login-password">Senha</label>
            <input
              id="login-password"
              type="password"
              formControlName="password"
              autocomplete="current-password"
              [attr.aria-invalid]="invalid('password')"
            />
            @if (invalid('password')) {
              <span class="error">Informe sua senha.</span>
            }
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

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.maxLength(254)]],
    password: ['', [Validators.required, Validators.maxLength(100)]],
  });
  protected readonly submitted = signal(false);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected invalid(name: 'email' | 'password'): boolean {
    return this.submitted() && this.form.controls[name].invalid;
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.error.set(null);
    if (this.form.invalid || this.submitting()) return;

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
