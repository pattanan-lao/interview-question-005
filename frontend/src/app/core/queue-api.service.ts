import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

export interface TicketResponse {
  ticketNumber: string;
  issuedAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class QueueApiService {
  private readonly http = inject(HttpClient);

  takeTicket(): Observable<TicketResponse> {
    return this.http.post<TicketResponse>(`${API_BASE_URL}/queue/tickets`, {});
  }

  clearQueue(): Observable<TicketResponse> {
    return this.http.post<TicketResponse>(`${API_BASE_URL}/queue/clear`, {});
  }

  getCurrent(): Observable<TicketResponse> {
    return this.http.get<TicketResponse>(`${API_BASE_URL}/queue/current`);
  }
}
