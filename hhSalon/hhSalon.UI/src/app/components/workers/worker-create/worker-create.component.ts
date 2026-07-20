import { Component, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import { Group } from 'src/app/models/group';
import { AuthService } from 'src/app/services/auth.service';
import { GroupsService } from 'src/app/services/groups.service';
import * as toastr from 'toastr';

const STRONG_PASSWORD = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/;

@Component({ selector: 'app-worker-create', templateUrl: './worker-create.component.html', styleUrls: ['./worker-create.component.scss'], imports: [ReactiveFormsModule, RouterLink] })
export class WorkerCreateComponent implements OnInit {
  readonly groups = signal<Group[]>([]);
  readonly loadingGroups = signal(true);
  readonly submitting = signal(false);
  readonly passwordVisible = signal(false);
  readonly workerForm = this.formBuilder.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(80)]],
    lastName: ['', [Validators.required, Validators.maxLength(80)]],
    userName: ['', [Validators.required, Validators.maxLength(80)]],
    email: ['', [Validators.required, Validators.email]],
    groupsIds: this.formBuilder.nonNullable.control<number[]>([], Validators.required),
    address: ['', [Validators.required, Validators.maxLength(45)]],
    gender: ['Female', Validators.required],
    password: ['', [Validators.required, Validators.pattern(STRONG_PASSWORD)]],
  });

  constructor(private readonly formBuilder: FormBuilder, private readonly router: Router, private readonly auth: AuthService, private readonly groupsService: GroupsService) {}

  ngOnInit(): void {
    this.groupsService.getGroups().pipe(catchError(() => of([])), finalize(() => this.loadingGroups.set(false))).subscribe((groups) => this.groups.set(groups));
  }

  submit(): void {
    if (this.workerForm.invalid || this.submitting()) { this.workerForm.markAllAsTouched(); return; }
    this.submitting.set(true);
    this.auth.signUpWorker(this.workerForm.getRawValue()).subscribe({
      next: (response) => {
        toastr.success(response.message, 'Specialist created', { timeOut: 4200 });
        void this.router.navigate(['/worker-schedule-create', response.workerId]);
      },
      error: (error) => {
        this.submitting.set(false);
        toastr.error(error?.error?.message ?? error?.error?.Message ?? 'Specialist could not be created.', 'Creation failed', { timeOut: 5000 });
      },
      complete: () => this.submitting.set(false),
    });
  }
}
