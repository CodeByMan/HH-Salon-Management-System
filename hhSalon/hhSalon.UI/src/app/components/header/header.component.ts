import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, HostListener, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { catchError, distinctUntilChanged, filter, of } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Group } from '../../models/group';
import { AuthService } from '../../services/auth.service';
import { ChatService } from '../../services/chat.service';
import { GroupsService } from '../../services/groups.service';
import { SharedService } from '../../services/shared.service';
import { UserStoreService } from '../../services/user-store.service';
import { UsersService } from '../../services/users.service';

interface HeaderUserSummary {
  userName?: string;
  fullName?: string;
}

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeaderComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly chatServiceInternal = inject(ChatService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private readonly groupsService = inject(GroupsService);
  private readonly router = inject(Router);
  private readonly sharedService = inject(SharedService);
  private readonly userStore = inject(UserStoreService);
  private readonly usersService = inject(UsersService);

  readonly chatService = this.chatServiceInternal;
  readonly isAuthorized = signal(this.auth.isLoggedIn());
  readonly role = signal(this.auth.getRoleFromToken());
  readonly userId = signal(this.auth.getIdFromToken());
  readonly displayName = signal(this.auth.getFullNameFromToken() || 'Account');
  readonly userName = signal('');
  readonly groups = signal<Group[]>([]);
  readonly mobileOpen = signal(false);
  readonly activeDropdown = signal<string | null>(null);
  readonly scrolled = signal(false);

  ngOnInit(): void {
    this.groupsService
      .getGroups()
      .pipe(
        catchError(() => of([] as Group[])),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((groups) => this.groups.set(groups.slice(0, 8)));

    this.sharedService
      .getData()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((isLoggedIn) => this.isAuthorized.set(Boolean(isLoggedIn)));

    this.userStore
      .getFullNameFromStore()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((name) => this.displayName.set(name || this.auth.getFullNameFromToken() || 'Account'));

    this.userStore
      .getRoleFromStore()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((role) => this.role.set(role || this.auth.getRoleFromToken()));

    this.userStore
      .getIdFromStore()
      .pipe(
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((id) => {
        const resolvedId = id || this.auth.getIdFromToken();
        this.userId.set(resolvedId);
        this.initializeAuthenticatedUser(resolvedId);
      });
  }

  @HostListener('window:scroll')
  onWindowScroll(): void {
    this.scrolled.set(window.scrollY > 24);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeNavigation();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target as Node)) {
      this.activeDropdown.set(null);
      this.mobileOpen.set(false);
    }
  }

  toggleMobileMenu(): void {
    this.mobileOpen.update((open) => !open);
    this.activeDropdown.set(null);
  }

  toggleDropdown(name: string, event: MouseEvent): void {
    event.stopPropagation();
    this.activeDropdown.update((current) => (current === name ? null : name));
  }

  closeNavigation(): void {
    this.activeDropdown.set(null);
    this.mobileOpen.set(false);
  }

  signOut(): void {
    this.closeNavigation();
    this.auth.signOut().subscribe({
      next: () => this.finishSignOut(),
      error: () => this.finishSignOut(),
    });
  }

  private initializeAuthenticatedUser(id: string): void {
    if (!id) {
      this.userName.set('');
      return;
    }

    this.usersService
      .getUserById(id)
      .pipe(
        catchError(() => of(null)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((user) => {
        const summary = user as HeaderUserSummary | null;
        this.userName.set(summary?.userName || summary?.fullName || this.displayName());
      });

    if (this.chatServiceInternal.userId === id) {
      return;
    }

    this.chatServiceInternal
      .addUser(id)
      .pipe(
        catchError(() => of(null)),
        filter((response) => response !== null),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.chatServiceInternal.createChatConnection(id));
  }

  private finishSignOut(): void {
    this.auth.clearSession();
    this.sharedService.sendData(false);
    this.chatServiceInternal.stopChatConnection();
    this.isAuthorized.set(false);
    this.role.set('');
    this.userId.set('');
    this.userName.set('');
    void this.router.navigate(['/login']);
  }
}
