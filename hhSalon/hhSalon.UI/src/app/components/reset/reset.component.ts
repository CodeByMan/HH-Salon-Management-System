import { NgIf } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import * as toastr from 'toastr';

import { confirmPasswordValidator } from 'src/app/helpers/confirm-password';
import ValidateForm from 'src/app/helpers/validateForm';
import { ResetPassword } from 'src/app/models/reset-password';
import { ResetPasswordService } from 'src/app/services/reset-password.service';

@Component({
  selector: 'app-reset',
  standalone: true,
  imports: [ReactiveFormsModule, NgIf],
  templateUrl: './reset.component.html',
  styleUrls: ['./reset.component.scss'],
})
export class ResetComponent implements OnInit {
  resetPasswordForm!: FormGroup;
  emailToReset = '';
  emailToken = '';
  resetPasswordObj = new ResetPassword();

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly resetService: ResetPasswordService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.resetPasswordForm = this.formBuilder.group(
      {
        password: [null, Validators.required],
        confirmPassword: [null, Validators.required],
      },
      {
        validators: confirmPasswordValidator('password', 'confirmPassword'),
      },
    );

    this.route.queryParams.subscribe((params) => {
      this.emailToReset = params['email'] ?? '';
      const token = params['code'] ?? '';
      this.emailToken = token.replace(/ /g, '+');
    });
  }

  reset(): void {
    if (this.resetPasswordForm.invalid) {
      ValidateForm.validateAllFormFields(this.resetPasswordForm);
      return;
    }

    this.resetPasswordObj.email = this.emailToReset;
    this.resetPasswordObj.newPassword = this.resetPasswordForm.value.password;
    this.resetPasswordObj.confirmPassword = this.resetPasswordForm.value.confirmPassword;
    this.resetPasswordObj.emailToken = this.emailToken;

    this.resetService.resetPassword(this.resetPasswordObj).subscribe({
      next: (response) => {
        toastr.success(response.message, 'Success', { timeOut: 3000 });
        void this.router.navigate(['login']);
      },
      error: (error) => {
        const message = error?.error?.message ?? 'Password reset failed.';
        toastr.error(message.replace('\n', '<br/>'), 'ERROR', { timeOut: 3000 });
      },
    });
  }
}
