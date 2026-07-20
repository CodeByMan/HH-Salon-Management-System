import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { catchError, of } from 'rxjs';
import { AuthService } from './services/auth.service';
import { SharedService } from './services/shared.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  styles: [':host { display: block; min-height: 100%; }'],
})
export class AppComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly sharedService = inject(SharedService);

  ngOnInit(): void {
    this.auth
      .getCurrentUser()
      .pipe(
        catchError(() => this.auth.renewSession()),
        catchError(() => {
          this.auth.clearSession();
          return of(null);
        }),
      )
      .subscribe((user) => this.sharedService.sendData(Boolean(user)));
  }
}
