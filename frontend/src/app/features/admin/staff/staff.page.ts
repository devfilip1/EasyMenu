import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Api } from '../../../api/generated/api';
import { createStaffMember } from '../../../api/generated/fn/staff/create-staff-member';
import { deactivateStaffMember } from '../../../api/generated/fn/staff/deactivate-staff-member';
import { listStaff } from '../../../api/generated/fn/staff/list-staff';
import { StaffMember } from '../../../api/generated/models/staff-member';
import { errorCode, problemFieldErrors, problemMessage } from '../../../core/errors/problem';
import { FieldErrorComponent } from '../../../shared/forms/field-error.component';
import { fieldErrorId, focusFirstInvalid } from '../../../shared/forms/form-a11y';

type Field = 'name' | 'email' | 'password';

const CLIENT_MESSAGES: Record<Field, string> = {
  name: 'Informe o nome (até 100 caracteres).',
  email: 'Informe um e-mail válido.',
  password: 'A senha precisa ter pelo menos 8 caracteres.',
};

/** Kitchen staff of the establishment: list, create and deactivate. */
@Component({
  selector: 'app-staff-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FieldErrorComponent],
  template: `
    <div class="stack">
      <h1>Equipe</h1>
      <p class="muted">Contas da cozinha e do bar: acessam só o painel da cozinha.</p>

      @if (listError()) {
        <p class="alert" role="alert">{{ listError() }}</p>
      }

      <ul class="list">
        @for (member of staff(); track member.id) {
          <li class="list-row" data-testid="staff-row">
            <div class="grow">
              <strong>{{ member.name }}</strong>
              <div class="muted">{{ member.email }}</div>
            </div>
            <span class="badge" [class.active]="member.isActive">
              {{ member.isActive ? 'Ativo' : 'Desativado' }}
            </span>
            @if (member.isActive) {
              <button
                class="btn btn-sm btn-danger"
                type="button"
                [disabled]="busyId() === member.id"
                (click)="deactivate(member)"
              >
                Desativar
              </button>
            }
          </li>
        } @empty {
          @if (loaded()) {
            <li class="muted">Nenhuma conta da cozinha ainda.</li>
          }
        }
      </ul>

      <section class="card stack" aria-labelledby="staff-form-title">
        <h2 id="staff-form-title">Nova conta da cozinha</h2>
        <form [formGroup]="form" (ngSubmit)="create()" novalidate>
          @if (error()) {
            <p class="alert" role="alert">{{ error() }}</p>
          }
          <div class="field">
            <label for="staff-name">Nome</label>
            <input
              id="staff-name"
              type="text"
              formControlName="name"
              maxlength="100"
              autocomplete="off"
              aria-required="true"
              (input)="clearServerError('name')"
              [attr.aria-invalid]="!!errorFor('name')"
              [attr.aria-describedby]="errorId('name')"
            />
            <app-field-error [id]="errorId('name')" [message]="errorFor('name')" />
          </div>
          <div class="field">
            <label for="staff-email">E-mail</label>
            <input
              id="staff-email"
              type="email"
              formControlName="email"
              autocomplete="off"
              inputmode="email"
              aria-required="true"
              (input)="clearServerError('email')"
              [attr.aria-invalid]="!!errorFor('email')"
              [attr.aria-describedby]="errorId('email')"
            />
            <app-field-error [id]="errorId('email')" [message]="errorFor('email')" />
          </div>
          <div class="field">
            <label for="staff-password">Senha</label>
            <input
              id="staff-password"
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
          <div>
            <button class="btn btn-primary" type="submit" [disabled]="saving()">Cadastrar</button>
          </div>
        </form>
      </section>
    </div>
  `,
  styles: `
    .badge.active {
      background: var(--success-bg);
      color: var(--success);
    }
  `,
})
export class StaffPage implements OnInit {
  private readonly api = inject(Api);
  private readonly host: HTMLElement = inject(ElementRef).nativeElement;

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(100)]],
  });

  protected readonly staff = signal<StaffMember[]>([]);
  protected readonly loaded = signal(false);
  protected readonly listError = signal<string | null>(null);
  protected readonly busyId = signal<string | null>(null);
  protected readonly submitted = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Partial<Record<Field, string>>>({});

  async ngOnInit(): Promise<void> {
    try {
      this.staff.set(await this.api.invoke(listStaff));
    } catch (err) {
      this.listError.set(problemMessage(errorCode(err)));
    } finally {
      this.loaded.set(true);
    }
  }

  protected errorId(field: Field): string {
    return fieldErrorId('staff', field);
  }

  protected errorFor(field: Field): string | null {
    const server = this.serverErrors()[field];
    if (server) return server;
    return this.submitted() && this.form.controls[field].invalid ? CLIENT_MESSAGES[field] : null;
  }

  protected clearServerError(field: Field): void {
    if (!this.serverErrors()[field]) return;
    this.serverErrors.update(({ [field]: _removed, ...rest }) => rest);
  }

  protected async create(): Promise<void> {
    this.submitted.set(true);
    this.error.set(null);
    this.serverErrors.set({});
    if (this.form.invalid) {
      focusFirstInvalid(this.form, this.host);
      return;
    }
    if (this.saving()) return;

    this.saving.set(true);
    try {
      const { name, email, password } = this.form.getRawValue();
      const member = await this.api.invoke(createStaffMember, {
        body: { name: name.trim(), email: email.trim(), password },
      });
      this.staff.update((list) => [...list, member]);
      this.form.reset();
      this.submitted.set(false);
    } catch (err) {
      this.showError(err);
    } finally {
      this.saving.set(false);
    }
  }

  protected async deactivate(member: StaffMember): Promise<void> {
    if (!window.confirm(`Desativar a conta de ${member.name}? Ela perde o acesso na hora.`)) {
      return;
    }
    this.busyId.set(member.id);
    this.listError.set(null);
    try {
      const updated = await this.api.invoke(deactivateStaffMember, { id: member.id });
      this.staff.update((list) => list.map((m) => (m.id === member.id ? updated : m)));
    } catch (err) {
      this.listError.set(problemMessage(errorCode(err)));
    } finally {
      this.busyId.set(null);
    }
  }

  private showError(err: unknown): void {
    const code = errorCode(err);
    if (code === 'email_taken') {
      this.serverErrors.set({ email: problemMessage(code) });
      return;
    }
    const fields = Object.entries(problemFieldErrors(err)).filter(
      (entry): entry is [Field, string] => entry[0] in CLIENT_MESSAGES,
    );
    if (fields.length > 0) this.serverErrors.set(Object.fromEntries(fields));
    else this.error.set(problemMessage(code));
  }
}
