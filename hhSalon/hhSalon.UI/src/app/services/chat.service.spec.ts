import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { environment } from 'src/environments/environment';
import { Message } from '../models/message';
import { ChatService } from './chat.service';
import { UsersService } from './users.service';

class FakeHttpClient {
  call?: { method: string; url: string; body: unknown };

  post<T>(url: string, body: unknown) {
    this.call = { method: 'POST', url, body };
    return of({} as T);
  }

  put<T>(url: string, body: unknown) {
    this.call = { method: 'PUT', url, body };
    return of({} as T);
  }
}

const message: Message = {
  id: 11,
  fromId: 'browser-supplied-sender',
  fromUser: {},
  toId: 'recipient',
  toUser: {},
  content: 'Hello',
  date: new Date(),
  isRead: false
};

describe('ChatService', () => {
  it('does not send a client-controlled sender identity', () => {
    const http = new FakeHttpClient();
    const service = new ChatService(
      http as unknown as HttpClient,
      {} as UsersService,
      {} as Router
    );

    service.saveMessage(message).subscribe();

    expect(http.call).toEqual({
      method: 'POST',
      url: `${environment.apiUrl}/Chat/save-message`,
      body: { toId: 'recipient', content: 'Hello' }
    });
    expect((http.call?.body as Record<string, unknown>)['fromId']).toBeUndefined();
  });

  it('marks a message read using only its server-issued ID', () => {
    const http = new FakeHttpClient();
    const service = new ChatService(
      http as unknown as HttpClient,
      {} as UsersService,
      {} as Router
    );

    service.updateMessage(message).subscribe();

    expect(http.call).toEqual({
      method: 'PUT',
      url: `${environment.apiUrl}/Chat`,
      body: { id: 11 }
    });
    expect((http.call?.body as Record<string, unknown>)['toId']).toBeUndefined();
  });
});
