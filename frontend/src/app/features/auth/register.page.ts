import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-register-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1>Cadastrar estabelecimento</h1>`,
})
export class RegisterPage {}
