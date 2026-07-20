import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, finalize, of, switchMap, takeUntil, tap } from 'rxjs';
import { AttendancesService } from 'src/app/services/attendances.service';
import { TimeStringPipe } from '../../../pipes/time-string.pipe';

@Component({
  selector: 'app-all-attendances',
  templateUrl: './all-attendances.component.html',
  styleUrls: ['./all-attendances.component.scss'],
  imports: [RouterLink, DatePipe, TimeStringPipe],
})
export class AllAttendancesComponent implements OnInit, OnDestroy {
  readonly attendances = signal<any[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly searchTerm = signal('');
  private readonly search$ = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  constructor(private readonly attendancesService: AttendancesService) {}

  ngOnInit(): void {
    this.search$.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      tap(() => { this.loading.set(true); this.errorMessage.set(''); }),
      switchMap((content) => (content ? this.attendancesService.filterAttendances(content) : this.attendancesService.getAllAttendances()).pipe(
        catchError(() => {
          this.errorMessage.set('Appointments could not be loaded from the API.');
          return of([]);
        }),
        finalize(() => this.loading.set(false)),
      )),
      takeUntil(this.destroy$),
    ).subscribe((items) => this.attendances.set(items));
    this.search$.next('');
  }

  updateSearch(value: string): void {
    this.searchTerm.set(value);
    this.search$.next(value.trim());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  statusClass(value: string): string {
    return String(value).toLowerCase() === 'yes' ? 'premium-status premium-status--success' : 'premium-status premium-status--neutral';
  }
}
