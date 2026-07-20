import { Component, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from 'src/app/services/auth.service';
import * as toastr from 'toastr';

const STRONG_PASSWORD = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/;

@Component({
  selector: 'app-admin-create',
  templateUrl: './admin-create.component.html',
  styleUrls: ['./admin-create.component.scss'],
  imports: [ReactiveFormsModule, RouterLink],
})
export class AdminCreateComponent {
  readonly submitting = signal(false);
  readonly passwordVisible = signal(false);
  readonly adminForm = this.formBuilder.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(80)]],
    lastName: ['', [Validators.required, Validators.maxLength(80)]],
    email: ['', [Validators.required, Validators.email]],
    userName: ['', [Validators.required, Validators.maxLength(80)]],
    password: ['', [Validators.required, Validators.pattern(STRONG_PASSWORD)]],
  });

  constructor(private readonly formBuilder: FormBuilder, private readonly router: Router, private readonly auth: AuthService) {}

  submit(): void {
    if (this.adminForm.invalid || this.submitting()) {
      this.adminForm.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.auth.createAdmin(this.adminForm.getRawValue()).subscribe({
      next: (response) => {
        toastr.success(response.message, 'Administrator created', { timeOut: 4000 });
        void this.router.navigate(['/users']);
      },
      error: (error) => {
        this.submitting.set(false);
        toastr.error(error?.error?.message ?? error?.error?.Message ?? 'Administrator could not be created.', 'Creation failed', { timeOut: 5000 });
      },
      complete: () => this.submitting.set(false),
    });
  }
}
