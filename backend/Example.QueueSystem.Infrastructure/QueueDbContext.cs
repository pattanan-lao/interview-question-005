using Example.QueueSystem.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Example.QueueSystem.Infrastructure;

public class QueueDbContext : DbContext
{
    /// <summary>
    /// Seed timestamp for the singleton state row. Fixed rather than <c>UtcNow</c> because
    /// migration seed data has to be deterministic — a moving value would make EF Core
    /// scaffold a pointless "update the seed" migration on every add.
    /// </summary>
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public QueueDbContext(DbContextOptions<QueueDbContext> options)
        : base(options)
    {
    }

    public DbSet<QueueStateEntity> QueueStates => Set<QueueStateEntity>();

    public DbSet<QueueTicketEntity> QueueTickets => Set<QueueTicketEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QueueStateEntity>(entity =>
        {
            entity.ToTable("queue_state");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.CurrentLetterIndex).HasColumnName("current_letter_index");
            entity.Property(e => e.CurrentDigit).HasColumnName("current_digit");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.Property(e => e.Version).HasColumnName("version").IsConcurrencyToken();

            entity.HasData(new QueueStateEntity
            {
                Id = QueueStateEntity.SingletonId,
                CurrentLetterIndex = null,
                CurrentDigit = null,
                UpdatedAt = SeedTimestamp,
                Version = 0,
            });
        });

        modelBuilder.Entity<QueueTicketEntity>(entity =>
        {
            entity.ToTable("queue_tickets");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(e => e.TicketNumber).HasColumnName("ticket_number").IsRequired();
            entity.Property(e => e.IssuedAt).HasColumnName("issued_at").IsRequired();
        });
    }
}
