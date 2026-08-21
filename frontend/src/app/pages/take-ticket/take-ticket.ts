import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { QueueApiService } from '../../core/queue-api.service';

@Component({
  selector: 'app-take-ticket',
  imports: [],
  templateUrl: './take-ticket.html',
  styleUrl: './take-ticket.css',
})
export class TakeTicket {
  private readonly queueApi = inject(QueueApiService);
  private readonly router = inject(Router);

  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isSubmitting = signal(false);

  protected onTakeTicket(): void {
    if (this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    this.queueApi.takeTicket().subscribe({
      next: (ticket) => {
        this.isSubmitting.set(false);
        this.router.navigate(['/ticket'], { state: { ticket } });
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.errorMessage.set(
          err.status === 409
            ? 'คิวเต็มแล้ว กรุณาล้างคิวก่อนรับบัตรใหม่'
            : 'เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง',
        );
      },
    });
  }

  protected onClearQueue(): void {
    this.router.navigate(['/clear']);
  }
}
