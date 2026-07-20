import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';

export interface PayPalConfiguration {
  enabled: boolean;
  clientId: string;
  currency: string;
}

export interface PayPalOrder {
  orderId: string;
  status: string;
  amount: number;
  currency: string;
  payerId?: string;
  payerEmail?: string;
}

@Injectable({ providedIn: 'root' })
export class PaymentsService {
  private readonly url = `${environment.apiUrl}/Payments`;

  constructor(private http: HttpClient) {}

  getPayPalConfiguration() {
    return this.http.get<PayPalConfiguration>(`${this.url}/paypal-configuration`);
  }

  createPayPalOrder(attendanceIds: number[]) {
    return this.http.post<PayPalOrder>(`${this.url}/paypal/orders`, { attendanceIds });
  }

  capturePayPalOrder(orderId: string, attendanceIds: number[]) {
    return this.http.post<PayPalOrder>(`${this.url}/paypal/orders/${encodeURIComponent(orderId)}/capture`, { attendanceIds });
  }
}
