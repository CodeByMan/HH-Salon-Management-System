import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <main class="not-found">
      <div class="not-found__content">
        <span class="not-found__eyebrow">404</span>
        <h1>This page is out of style.</h1>
        <p>The address may have changed, but your next salon experience is only one click away.</p>
        <div class="not-found__actions">
          <a class="hh-button hh-button--primary" routerLink="/">Return home</a>
          <a class="hh-button hh-button--outline" routerLink="/groups">Browse services</a>
        </div>
      </div>
    </main>
  `,
  styles: [`
    .not-found { min-height: 100vh; display: grid; place-items: center; padding: 3rem 1rem; background: linear-gradient(rgba(24,27,28,.78), rgba(24,27,28,.78)), url('/assets/hipstyle/img/breadcrumb.png') center/cover; color: #fff; text-align: center; }
    .not-found__content { width: min(100%, 44rem); }
    .not-found__eyebrow { color: var(--hh-color-primary); font-size: 1rem; font-weight: 800; letter-spacing: .28em; }
    h1 { margin: 1rem 0; font-family: var(--hh-font-display); font-size: clamp(2.5rem, 7vw, 5rem); font-weight: 700; }
    p { margin: 0 auto 2rem; max-width: 34rem; color: rgba(255,255,255,.76); font-size: 1.05rem; line-height: 1.75; }
    .not-found__actions { display: flex; justify-content: center; flex-wrap: wrap; gap: 1rem; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFoundComponent {}
