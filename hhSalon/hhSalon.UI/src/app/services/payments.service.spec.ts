import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { environment } from 'src/environments/environment';
import { PaymentsService } from './payments.service';

class FakeHttpClient {
  call?: { url: string; body: unknown };

  post<T>(url: string, body: unknown) {
    this.call = { url, body };
    return of({} as T);
  }
}

describe('PaymentsService', () => {
  it('creates an order using appointment IDs only', () => {
    const http = new FakeHttpClient();
    const service = new PaymentsService(http as unknown as HttpClient);

    service.createPayPalOrder([1, 2]).subscribe();

    expect(http.call).toEqual({
      url: `${environment.apiUrl}/Payments/paypal/orders`,
      body: { attendanceIds: [1, 2] }
    });
    expect((http.call?.body as Record<string, unknown>)['isPaid']).toBeUndefined();
    expect((http.call?.body as Record<string, unknown>)['amount']).toBeUndefined();
  });

  it('captures through the backend instead of setting paid locally', () => {
    const http = new FakeHttpClient();
    const service = new PaymentsService(http as unknown as HttpClient);

    service.capturePayPalOrder('ORDER/1', [1]).subscribe();

    expect(http.call).toEqual({
      url: `${environment.apiUrl}/Payments/paypal/orders/ORDER%2F1/capture`,
      body: { attendanceIds: [1] }
    });
  });
});
