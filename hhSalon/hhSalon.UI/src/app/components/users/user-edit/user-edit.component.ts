import { Component, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, finalize, of, switchMap } from 'rxjs';
import { ResetPasswordService } from 'src/app/services/reset-password.service';
import { UsersService } from 'src/app/services/users.service';
import * as toastr from 'toastr';

interface EditableUser {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  userName: string;
}

@Component({
  selector: 'app-user-edit',
  templateUrl: './user-edit.component.html',
  styleUrls: ['./user-edit.component.scss'],
  imports: [ReactiveFormsModule, RouterLink],
})
export class UserEditComponent implements OnInit {
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly sendingReset = signal(false);
  readonly errorMessage = signal('');
  readonly userName = signal('Profile');

  readonly profileForm = this.formBuilder.nonNullable.group({
    id: ['', Validators.required],
    firstName: ['', [Validators.required, Validators.maxLength(80)]],
    lastName: ['', [Validators.required, Validators.maxLength(80)]],
    email: ['', [Validators.required, Validators.email]],
    userName: [{ value: '', disabled: true }],
  });

  constructor(
    private readonly usersService: UsersService,
    private readonly route: ActivatedRoute,
    private readonly resetService: ResetPasswordService,
    private readonly formBuilder: FormBuilder,
  ) {}

  ngOnInit(): void {
    this.route.paramMap.pipe(
      switchMap((params) => {
        const id = params.get('userId');
        if (!id) {
          this.errorMessage.set('The profile identifier is missing.');
          return of(null);
        }
        return this.usersService.getUserById(id).pipe(
          catchError(() => {
            this.errorMessage.set('This profile could not be loaded. Please refresh or return to the previous page.');
            return of(null);
          }),
        );
      }),
      finalize(() => this.loading.set(false)),
    ).subscribe((user: EditableUser | null) => {
      this.loading.set(false);
      if (!user) return;
      this.userName.set(user.userName || `${user.firstName} ${user.lastName}`);
      this.profileForm.reset(user);
    });
  }

  save(): void {
    if (this.profileForm.invalid || this.saving()) {
      this.profileForm.markAllAsTouched();
      return;
    }
    const raw = this.profileForm.getRawValue();
    const payload = { id: raw.id, firstName: raw.firstName, lastName: raw.lastName, email: raw.email };
    this.saving.set(true);
    this.usersService.updateUser(payload).subscribe({
      next: (response) => toastr.success(response.message, 'Profile updated'),
      error: (error) => {
        this.saving.set(false);
        toastr.error(error?.error?.message ?? error?.error?.Message ?? 'Profile could not be updated.', 'Update failed');
      },
      complete: () => this.saving.set(false),
    });
  }

  sendResetLink(): void {
    const email = this.profileForm.controls.email.value;
    if (!email || this.profileForm.controls.email.invalid || this.sendingReset()) {
      this.profileForm.controls.email.markAsTouched();
      return;
    }
    this.sendingReset.set(true);
    this.resetService.sendResetPasswordLink(email).subscribe({
      next: (response) => toastr.success(response.message, 'Reset link sent'),
      error: (error) => {
        this.sendingReset.set(false);
        toastr.error(error?.error?.message ?? error?.error?.Message ?? 'Reset link could not be sent.', 'Request failed');
      },
      complete: () => this.sendingReset.set(false),
    });
  }
}
