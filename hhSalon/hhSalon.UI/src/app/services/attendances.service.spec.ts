import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { environment } from 'src/environments/environment';
import { AttendancesService } from './attendances.service';

class FakeHttpClient {
  call?: { url: string; body: unknown };

  put<T>(url: string, body: unknown) {
    this.call = { url, body };
    return of({} as T);
  }
}

describe('AttendancesService', () => {
  it('sends only completion status through the worker endpoint', () => {
    const http = new FakeHttpClient();
    const service = new AttendancesService(http as unknown as HttpClient);

    service.updateWorkerAttendanceStatus(9, 'Yes').subscribe();

    expect(http.call).toEqual({
      url: `${environment.apiUrl}/Attendances/worker-status`,
      body: { id: 9, isRendered: 'Yes' }
    });
    expect((http.call?.body as Record<string, unknown>)['isPaid']).toBeUndefined();
    expect((http.call?.body as Record<string, unknown>)['date']).toBeUndefined();
  });
});
