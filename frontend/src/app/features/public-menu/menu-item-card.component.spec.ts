import { ComponentFixture, TestBed } from '@angular/core/testing';
import { button, textOf } from '../../../testing/dom';
import { provideApiTesting, settle } from '../../../testing/http';
import { expectNoPromotionLabels, expectPromotionLabels } from '../../../testing/price-a11y';
import { BATATA, CAIPIRINHA, CHOPP } from '../../../testing/public-menu';
import { PublicMenuItem } from '../../api/generated/models/public-menu-item';
import { MenuItemCardComponent } from './menu-item-card.component';

describe('MenuItemCardComponent', () => {
  let fixture: ComponentFixture<MenuItemCardComponent>;
  let el: HTMLElement;

  async function render(item: PublicMenuItem): Promise<void> {
    TestBed.configureTestingModule({ providers: provideApiTesting() });
    fixture = TestBed.createComponent(MenuItemCardComponent);
    fixture.componentRef.setInput('item', item);
    el = fixture.nativeElement;
    await settle(fixture);
  }

  describe('accessible promotion text (T130)', () => {
    it('labels the original and the promotional price for screen readers', async () => {
      await render(CAIPIRINHA);

      expectPromotionLabels(el, 'R$ 25,00', 'R$ 19,90');
    });

    it('shows neither label without a promotion', async () => {
      await render(BATATA);

      expectNoPromotionLabels(el);
      expect(textOf(el)).toContain('R$ 30,00');
    });
  });

  it('emits add with the item, and cannot add a sold-out item', async () => {
    await render(CAIPIRINHA);
    const added: PublicMenuItem[] = [];
    fixture.componentInstance.add.subscribe((i) => added.push(i));

    button(el, /adicionar/i).click();
    expect(added).toEqual([CAIPIRINHA]);

    fixture.componentRef.setInput('item', CHOPP);
    await settle(fixture);
    expect(button(el, /adicionar/i).disabled).toBe(true);
    expect(textOf(el)).toContain('Esgotado');
  });
});
