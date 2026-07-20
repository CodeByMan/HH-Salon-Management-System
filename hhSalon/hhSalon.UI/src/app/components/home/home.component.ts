import { AfterViewInit, ChangeDetectionStrategy, Component, DestroyRef, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { Group } from '../../models/group';
import { Service } from '../../models/service';
import { GroupsService } from '../../services/groups.service';
import { ServicesService } from '../../services/services.service';
import { WorkersService } from '../../services/workers.service';

interface WorkerSummary {
  id: string;
  user?: {
    firstName?: string;
    lastName?: string;
    userName?: string;
  };
  workers_Groups?: Array<{
    group?: {
      name?: string;
    };
  }>;
}

@Component({
  selector: 'app-home',
  imports: [RouterLink, DecimalPipe],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent implements OnInit, AfterViewInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly groupsService = inject(GroupsService);
  private readonly route = inject(ActivatedRoute);
  private readonly servicesService = inject(ServicesService);
  private readonly workersService = inject(WorkersService);

  @ViewChild('aboutSection') private aboutSection?: ElementRef<HTMLElement>;

  readonly groups = signal<Group[]>([]);
  readonly featuredServices = signal<Service[]>([]);
  readonly workers = signal<WorkerSummary[]>([]);
  readonly loadingGroups = signal(true);
  readonly loadingWorkers = signal(true);

  readonly groupFallbackImages = [
    '/assets/hipstyle/img/offer_img_1.png',
    '/assets/hipstyle/img/offer_img_2.png',
    '/assets/hipstyle/img/offer_img_3.png',
    '/assets/hipstyle/img/offer_img_4.png',
  ];

  readonly workerImages = [
    '/assets/hipstyle/img/artist/artist_1.png',
    '/assets/hipstyle/img/artist/artist_2.png',
    '/assets/hipstyle/img/artist/artist_3.png',
  ];

  ngOnInit(): void {
    this.loadCatalogPreview();
    this.loadWorkersPreview();
  }

  ngAfterViewInit(): void {
    if (this.route.snapshot.data['section'] === 'about') {
      requestAnimationFrame(() => this.aboutSection?.nativeElement.scrollIntoView({ behavior: 'smooth' }));
    }
  }

  groupImage(group: Group, index: number): string {
    return group.imgUrl?.trim() || this.groupFallbackImages[index % this.groupFallbackImages.length];
  }

  replaceBrokenImage(event: Event, fallback: string): void {
    const image = event.target as HTMLImageElement;
    if (image.src.endsWith(fallback)) {
      return;
    }
    image.src = fallback;
  }

  workerName(worker: WorkerSummary): string {
    const fullName = [worker.user?.firstName, worker.user?.lastName].filter(Boolean).join(' ');
    return fullName || worker.user?.userName || 'hhSalon specialist';
  }

  workerSpecialties(worker: WorkerSummary): string {
    const specialties = worker.workers_Groups
      ?.map((item) => item.group?.name)
      .filter((name): name is string => Boolean(name));
    return specialties?.length ? specialties.join(' · ') : 'Salon specialist';
  }

  private loadCatalogPreview(): void {
    this.groupsService
      .getGroups()
      .pipe(
        catchError(() => of([] as Group[])),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((groups) => {
        this.groups.set(groups);
        this.loadingGroups.set(false);

        const featuredGroups = groups.filter((group) => group.id !== undefined).slice(0, 2);
        if (!featuredGroups.length) {
          return;
        }

        forkJoin(
          featuredGroups.map((group) =>
            this.servicesService.getServices(group.id as number).pipe(catchError(() => of([] as Service[]))),
          ),
        )
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe((servicesByGroup) => this.featuredServices.set(servicesByGroup.flat().slice(0, 8)));
      });
  }

  private loadWorkersPreview(): void {
    this.workersService
      .getWorkers()
      .pipe(
        catchError(() => of([] as WorkerSummary[])),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((workers) => {
        this.workers.set((workers as WorkerSummary[]).slice(0, 3));
        this.loadingWorkers.set(false);
      });
  }
}
