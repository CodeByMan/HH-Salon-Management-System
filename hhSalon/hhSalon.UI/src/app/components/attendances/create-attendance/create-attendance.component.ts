import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import { Attendance } from 'src/app/models/attendance';
import { Group } from 'src/app/models/group';
import { Service } from 'src/app/models/service';
import { AttendancesService } from 'src/app/services/attendances.service';
import { GroupsService } from 'src/app/services/groups.service';
import { ServicesService } from 'src/app/services/services.service';
import { WorkersService } from 'src/app/services/workers.service';
import * as toastr from 'toastr';

interface BookingWorker { id: string; firstName: string; lastName: string; }

@Component({ selector: 'app-create-attendance', templateUrl: './create-attendance.component.html', styleUrls: ['./create-attendance.component.scss'], imports: [ReactiveFormsModule, RouterLink, CurrencyPipe] })
export class CreateAttendanceComponent implements OnInit {
  readonly groups = signal<Group[]>([]);
  readonly services = signal<Service[]>([]);
  readonly workers = signal<BookingWorker[]>([]);
  readonly slots = signal<string[]>([]);
  readonly activeSlot = signal('');
  readonly loading = signal(true);
  readonly loadingOptions = signal(false);
  readonly loadingSlots = signal(false);
  readonly submitting = signal(false);
  readonly errorMessage = signal('');
  readonly minimumDate = new Date().toISOString().split('T')[0];

  readonly bookingForm = this.formBuilder.nonNullable.group({ groupId: [0, [Validators.required, Validators.min(1)]], serviceId: [0, [Validators.required, Validators.min(1)]], workerId: ['', Validators.required], date: ['', Validators.required] });
  readonly selectedService = computed(() => this.services().find((service) => service.id === Number(this.bookingForm.controls.serviceId.value)));

  constructor(private readonly formBuilder: FormBuilder, private readonly attendancesService: AttendancesService, private readonly router: Router, private readonly groupsService: GroupsService, private readonly servicesService: ServicesService, private readonly workersService: WorkersService) {}

  ngOnInit(): void {
    this.groupsService.getGroups().pipe(catchError(() => { this.errorMessage.set('Service categories could not be loaded.'); return of([]); }), finalize(() => this.loading.set(false))).subscribe((groups) => {
      this.groups.set(groups);
      const firstId = groups[0]?.id;
      if (firstId) { this.bookingForm.controls.groupId.setValue(firstId); this.loadGroup(firstId); }
    });
  }

  changeGroup(groupId: number): void { this.bookingForm.patchValue({ groupId, serviceId: 0, workerId: '' }); this.activeSlot.set(''); this.slots.set([]); this.loadGroup(groupId); }

  private loadGroup(groupId: number): void {
    this.loadingOptions.set(true); this.errorMessage.set('');
    forkJoin({ services: this.servicesService.getServices(groupId).pipe(catchError(() => of([]))), workers: this.workersService.getWorkersByGroupId(groupId).pipe(catchError(() => of([]))) }).pipe(finalize(() => this.loadingOptions.set(false))).subscribe(({ services, workers }) => {
      this.services.set(services); this.workers.set(workers);
      this.bookingForm.patchValue({ serviceId: services[0]?.id ?? 0, workerId: workers[0]?.id ?? '' });
      if (this.bookingForm.controls.date.value && workers[0]?.id) this.loadSlots();
    });
  }

  loadSlots(): void {
    const workerId = this.bookingForm.controls.workerId.value;
    const date = this.bookingForm.controls.date.value;
    this.activeSlot.set(''); this.slots.set([]);
    if (!workerId || !date) return;
    if (date < this.minimumDate) { toastr.error('Choose today or a future date.', 'Invalid date'); return; }
    this.loadingSlots.set(true);
    this.attendancesService.getFreeTimeSlots(workerId, date).pipe(catchError(() => { toastr.error('Available times could not be loaded.', 'Schedule unavailable'); return of([]); }), finalize(() => this.loadingSlots.set(false))).subscribe((result) => this.slots.set(result.map((value) => value.split(':').slice(0, 2).join(':'))));
  }

  selectSlot(slot: string): void { this.activeSlot.set(this.activeSlot() === slot ? '' : slot); }

  submit(): void {
    if (this.bookingForm.invalid || !this.activeSlot() || this.submitting()) { this.bookingForm.markAllAsTouched(); if (!this.activeSlot()) toastr.error('Select an available time.', 'Time required'); return; }
    const raw = this.bookingForm.getRawValue();
    const payload: Attendance = { groupId: Number(raw.groupId), serviceId: Number(raw.serviceId), workerId: raw.workerId, date: new Date(raw.date), time: this.activeSlot(), price: this.selectedService()?.price };
    this.submitting.set(true);
    this.attendancesService.createAttendance(payload).subscribe({
      next: () => { toastr.success('Your appointment has been created.', 'Booking confirmed', { timeOut: 3500 }); void this.router.navigate(['/my-not-rendered-attendances']); },
      error: (error) => { this.submitting.set(false); toastr.error(error?.error?.message ?? error?.error?.Message ?? 'Appointment could not be created.', 'Booking failed', { timeOut: 5000 }); },
      complete: () => this.submitting.set(false),
    });
  }
}
