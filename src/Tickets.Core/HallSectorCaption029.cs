using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Tickets.Core;

public sealed record SectorCaption029(string Text,double OffsetX,double OffsetY,double Angle);

public sealed partial class Store
{
    private void EnsureHallSectorCaptions029()
    {
        using var c=Open();using var q=Command(c,"CREATE TABLE IF NOT EXISTS hall_sector_caption_029(venue_id INTEGER NOT NULL,sector_id INTEGER NOT NULL,caption TEXT NOT NULL,PRIMARY KEY(venue_id,sector_id),FOREIGN KEY(venue_id,sector_id) REFERENCES sector(venue_id,id) ON DELETE CASCADE)");q.ExecuteNonQuery();
    }
    private static SectorCaption029? ReadSectorCaption029(SqliteDataReader reader,int index)
        =>reader.IsDBNull(index)?null:JsonSerializer.Deserialize<SectorCaption029>(reader.GetString(index));
    private static void ValidateSectorCaptions029(IEnumerable<DraftSeat> seats)
    {
        foreach(var group in seats.GroupBy(s=>s.Sector,StringComparer.OrdinalIgnoreCase))
        {
            if(group.Select(s=>s.SectorCaption).Distinct().Count()!=1)throw new TicketException("У мест одного сектора должна быть одна надпись.");
            if(group.First().SectorCaption is not {} caption)continue;
            var text=(caption.Text??"").Replace("\r\n","\n");
            if(string.IsNullOrWhiteSpace(text)||text.Length>200||text.Count(c=>c=='\n')>1||text.Any(c=>char.IsControl(c)&&c!='\n')||text.Split('\n').Any(string.IsNullOrWhiteSpace)||
                !double.IsFinite(caption.OffsetX)||!double.IsFinite(caption.OffsetY)||!double.IsFinite(caption.Angle)||Math.Abs(caption.OffsetX)>1000000||Math.Abs(caption.OffsetY)>1000000)
                throw new TicketException("Укажите надпись в одну или две строки и корректное положение и угол.");
        }
    }
    private static void SaveSectorCaption029(SqliteConnection c,SqliteTransaction tx,long venueId,long sectorId,SectorCaption029? caption)
    {
        using var delete=Command(c,"DELETE FROM hall_sector_caption_029 WHERE venue_id=$v AND sector_id=$s","$v",venueId,"$s",sectorId);delete.Transaction=tx;delete.ExecuteNonQuery();
        if(caption is null)return;
        using var q=Command(c,"INSERT INTO hall_sector_caption_029(venue_id,sector_id,caption) VALUES($v,$s,$c)","$v",venueId,"$s",sectorId,"$c",JsonSerializer.Serialize(caption));q.Transaction=tx;q.ExecuteNonQuery();
    }
}
