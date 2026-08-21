using InventoryService.Domain;

namespace InventoryService.Tests.Domain;

public class InventoryReservationTests
{
    [Fact]
    public void Create_SetsStatusReservedAndExpiresAtFiveMinutesOut()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), 2, TimeSpan.FromMinutes(5));

        Assert.Equal(ReservationStatus.Reserved, reservation.Status);
        Assert.True(reservation.ExpiresAt > reservation.ReservedAt);
        Assert.Equal(5, (reservation.ExpiresAt - reservation.ReservedAt).TotalMinutes, precision: 1);
    }

    [Fact]
    public void Confirm_FromReserved_Succeeds()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), 2, TimeSpan.FromMinutes(5));

        reservation.Confirm();

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.NotNull(reservation.ConfirmedAt);
    }

    [Fact]
    public void Expire_FromReserved_SetsStatusExpired()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), 2, TimeSpan.FromMinutes(5));

        reservation.Expire();

        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.NotNull(reservation.ReleasedAt);
    }

    [Fact]
    public void Expire_WhenAlreadyConfirmed_Throws()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), 2, TimeSpan.FromMinutes(5));
        reservation.Confirm();

        Assert.Throws<InvalidInventoryOperationException>(() => reservation.Expire());
    }

    [Fact]
    public void HasExpired_PastExpiresAt_ReturnsTrueOnlyWhileReserved()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), 2, TimeSpan.FromMinutes(-1));

        Assert.True(reservation.HasExpired(DateTime.UtcNow));

        reservation.Confirm();
        Assert.False(reservation.HasExpired(DateTime.UtcNow));
    }
}
