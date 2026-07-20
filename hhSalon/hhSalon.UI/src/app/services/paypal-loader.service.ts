import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class PayPalLoaderService {
  private loading?: Promise<any>;

  load(clientId: string, currency: string): Promise<any> {
    if ((window as any).paypal) return Promise.resolve((window as any).paypal);
    if (this.loading) return this.loading;

    this.loading = new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = `https://www.paypal.com/sdk/js?client-id=${encodeURIComponent(clientId)}&currency=${encodeURIComponent(currency)}&intent=capture`;
      script.async = true;
      script.onload = () => resolve((window as any).paypal);
      script.onerror = () => reject(new Error('The PayPal SDK could not be loaded.'));
      document.head.appendChild(script);
    });
    return this.loading;
  }
}
