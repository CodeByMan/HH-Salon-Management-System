import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import { AttendancesService } from 'src/app/services/attendances.service';
import { AuthService } from 'src/app/services/auth.service';
import { TimeStringPipe } from '../../../pipes/time-string.pipe';

@Component({
  selector: 'app-my-history',
  templateUrl: './my-history.component.html',
  styleUrls: ['./my-history.component.scss'],
  imports: [RouterLink, DatePipe, TimeStringPipe],
})
export class MyHistoryComponent implements OnInit {
  readonly attendances = signal<any[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly name = signal('');

  constructor(private readonly attendanceService: AttendancesService, private readonly auth: AuthService) {}

  ngOnInit(): void {
    const userId = this.auth.getIdFromToken();
    this.name.set(this.auth.getFullNameFromToken());
    if (!userId) {
      this.loading.set(false);
      this.errorMessage.set('Your session information is unavailable. Please sign in again.');
      return;
    }

    this.attendanceService.MyHistoryAttendances(userId).pipe(
      catchError(() => {
        this.errorMessage.set('Appointment history could not be loaded.');
        return of([]);
      }),
      finalize(() => this.loading.set(false)),
    ).subscribe((items) => this.attendances.set(items));
  }
}
