import { ComponentFixture, TestBed } from '@angular/core/testing';
import { textOf } from '../../../testing/dom';
import { settle } from '../../../testing/http';
import { FieldErrorComponent } from './field-error.component';

const ID = 'register-slug-error';

/**
 * T128: the field error is a polite live region that is ALWAYS in the DOM and visible to
 * assistive technology, even when empty. Screen readers only announce changes to a live region
 * that already existed, so the message must appear inside the same, already-rendered node.
 */
describe('FieldErrorComponent', () => {
  let fixture: ComponentFixture<FieldErrorComponent>;
  let host: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({});
    fixture = TestBed.createComponent(FieldErrorComponent);
    host = fixture.nativeElement;
    // Attached to the document so getComputedStyle sees the component styles.
    document.body.appendChild(host);
    fixture.componentRef.setInput('id', ID);
    fixture.componentRef.setInput('message', null);
    await settle(fixture);
  });

  afterEach(() => host.remove());

  /** The live region: the host itself or its first descendant with aria-live. */
  const region = (): HTMLElement =>
    host.hasAttribute('aria-live') ? host : (host.querySelector<HTMLElement>('[aria-live]') ?? host);

  function expectExposed(el: HTMLElement): void {
    for (let node: HTMLElement | null = el; node && node !== document.body; node = node.parentElement) {
      const style = getComputedStyle(node);
      expect(style.display, `display of <${node.tagName.toLowerCase()}>`).not.toBe('none');
      expect(style.visibility, `visibility of <${node.tagName.toLowerCase()}>`).not.toBe('hidden');
      expect(node.hasAttribute('hidden'), `hidden on <${node.tagName.toLowerCase()}>`).toBe(false);
      expect(node.getAttribute('aria-hidden'), `aria-hidden on <${node.tagName.toLowerCase()}>`).not.toBe('true');
    }
  }

  describe('without an error', () => {
    it('renders the region with aria-live="polite"', () => {
      expect(region().isConnected).toBe(true);
      expect(region().getAttribute('aria-live')).toBe('polite');
      expect(textOf(region())).toBe('');
    });

    it('keeps the region exposed (not display:none, hidden or aria-hidden)', () => {
      expectExposed(region());
    });

    it('carries the id used by aria-describedby', () => {
      expect(region().id).toBe(ID);
    });
  });

  describe('when an error appears', () => {
    it('shows the message inside the same, already rendered node', async () => {
      const before = region();

      fixture.componentRef.setInput('message', 'Este endereço já está em uso');
      await settle(fixture);

      expect(region()).toBe(before);
      expect(before.isConnected).toBe(true);
      expect(textOf(before)).toBe('Este endereço já está em uso');
      expectExposed(before);
    });

    it('keeps the same id across states', async () => {
      fixture.componentRef.setInput('message', 'Informe o e-mail.');
      await settle(fixture);
      expect(region().id).toBe(ID);

      fixture.componentRef.setInput('message', null);
      await settle(fixture);
      expect(region().id).toBe(ID);
      expect(textOf(region())).toBe('');
      expectExposed(region());
    });
  });
});
