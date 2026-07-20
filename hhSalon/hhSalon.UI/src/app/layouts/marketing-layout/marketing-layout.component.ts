import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { FooterComponent } from '../../components/footer/footer.component';
import { HeaderComponent } from '../../components/header/header.component';

@Component({
  selector: 'app-marketing-layout',
  imports: [HeaderComponent, RouterOutlet, FooterComponent],
  template: `
    <a class="skip-link" href="#main-content">Skip to main content</a>
    <app-header />
    <main id="main-content" class="site-main">
      <router-outlet />
    </main>
    <app-footer />
  `,
  styleUrl: './marketing-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MarketingLayoutComponent {}
