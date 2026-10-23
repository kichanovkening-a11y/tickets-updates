namespace Tickets.Core;
public sealed partial class Store
{
    private void EnsureHallHiddenSeats024()
    {
        using var c=Open();using var cmd=Command(c,"CREATE TABLE IF NOT EXISTS hall_hidden_seat(seat_id INTEGER PRIMARY KEY REFERENCES seat(id) ON DELETE CASCADE)");cmd.ExecuteNonQuery();
    }
}
