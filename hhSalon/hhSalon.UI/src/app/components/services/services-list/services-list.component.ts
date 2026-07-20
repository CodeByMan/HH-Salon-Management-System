import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, finalize, map, of, switchMap, tap } from 'rxjs';
import { Service } from 'src/app/models/service';
import { AuthService } from 'src/app/services/auth.service';
import { ServicesService } from 'src/app/services/services.service';
import { UpdateServiceComponent } from '../update-service/update-service.component';
import * as toastr from 'toastr';

const GROUP_VISUALS: Record<number, { image: string; label: string; description: string }> = {
  1: { image: '/assets/hipstyle/img/pricing_img/pricing_img_1.png', label: 'Skin & beauty', description: 'Professional facial care designed to refresh, refine and restore.' },
  2: { image: '/assets/hipstyle/img/pricing_img/pricing_img_2.png', label: 'Body wellness', description: 'Restorative massage experiences for calm, balance and recovery.' },
  3: { image: '/assets/hipstyle/img/pricing_img/pricing_img_3.png', label: 'Nail artistry', description: 'Polished manicure and pedicure services with lasting detail.' },
  4: { image: '/assets/hipstyle/img/pricing_img/pricing_img_4.png', label: 'Hair studio', description: 'Cuts, styling and colour services shaped around your look.' },
  5: { image: '/assets/hipstyle/img/pricing_img/pricing_img_5.png', label: 'Makeup artistry', description: 'Expressive makeup for everyday confidence and special moments.' },
  6: { image: '/assets/hipstyle/img/pricing_img/pricing_img_6.png', label: 'Smooth care', description: 'Professional waxing services delivered with comfort and precision.' },
};

@Component({ selector: 'app-services-list', templateUrl: './services-list.component.html', styleUrls: ['./services-list.component.scss'], imports: [RouterLink, UpdateServiceComponent, CurrencyPipe] })
export class ServicesListComponent implements OnInit {
  readonly groupId = signal(1);
  readonly groupName = signal('Services');
  readonly services = signal<Service[]>([]);
  readonly serviceToEdit = signal<Service | null>(null);
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly role = signal(this.auth.getRoleFromToken());
  readonly visual = signal(GROUP_VISUALS[1]);

  constructor(private readonly servicesService: ServicesService, private readonly route: ActivatedRoute, private readonly auth: AuthService) {}

  ngOnInit(): void {
    this.route.paramMap.pipe(
      map((params) => ({ id: Number(params.get('groupId')), name: params.get('groupName') || 'Services' })),
      tap(({ id, name }) => { this.loading.set(true); this.errorMessage.set(''); this.groupId.set(id); this.groupName.set(decodeURIComponent(name)); this.visual.set(GROUP_VISUALS[id] ?? GROUP_VISUALS[1]); }),
      switchMap(({ id }) => this.servicesService.getServices(id).pipe(catchError((error) => { this.errorMessage.set(error?.error?.message === 'Empty' || error?.error?.Message === 'Empty' ? 'No services have been added to this category yet.' : 'Services could not be loaded from the API.'); return of([]); }), finalize(() => this.loading.set(false)))),
    ).subscribe((services) => this.services.set(services));
  }

  deleteService(service: Service): void {
    if (!confirm(`Delete “${service.name}”?`)) return;
    this.servicesService.deleteService(service).subscribe({ next: (services) => { this.services.set(Array.isArray(services) ? services : []); toastr.success('Service was removed.', 'Catalog updated'); }, error: (error) => toastr.error(error?.error?.message ?? error?.error?.Message ?? 'Service could not be deleted.', 'Delete failed') });
  }

  updatedServicesList(services: Service[]): void { if (Array.isArray(services)) this.services.set(services); this.serviceToEdit.set(null); }
}
