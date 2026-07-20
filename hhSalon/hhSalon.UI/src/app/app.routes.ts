import { Routes } from '@angular/router';
import { AdminGuard } from './guards/admin.guard';
import { AuthGuard } from './guards/auth.guard';
import { WorkerAdminGuard } from './guards/worker-admin.guard';
import { WorkerGuard } from './guards/worker.guard';
import { AuthLayoutComponent } from './layouts/auth-layout/auth-layout.component';
import { MarketingLayoutComponent } from './layouts/marketing-layout/marketing-layout.component';
import { PortalLayoutComponent } from './layouts/portal-layout/portal-layout.component';

export const APP_ROUTES: Routes = [
  {
    path: '',
    component: MarketingLayoutComponent,
    children: [
      {
        path: '',
        title: 'hhSalon | Premium salon care',
        loadComponent: () => import('./components/home/home.component').then((m) => m.HomeComponent),
      },
      {
        path: 'about',
        title: 'About | hhSalon',
        loadComponent: () => import('./components/home/home.component').then((m) => m.HomeComponent),
        data: { section: 'about' },
      },
      {
        path: 'groups',
        title: 'Services | hhSalon',
        loadComponent: () => import('./components/groups/groups-list/groups-list.component').then((m) => m.GroupsListComponent),
      },
      {
        path: 'services/:groupId/:groupName',
        title: 'Service details | hhSalon',
        loadComponent: () => import('./components/services/services-list/services-list.component').then((m) => m.ServicesListComponent),
      },
      {
        path: 'workers',
        title: 'Our specialists | hhSalon',
        loadComponent: () => import('./components/workers/workers-list/workers-list.component').then((m) => m.WorkersListComponent),
      },
      {
        path: 'contacts',
        title: 'Contact | hhSalon',
        loadComponent: () => import('./components/contacts/contacts.component').then((m) => m.ContactsComponent),
      },
      { path: 'services', pathMatch: 'full', redirectTo: 'groups' },
      { path: 'team', pathMatch: 'full', redirectTo: 'workers' },
      { path: 'contact', pathMatch: 'full', redirectTo: 'contacts' },
    ],
  },
  {
    path: '',
    component: AuthLayoutComponent,
    children: [
      {
        path: 'login',
        title: 'Login | hhSalon',
        loadComponent: () => import('./components/login/login.component').then((m) => m.LoginComponent),
      },
      {
        path: 'sign-up',
        title: 'Create account | hhSalon',
        loadComponent: () => import('./components/sign-up/sign-up.component').then((m) => m.SignUpComponent),
      },
      {
        path: 'reset',
        title: 'Reset password | hhSalon',
        loadComponent: () => import('./components/reset/reset.component').then((m) => m.ResetComponent),
      },
      { path: 'auth/login', pathMatch: 'full', redirectTo: 'login' },
      { path: 'auth/register', pathMatch: 'full', redirectTo: 'sign-up' },
      { path: 'auth/reset-password', pathMatch: 'full', redirectTo: 'reset' },
    ],
  },
  {
    path: '',
    component: PortalLayoutComponent,
    children: [
      {
        path: 'attendances/create',
        canActivate: [AuthGuard],
        title: 'Book appointment | hhSalon',
        loadComponent: () => import('./components/attendances/create-attendance/create-attendance.component').then((m) => m.CreateAttendanceComponent),
      },
      {
        path: 'all-attendances',
        canActivate: [AdminGuard],
        title: 'Manage appointments | hhSalon',
        loadComponent: () => import('./components/attendances/all-attendances/all-attendances.component').then((m) => m.AllAttendancesComponent),
      },
      {
        path: 'my-not-rendered-attendances',
        canActivate: [AuthGuard],
        title: 'Upcoming appointments | hhSalon',
        loadComponent: () => import('./components/attendances/my-not-rendered-attendances/my-not-rendered-attendances.component').then((m) => m.MyNotRenderedAttendancesComponent),
      },
      {
        path: 'my-history',
        canActivate: [AuthGuard],
        title: 'Appointment history | hhSalon',
        loadComponent: () => import('./components/attendances/my-history/my-history.component').then((m) => m.MyHistoryComponent),
      },
      {
        path: 'worker-history',
        canActivate: [WorkerGuard],
        title: 'Worker history | hhSalon',
        loadComponent: () => import('./components/attendances/worker-history/worker-history.component').then((m) => m.WorkerHistoryComponent),
      },
      {
        path: 'worker-not-rendered',
        canActivate: [WorkerGuard],
        title: 'Worker appointments | hhSalon',
        loadComponent: () => import('./components/attendances/worker-not-rendered-attendances/worker-not-rendered-attendances.component').then((m) => m.WorkerNotRenderedAttendancesComponent),
      },
      {
        path: 'groups/create',
        canActivate: [AdminGuard],
        title: 'Create service group | hhSalon',
        loadComponent: () => import('./components/groups/create-group/create-group.component').then((m) => m.CreateGroupComponent),
      },
      {
        path: 'groups/edit/:groupId',
        canActivate: [AdminGuard],
        title: 'Edit service group | hhSalon',
        loadComponent: () => import('./components/groups/update-group/update-group.component').then((m) => m.UpdateGroupComponent),
      },
      {
        path: 'services/create',
        canActivate: [AdminGuard],
        title: 'Create service | hhSalon',
        loadComponent: () => import('./components/services/create-service/create-service.component').then((m) => m.CreateServiceComponent),
      },
      {
        path: 'users',
        canActivate: [AdminGuard],
        title: 'Manage users | hhSalon',
        loadComponent: () => import('./components/users/users-list/users-list.component').then((m) => m.UsersListComponent),
      },
      {
        path: 'admin-create',
        canActivate: [AdminGuard],
        title: 'Create administrator | hhSalon',
        loadComponent: () => import('./components/admin-create/admin-create.component').then((m) => m.AdminCreateComponent),
      },
      {
        path: 'worker-create',
        canActivate: [AdminGuard],
        title: 'Create worker | hhSalon',
        loadComponent: () => import('./components/workers/worker-create/worker-create.component').then((m) => m.WorkerCreateComponent),
      },
      {
        path: 'worker-schedule-create/:workerId',
        canActivate: [AdminGuard],
        title: 'Worker schedule | hhSalon',
        loadComponent: () => import('./components/workers/worker-schedule/worker-schedule.component').then((m) => m.WorkerScheduleComponent),
      },
      {
        path: 'workers/edit/:workerId',
        canActivate: [WorkerAdminGuard],
        title: 'Edit worker | hhSalon',
        loadComponent: () => import('./components/workers/worker-edit/worker-edit.component').then((m) => m.WorkerEditComponent),
      },
      {
        path: 'users/edit/:userId',
        canActivate: [AuthGuard],
        title: 'Edit profile | hhSalon',
        loadComponent: () => import('./components/users/user-edit/user-edit.component').then((m) => m.UserEditComponent),
      },
      {
        path: 'chat/:toUser',
        canActivate: [AuthGuard],
        title: 'Messages | hhSalon',
        loadComponent: () => import('./components/chat/chat.component').then((m) => m.ChatComponent),
      },
      { path: 'account/appointments/upcoming', pathMatch: 'full', redirectTo: 'my-not-rendered-attendances' },
      { path: 'account/appointments/history', pathMatch: 'full', redirectTo: 'my-history' },
      { path: 'worker/appointments/active', pathMatch: 'full', redirectTo: 'worker-not-rendered' },
      { path: 'worker/appointments/history', pathMatch: 'full', redirectTo: 'worker-history' },
      { path: 'admin/appointments', pathMatch: 'full', redirectTo: 'all-attendances' },
      { path: 'admin/users', pathMatch: 'full', redirectTo: 'users' },
      { path: 'admin/workers', pathMatch: 'full', redirectTo: 'workers' },
    ],
  },
  {
    path: '**',
    title: 'Page not found | hhSalon',
    loadComponent: () => import('./components/not-found/not-found.component').then((m) => m.NotFoundComponent),
  },
];
