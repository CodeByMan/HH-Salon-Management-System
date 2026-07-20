import { Component, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from 'src/app/services/auth.service';
import * as toastr from 'toastr';

const STRONG_PASSWORD = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/;

@Component({
  selector: 'app-sign-up',
  templateUrl: './sign-up.component.html',
  styleUrls: ['./sign-up.component.scss'],
  imports: [ReactiveFormsModule, RouterLink],
})
export class SignUpComponent {
  readonly submitting = signal(false);
  readonly passwordVisible = signal(false);

  readonly signUpForm = this.formBuilder.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(80)]],
    lastName: ['', [Validators.required, Validators.maxLength(80)]],
    email: ['', [Validators.required, Validators.email]],
    userName: ['', [Validators.required, Validators.maxLength(80)]],
    password: ['', [Validators.required, Validators.pattern(STRONG_PASSWORD)]],
    confirmPassword: ['', [Validators.required]],
  });

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly router: Router,
    private readonly auth: AuthService,
  ) {}

  get passwordMismatch(): boolean {
    const { password, confirmPassword } = this.signUpForm.getRawValue();
    return Boolean(confirmPassword && password !== confirmPassword);
  }

  submit(): void {
    if (this.signUpForm.invalid || this.passwordMismatch || this.submitting()) {
      this.signUpForm.markAllAsTouched();
      return;
    }

    const { confirmPassword: _confirmPassword, ...payload } = this.signUpForm.getRawValue();
    this.submitting.set(true);
    this.auth.signUp(payload).subscribe({
      next: (response) => {
        toastr.success(response.message, 'Account created', { timeOut: 3200 });
        void this.router.navigate(['/login']);
      },
      error: (error) => {
        this.submitting.set(false);
        toastr.error(error?.error?.message ?? error?.error?.Message ?? 'Your account could not be created.', 'Registration failed', { timeOut: 5000 });
      },
      complete: () => this.submitting.set(false),
    });
  }
}
