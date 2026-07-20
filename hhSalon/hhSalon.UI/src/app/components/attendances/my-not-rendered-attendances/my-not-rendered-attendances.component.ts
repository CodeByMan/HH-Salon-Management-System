import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AttendancesService } from 'src/app/services/attendances.service';
import { AuthService } from 'src/app/services/auth.service';
import { PayPalLoaderService } from 'src/app/services/paypal-loader.service';
import { PaymentsService } from 'src/app/services/payments.service';
import { UserStoreService } from 'src/app/services/user-store.service';
import * as toastr from 'toastr';
import { FormsModule } from '@angular/forms';
import { NgIf, NgFor, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TimeStringPipe } from '../../../pipes/time-string.pipe';

@Component({
    selector: 'app-my-not-rendered-attendances',
    templateUrl: './my-not-rendered-attendances.component.html',
    styleUrls: ['./my-not-rendered-attendances.component.scss'],
    imports: [FormsModule, NgIf, NgFor, RouterLink, DatePipe, TimeStringPipe]
})
export class MyNotRenderedAttendancesComponent implements OnInit {
  attendances: any[] = [];
  userId = '';
  name = '';
  totalPrice = 0;
  isShown = false;
  selectedAttendances: any[] = [];
  paymentEnabled = false;
  paymentMessage = '';
  private buttonsRendered = false;

  @ViewChild('paymentRef') paymentRef!: ElementRef;

  constructor(
    private attendanceService: AttendancesService,
    private userStore: UserStoreService,
    private auth: AuthService,
    private payments: PaymentsService,
    private payPalLoader: PayPalLoaderService
  ) {}

  ngOnInit(): void {
    this.userStore.getIdFromStore().subscribe(id => this.userId = id || this.auth.getIdFromToken());
    this.userStore.getFullNameFromStore().subscribe(name => this.name = name || this.auth.getFullNameFromToken());
    this.loadNotPaid();
  }

  filterNotPaid() { this.loadNotPaid(); }

  filterIsPaid() {
    this.totalPrice = 0;
    this.selectedAttendances = [];
    this.attendanceService.MyNotRenderedIsPaidAttendances(this.userId).subscribe(result => this.attendances = result);
  }

  Pay(attendance: any) {
    if (!this.selectedAttendances.includes(attendance)) {
      this.selectedAttendances.push(attendance);
    } else {
      this.selectedAttendances.splice(this.selectedAttendances.indexOf(attendance), 1);
    }
    this.calculateTotalPrice(this.selectedAttendances.length ? this.selectedAttendances : this.attendances);
  }

  calculateTotalPrice(attendances: any[]) {
    this.totalPrice = attendances.reduce((total, attendance) => total + Number(attendance.price), 0);
  }

  private loadNotPaid() {
    this.attendanceService.MyNotRenderedNotPaidAttendances(this.userId).subscribe(result => {
      this.attendances = result;
      this.selectedAttendances = [];
      this.isShown = result.length > 0;
      this.calculateTotalPrice(result);
      if (result.length > 0) setTimeout(() => this.initializePayPal(), 0);
    });
  }

  private selectedIds(): number[] {
    const source = this.selectedAttendances.length ? this.selectedAttendances : this.attendances;
    return source.map(attendance => attendance.id);
  }

  private initializePayPal() {
    if (this.buttonsRendered || !this.paymentRef) return;
    this.payments.getPayPalConfiguration().subscribe({
      next: async configuration => {
        this.paymentEnabled = configuration.enabled;
        if (!configuration.enabled) {
          this.paymentMessage = 'PayPal is unavailable until server credentials are configured.';
          return;
        }
        try {
          const paypal = await this.payPalLoader.load(configuration.clientId, configuration.currency);
          paypal.Buttons({
            style: { size: 'small', color: 'gold', shape: 'pill' },
            createOrder: async () => {
              const order = await firstValueFrom(this.payments.createPayPalOrder(this.selectedIds()));
              return order.orderId;
            },
            onApprove: async (data: any) => {
              await firstValueFrom(this.payments.capturePayPalOrder(data.orderID, this.selectedIds()));
              toastr.success('Payment verified by PayPal.', 'SUCCESS', { timeOut: 5000 });
              this.loadNotPaid();
            },
            onError: () => toastr.error('The payment could not be verified.', 'Payment error')
          }).render(this.paymentRef.nativeElement);
          this.buttonsRendered = true;
        } catch {
          this.paymentMessage = 'The PayPal payment interface could not be loaded.';
        }
      },
      error: () => this.paymentMessage = 'Payment configuration is unavailable.'
    });
  }
}
