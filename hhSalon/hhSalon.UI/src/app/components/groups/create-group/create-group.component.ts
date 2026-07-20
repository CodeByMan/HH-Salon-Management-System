import { Component, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { GroupsService } from 'src/app/services/groups.service';
import { SharedService } from 'src/app/services/shared.service';
import * as toastr from 'toastr';

@Component({ selector: 'app-create-group', templateUrl: './create-group.component.html', styleUrls: ['./create-group.component.scss'], imports: [ReactiveFormsModule, RouterLink] })
export class CreateGroupComponent {
  readonly submitting = signal(false);
  readonly groupForm = this.formBuilder.nonNullable.group({ name: ['', [Validators.required, Validators.maxLength(120)]], imgUrl: [''] });
  constructor(private readonly formBuilder: FormBuilder, private readonly groupsService: GroupsService, private readonly sharedService: SharedService, private readonly router: Router) {}

  submit(): void {
    if (this.groupForm.invalid || this.submitting()) { this.groupForm.markAllAsTouched(); return; }
    this.submitting.set(true);
    this.groupsService.createGroup(this.groupForm.getRawValue()).subscribe({
      next: (groups) => { this.sharedService.sendData(groups); toastr.success('Service group was created.', 'Catalog updated'); void this.router.navigate(['/groups']); },
      error: (error) => { this.submitting.set(false); toastr.error(error?.error?.message ?? error?.error?.Message ?? 'Group could not be created.', 'Creation failed'); },
      complete: () => this.submitting.set(false),
    });
  }
}
