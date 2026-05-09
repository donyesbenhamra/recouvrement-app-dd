import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';


@Component({
  selector: 'app-fiche-client',
  standalone: true,
  imports: [CommonModule,RouterLink],
  templateUrl: './fiche-client.component.html',
  styleUrl: './fiche-client.css',
})
export class FicheClientComponent implements OnInit {
  clientId: number = 0;

  constructor(private route: ActivatedRoute) {}

  ngOnInit() {
    this.clientId = Number(this.route.snapshot.paramMap.get('id'));
    // Plus tard : charger les données depuis l'API avec this.clientId
  }
}