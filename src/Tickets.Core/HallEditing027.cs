namespace Tickets.Core;

public static class HallEditing027
{
    public static void RenameSector(IReadOnlyList<DraftSeat> seats,string sector,string name)
    {
        name=name.Trim();
        if(name.Length is 0 or >200||name.Any(char.IsControl))throw new TicketException("Укажите название сектора (до 200 символов).");
        if(seats.Any(s=>s.Sector!=sector&&StringComparer.OrdinalIgnoreCase.Equals(s.Sector,name)))throw new TicketException("Сектор с таким названием уже есть.");
        foreach(var seat in seats.Where(s=>s.Sector==sector))seat.Sector=name;
    }
    public static void MoveRow(IReadOnlyList<DraftSeat> seats,string sector,string row,double dx)
    {
        if(!double.IsFinite(dx))throw new TicketException("Некорректное перемещение ряда.");
        foreach(var seat in seats.Where(s=>s.Sector==sector&&s.Row==row)){seat.X+=dx;seat.RowAisleAnchors=HallAnchors027.Move(seat.RowAisleAnchors,dx,0);}
    }
}

public sealed partial class Store
{
    /// <summary>Hall metadata and sector names keep the existing identity. Geometry gets a new identity.</summary>
    public long SaveEditedHall(long venueId,string name,string address,IReadOnlyCollection<DraftSeat> seats)
    {
        name=Required(name,"Название зала");address=address.Trim();
        var venue=Venues().SingleOrDefault(v=>v.Id==venueId)??throw new TicketException("Выберите зал.");
        var original=HallDraft(venueId);var draft=seats.ToArray();
        var renames=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var namesOnly=original.Count==draft.Length;
        for(var i=0;namesOnly&&i<original.Count;i++)
        {
            var old=original[i];var edit=draft[i];var sector=Required(edit.Sector,"Сектор");
            namesOnly=old.Row==edit.Row&&old.Number==edit.Number&&old.X==edit.X&&old.Y==edit.Y&&old.Hidden==edit.Hidden&&HallAnchors027.Equal(old.RowAisleAnchors,edit.RowAisleAnchors);
            if(renames.TryGetValue(old.Sector,out var renamed)&&renamed!=sector)namesOnly=false;
            renames[old.Sector]=sector;
        }
        if(namesOnly&&renames.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=renames.Count)
            throw new TicketException("Названия секторов должны различаться.");
        if(!namesOnly)
        {
            if(Venues().Any(v=>CatalogText.Key(v.Name)==CatalogText.Key(name)))
                throw new TicketException("Изменённую схему сохраните как новый зал под другим названием.");
            return CreateHall(name,address,draft);
        }
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        RequireActiveVenue(c,tx,venueId);
        if(name!=venue.Name&&Venues().Any(v=>v.Id!=venueId&&CatalogText.Key(v.Name)==CatalogText.Key(name)))
            throw new TicketException("Зал с таким названием уже есть.");
        using(var update=Command(c,"UPDATE venue SET name=$n,address=$a WHERE id=$v","$n",name,"$a",address,"$v",venueId))
        {update.Transaction=tx;update.ExecuteNonQuery();}
        // Updating sector rows keeps every seat, event, pricing and issuance reference intact.
        using var find=Command(c,"SELECT id,name FROM sector WHERE venue_id=$v","$v",venueId);find.Transaction=tx;
        var sectors=new List<(long Id,string Name)>();using(var reader=find.ExecuteReader())while(reader.Read())sectors.Add((reader.GetInt64(0),reader.GetString(1)));
        foreach(var sector in sectors)
        {
            if(!renames.TryGetValue(sector.Name,out var replacement))continue;
            using var update=Command(c,"UPDATE sector SET name=$n WHERE venue_id=$v AND id=$s","$n",replacement,"$v",venueId,"$s",sector.Id);update.Transaction=tx;update.ExecuteNonQuery();
        }
        tx.Commit();return venueId;
    }
}
