import { Component, ElementRef, Input, QueryList, ViewChildren } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { Group } from 'src/app/models/group';
import { GroupsService } from 'src/app/services/groups.service';
import { SharedService } from 'src/app/services/shared.service';
import { NgFor } from '@angular/common';

@Component({
    selector: 'app-groups-menu',
    templateUrl: './groups-menu.component.html',
    styleUrls: ['./groups-menu.component.scss'],
    imports: [NgFor, RouterLink],
})
export class GroupsMenuComponent {
  
  public groups: Group[] = [];
  subscription: Subscription | undefined;
  
  @ViewChildren('link') menu_links!: QueryList<ElementRef>;

  constructor(private groupsService: GroupsService,
    private router:Router,
    private sharedService: SharedService,
      ){      
      }
  
  ngOnInit(): void {
    this.groupsService.getGroups().subscribe(
      (result: Group[]) => {this.groups = result; }
    );


    this.subscription = this.sharedService.getData().subscribe(updatedGroups => this.groups = updatedGroups);
  }

}
