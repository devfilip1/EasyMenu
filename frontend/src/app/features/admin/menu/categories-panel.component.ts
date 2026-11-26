import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { Api } from '../../../api/generated/api';
import { createCategory } from '../../../api/generated/fn/categories/create-category';
import { deleteCategory } from '../../../api/generated/fn/categories/delete-category';
import { listCategories } from '../../../api/generated/fn/categories/list-categories';
import { renameCategory } from '../../../api/generated/fn/categories/rename-category';
import { reorderCategories } from '../../../api/generated/fn/categories/reorder-categories';
import { Category } from '../../../api/generated/models/category';
import { errorCode, problemMessage } from '../../../core/errors/problem';
import { byPosition, moved } from './reorder';

/** Lists, creates, renames, reorders and deletes the menu categories. */
@Component({
  selector: 'app-categories-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card stack" aria-labelledby="categories-title">
      <h2 id="categories-title">Categorias</h2>

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      <form class="create" (submit)="$event.preventDefault(); create()">
        <div class="field grow">
          <label for="new-category">Nova categoria</label>
          <input
            id="new-category"
            type="text"
            maxlength="60"
            placeholder="Ex.: Drinks"
            [value]="newName()"
            (input)="newName.set($any($event.target).value)"
          />
        </div>
        <button class="btn btn-primary" type="submit" [disabled]="!canCreate()">Adicionar</button>
      </form>

      @if (categories().length === 0 && loaded()) {
        <p class="muted">Nenhuma categoria ainda. Crie a primeira acima.</p>
      }

      <ol class="list">
        @for (category of categories(); track category.id; let first = $first, last = $last) {
          <li class="list-row" data-testid="category-row">
            @if (editingId() === category.id) {
              <div class="field grow">
                <input
                  type="text"
                  maxlength="60"
                  aria-label="Nome da categoria"
                  [value]="editName()"
                  (input)="editName.set($any($event.target).value)"
                  (keydown.enter)="saveRename(category)"
                  (keydown.escape)="editingId.set(null)"
                />
              </div>
              <div class="actions">
                <button
                  class="btn btn-sm btn-primary"
                  type="button"
                  [disabled]="!editName().trim() || busy()"
                  (click)="saveRename(category)"
                >
                  Salvar
                </button>
                <button class="btn btn-sm" type="button" (click)="editingId.set(null)">
                  Cancelar
                </button>
              </div>
            } @else {
              <span class="grow name">{{ category.name }}</span>
              <div class="actions">
                <button class="btn btn-sm" type="button" (click)="startRename(category)">
                  Renomear
                </button>
                <button
                  class="btn btn-sm"
                  type="button"
                  [attr.aria-label]="'Subir ' + category.name"
                  [disabled]="first || busy()"
                  (click)="move(category, -1)"
                >
                  Subir
                </button>
                <button
                  class="btn btn-sm"
                  type="button"
                  [attr.aria-label]="'Descer ' + category.name"
                  [disabled]="last || busy()"
                  (click)="move(category, 1)"
                >
                  Descer
                </button>
                <button
                  class="btn btn-sm btn-danger"
                  type="button"
                  [disabled]="busy()"
                  (click)="remove(category)"
                >
                  Excluir
                </button>
              </div>
            }
          </li>
        }
      </ol>
    </section>
  `,
  styles: `
    .create {
      flex-direction: row;
      align-items: flex-end;
      gap: 0.5rem;
    }
    .grow {
      flex: 1 1 10rem;
      min-width: 0;
    }
    .name {
      font-weight: 600;
    }
  `,
})
export class CategoriesPanelComponent implements OnInit {
  private readonly api = inject(Api);

  /** Emitted with the categories in display order after every load or change. */
  readonly categoriesChange = output<Category[]>();

  protected readonly categories = signal<Category[]>([]);
  protected readonly loaded = signal(false);
  protected readonly newName = signal('');
  protected readonly editingId = signal<string | null>(null);
  protected readonly editName = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly canCreate = computed(() => this.newName().trim().length > 0 && !this.busy());

  async ngOnInit(): Promise<void> {
    await this.run(async () => {
      this.set(byPosition(await this.api.invoke(listCategories)));
      this.loaded.set(true);
    });
  }

  protected async create(): Promise<void> {
    const name = this.newName().trim();
    if (!name) return;
    await this.run(async () => {
      const created = await this.api.invoke(createCategory, { body: { name } });
      this.set([...this.categories(), created]);
      this.newName.set('');
    });
  }

  protected startRename(category: Category): void {
    this.editName.set(category.name);
    this.editingId.set(category.id);
  }

  protected async saveRename(category: Category): Promise<void> {
    const name = this.editName().trim();
    if (!name) return;
    await this.run(async () => {
      const renamed = await this.api.invoke(renameCategory, { id: category.id, body: { name } });
      this.set(this.categories().map((c) => (c.id === category.id ? { ...c, ...renamed } : c)));
      this.editingId.set(null);
    });
  }

  protected async move(category: Category, delta: -1 | 1): Promise<void> {
    const list = this.categories();
    const next = moved(list, list.indexOf(category), delta);
    await this.run(async () => {
      await this.api.invoke(reorderCategories, { body: { ids: next.map((c) => c.id) } });
      this.set(next.map((c, position) => ({ ...c, position })));
    });
  }

  protected async remove(category: Category): Promise<void> {
    if (!window.confirm(`Excluir a categoria "${category.name}"?`)) return;
    await this.run(async () => {
      await this.api.invoke(deleteCategory, { id: category.id });
      this.set(this.categories().filter((c) => c.id !== category.id));
    });
  }

  private set(categories: Category[]): void {
    this.categories.set(categories);
    this.categoriesChange.emit(categories);
  }

  /** Runs one API action at a time, showing its error as a form-level alert. */
  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
    } catch (err) {
      this.error.set(problemMessage(errorCode(err)));
    } finally {
      this.busy.set(false);
    }
  }
}
