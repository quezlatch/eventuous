using Eventuous;

namespace Bookings.Payments.Domain;

public static class PaymentEvents {
    [EventType("PaymentRecorded")]
    public record PaymentRecorded(string BookingId, float Amount, string Currency, string Method, string Provider);
}
