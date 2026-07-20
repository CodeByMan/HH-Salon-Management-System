import { Component, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from 'src/app/services/auth.service';
import { ChatService } from 'src/app/services/chat.service';
import { ResetPasswordService } from 'src/app/services/reset-password.service';
import { SharedService } from 'src/app/services/shared.service';
import * as toastr from 'toastr';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [FormsModule, ReactiveFormsModule, RouterLink],
})
export class LoginComponent {
  readonly submitting = signal(false);
  readonly resetSubmitting = signal(false);
  readonly forgotOpen = signal(false);
  readonly passwordVisible = signal(false);

  readonly loginForm = this.formBuilder.nonNullable.group({
    userName: ['', [Validators.required, Validators.maxLength(80)]],
    password: ['', [Validators.required]],
  });

  readonly resetEmail = this.formBuilder.nonNullable.control('', [Validators.required, Validators.email]);

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly router: Router,
    private readonly auth: AuthService,
    private readonly sharedService: SharedService,
    private readonly chatService: ChatService,
    private readonly resetService: ResetPasswordService,
  ) {}

  onLogin(): void {
    if (this.loginForm.invalid || this.submitting()) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.auth.login(this.loginForm.getRawValue()).subscribe({
      next: (user) => {
        toastr.success(`Welcome back, ${user.fullName}.`, 'Signed in', { timeOut: 2200 });
        this.chatService.addUser(user.id).subscribe({
          next: () => {
            this.chatService.userId = user.id;
            this.chatService.createChatConnection(user.id);
          },
        });
        this.sharedService.sendData(true);
        void this.router.navigate(['/']);
      },
      error: (error) => {
        this.submitting.set(false);
        toastr.error(error?.error?.message ?? error?.error?.Message ?? 'The username or password is incorrect.', 'Unable to sign in', { timeOut: 5000 });
      },
      complete: () => this.submitting.set(false),
    });
  }

  openForgotPassword(): void {
    this.resetEmail.reset('');
    this.forgotOpen.set(true);
  }

  closeForgotPassword(): void {
    if (!this.resetSubmitting()) {
      this.forgotOpen.set(false);
    }
  }

  sendResetLink(): void {
    if (this.resetEmail.invalid || this.resetSubmitting()) {
      this.resetEmail.markAsTouched();
      return;
    }

    this.resetSubmitting.set(true);
    this.resetService.sendResetPasswordLink(this.resetEmail.value).subscribe({
      next: (response) => {
        toastr.success(response.message, 'Reset link sent', { timeOut: 3500 });
        this.forgotOpen.set(false);
      },
      error: (error) => {
        this.resetSubmitting.set(false);
        toastr.error(error?.error?.message ?? error?.error?.Message ?? 'The reset link could not be sent.', 'Request failed', { timeOut: 4000 });
      },
      complete: () => this.resetSubmitting.set(false),
    });
  }
}
