import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { FooterComponent } from '../../components/footer/footer.component';
import { HeaderComponent } from '../../components/header/header.component';

@Component({
  selector: 'app-portal-layout',
  imports: [HeaderComponent, RouterOutlet, FooterComponent],
  template: `
    <app-header />
    <main class="portal-layout">
      <div class="portal-layout__accent" aria-hidden="true"></div>
      <router-outlet />
    </main>
    <app-footer />
  `,
  styleUrl: './portal-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortalLayoutComponent {}
