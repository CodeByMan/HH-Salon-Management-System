import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import { AuthService } from 'src/app/services/auth.service';
import { WorkersService } from 'src/app/services/workers.service';
import * as toastr from 'toastr';

interface WorkerListItem { id: string; user: { firstName: string; lastName: string; userName: string }; workers_Groups?: Array<{ group: { id: number; name: string } }>; schedules?: unknown[]; }

@Component({ selector: 'app-workers-list', templateUrl: './workers-list.component.html', styleUrls: ['./workers-list.component.scss'], imports: [RouterLink] })
export class WorkersListComponent implements OnInit {
  readonly workers = signal<WorkerListItem[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly deletingId = signal('');
  readonly role = signal(this.auth.getRoleFromToken());
  readonly isAuthorized = signal(this.auth.isLoggedIn());
  constructor(private readonly workersService: WorkersService, private readonly auth: AuthService) {}

  ngOnInit(): void { this.loadWorkers(); }

  loadWorkers(): void {
    this.loading.set(true); this.errorMessage.set('');
    this.workersService.getWorkers().pipe(catchError(() => { this.errorMessage.set('Specialists could not be loaded from the API.'); return of([]); }), finalize(() => this.loading.set(false))).subscribe((items) => this.workers.set(items));
  }

  deleteWorker(worker: WorkerListItem): void {
    if (!confirm(`Delete ${worker.user.firstName} ${worker.user.lastName}? This action may be blocked when related appointments exist.`)) return;
    this.deletingId.set(worker.id);
    this.workersService.deleteWorker(worker.id).subscribe({
      next: (items) => { this.workers.set(items); toastr.success('Specialist was removed.', 'Team updated'); },
      error: (error) => { this.deletingId.set(''); toastr.error(error?.error?.message ?? error?.error?.Message ?? 'Specialist could not be deleted.', 'Delete failed'); },
      complete: () => this.deletingId.set(''),
    });
  }

  initials(worker: WorkerListItem): string { return `${worker.user.firstName?.[0] ?? ''}${worker.user.lastName?.[0] ?? ''}`.toUpperCase() || 'HS'; }
}
