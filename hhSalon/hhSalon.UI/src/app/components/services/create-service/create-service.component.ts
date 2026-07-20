import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import { Group } from 'src/app/models/group';
import { GroupsService } from 'src/app/services/groups.service';
import { ServicesService } from 'src/app/services/services.service';
import * as toastr from 'toastr';

@Component({ selector: 'app-create-service', templateUrl: './create-service.component.html', styleUrls: ['./create-service.component.scss'], imports: [ReactiveFormsModule, RouterLink, CurrencyPipe] })
export class CreateServiceComponent implements OnInit {
  readonly groups = signal<Group[]>([]);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly serviceForm = this.formBuilder.nonNullable.group({ name: ['', [Validators.required, Validators.maxLength(120)]], price: [0, [Validators.required, Validators.min(0.01)]], groupId: [0, [Validators.required, Validators.min(1)]] });

  constructor(private readonly formBuilder: FormBuilder, private readonly groupsService: GroupsService, private readonly servicesService: ServicesService, private readonly router: Router) {}

  ngOnInit(): void {
    this.groupsService.getGroups().pipe(catchError(() => of([])), finalize(() => this.loading.set(false))).subscribe((groups) => {
      this.groups.set(groups);
      if (groups[0]?.id) this.serviceForm.controls.groupId.setValue(groups[0].id);
    });
  }

  submit(): void {
    if (this.serviceForm.invalid || this.submitting()) { this.serviceForm.markAllAsTouched(); return; }
    const payload = this.serviceForm.getRawValue();
    const group = this.groups().find((item) => item.id === Number(payload.groupId));
    this.submitting.set(true);
    this.servicesService.createService({ ...payload, groupId: Number(payload.groupId), price: Number(payload.price) }).subscribe({
      next: () => { toastr.success('Service was created.', 'Catalog updated'); void this.router.navigate(['/services', payload.groupId, group?.name ?? 'Services']); },
      error: (error) => { this.submitting.set(false); toastr.error(error?.error?.message ?? error?.error?.Message ?? 'Service could not be created.', 'Creation failed'); },
      complete: () => this.submitting.set(false),
    });
  }
}
