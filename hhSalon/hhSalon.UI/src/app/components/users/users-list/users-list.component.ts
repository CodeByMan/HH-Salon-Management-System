import { Component, OnInit, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import { UsersService } from 'src/app/services/users.service';

interface UserListItem { id: string; firstName: string; lastName: string; userName: string; email: string; }

@Component({ selector: 'app-users', templateUrl: './users-list.component.html', styleUrls: ['./users-list.component.scss'], imports: [RouterLink] })
export class UsersListComponent implements OnInit {
  readonly users = signal<UserListItem[]>([]);
  readonly searchTerm = signal('');
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly filteredUsers = computed(() => {
    const query = this.searchTerm().trim().toLowerCase();
    return query ? this.users().filter((user) => `${user.firstName} ${user.lastName} ${user.userName} ${user.email}`.toLowerCase().includes(query)) : this.users();
  });
  constructor(private readonly usersService: UsersService) {}
  ngOnInit(): void { this.usersService.getUsers().pipe(catchError(() => { this.errorMessage.set('Users could not be loaded.'); return of([]); }), finalize(() => this.loading.set(false))).subscribe((items) => this.users.set(items)); }
  initials(user: UserListItem): string { return `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase() || 'U'; }
}
