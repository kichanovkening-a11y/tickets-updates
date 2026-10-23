using Tickets.Core;
internal static class Corrections025Checks
{
    internal static void Register(Action<string,Action<Store>> run)
    {
        run("025 whole row passage removes only selected row without geometric repacking",store=>{
            foreach(var angle in new[]{0d,90d,180d}){
                var seats=HallLayout.Generate("Партер",new[]{"1","2","3"}.Select(r=>new HallRowLayout{Label=r,SeatCount=4}).ToArray(),"1",false,0).ToList();HallEditing023.Rotate(seats,angle);
                seats[5].Hidden=true;var removed=seats.Where(s=>s.Row=="2").ToArray();var selected=removed.Where(s=>!s.Hidden).ToArray();var keep=seats.Except(removed).ToDictionary(s=>s,s=>(s.X,s.Y));
                HallEditing025.MakeAisle(seats,selected);
                if(seats.Count!=8||seats.Any(s=>keep[s]!=(s.X,s.Y))||!seats.Select(s=>s.Row).Distinct().SequenceEqual(new[]{"1","2"}))throw new Exception("Whole row not removed and renumbered in place");
                var hall=store.CreateHall("025 passage "+angle,"",seats);if(new Store(store.Path).HallDraft(hall).Count!=8)throw new Exception("Passage regenerated on reopen");
            }
        });
        run("025 letters in middle skip numeric sequence and preserve hidden geometry",_=>{
            var seats=HallLayout.Generate("Партер",new[]{"1","2","3"}.Select(r=>new HallRowLayout{Label=r,SeatCount=4}).ToArray(),"1",true,0).ToList();seats[2].Hidden=true;var old=seats.ToDictionary(s=>s,s=>(s.X,s.Y));
            HallEditing025.AssignLetter(seats,seats[4],"а",true);
            if(!seats.Select(s=>s.Row).Distinct().SequenceEqual(new[]{"1","А","2"}))throw new Exception("Middle row numbering");
            var target=seats.Single(s=>s.Row=="А"&&s.Number=="2");HallEditing025.AssignLetter(seats,target,"б",false);
            if(target.Number!="Б"||!seats.Where(s=>s.Row=="А").OrderBy(s=>s.X).Select(s=>s.Number).SequenceEqual(new[]{"3","2","Б","1"})||seats.Any(s=>old[s]!=(s.X,s.Y))||!seats[2].Hidden)throw new Exception("Place lettering changed logical order or geometry");
            try{HallEditing025.AssignLetter(seats,seats.Single(s=>s.Row=="А"&&s.Number=="2"),"б",false);throw new Exception("Duplicate letter accepted");}catch(TicketException){}
        });
        run("025 logical row endpoints follow rotations with middle aisle",_=>{
            foreach(var angle in new[]{0d,90d,180d}){
                var seats=HallLayout.Generate("P",new[]{new HallRowLayout{Label="1",SeatCount=6,AisleAfter=3}},"1",false,0).ToList();HallEditing023.Rotate(seats,angle);
                var anchors=HallEditing025.RowAnchors(seats.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,null,"",null,"","",null)).ToArray());
                if(anchors.Count!=3||Math.Abs(anchors[0].X-seats[0].X)>40||Math.Abs(anchors[2].X-seats[^1].X)>40)throw new Exception("Captions lost logical endpoints");
            }
        });
        run("025 curved logical row anchors remain at own seats through rotation",_=>{
            foreach(var layout in new[]{VenueLayouts019.RussianSongSeats(),VenueLayouts019.EstradaSeats()})foreach(var angle in new[]{0d,90d,180d}){
                var draft=layout.Select(s=>new DraftSeat{Sector=s.Sector,Row=s.Row,Number=s.Number,X=s.X,Y=s.Y}).ToArray();HallEditing023.Rotate(draft,angle);
                foreach(var row in draft.Select((s,i)=>new SeatInfo(i+1,s.Sector,s.Row,s.Number,s.X,s.Y,null,"",null,"","",null)).GroupBy(s=>(s.Sector,s.Row))){
                    var anchors=HallEditing025.RowAnchors(row.ToArray());foreach(var anchor in anchors)if(row.Any(seat=>anchor.X>seat.X&&anchor.X<seat.X+16&&anchor.Y>seat.Y&&anchor.Y<seat.Y+16))throw new Exception("Caption inside own row seat: "+row.Key+" at "+angle);
                }
            }
        });
        run("025 print warning preference persists independently per blank",store=>{
            store.SuppressTicketPrintWarning(null,true);var reopened=new Store(store.Path);if(!reopened.TicketPrintWarningSuppressed(null)||reopened.TicketPrintWarningSuppressed("another"))throw new Exception("Warning suppression leaked or lost");
            reopened.SuppressTicketPrintWarning(null,false);if(new Store(store.Path).TicketPrintWarningSuppressed(null))throw new Exception("Suppression not reset");
        });
        run("025 print ordering uses saved mixed row order and natural place labels",_=>{
            var hall=new[]{("1","1"),("1","А"),("1","2"),("1","10"),("Б","1"),("2","1")}.Select((p,i)=>new SeatInfo(i+1,"P",p.Item1,p.Item2,i*20,0,null,"",null,"","",null)).ToArray();
            var tickets=hall.AsEnumerable().Reverse().Select((s,i)=>new TicketInfo(i+1,s.Id,s.Sector,s.Row,s.Number,"R","S",100-i,100,"",null)).ToArray();
            var numericHall=new[]{new SeatInfo(1,"P","10","1",0,0,null,"",null,"","",null),new SeatInfo(2,"P","2","1",0,20,null,"",null,"","",null)};
            var numericTickets=numericHall.Select(s=>new TicketInfo(s.Id,s.Id,s.Sector,s.Row,s.Number,"R","S",s.Id,100,"",null)).ToArray();
            if(!TicketPrintOrder.Order(numericTickets,numericHall).Select(t=>t.Row).SequenceEqual(new[]{"2","10"}))throw new Exception("Natural row order lost");
            var ordered=TicketPrintOrder.Order(tickets,hall);if(!ordered.Select(t=>t.SeatId).SequenceEqual(new long[]{1,2,3,4,5,6}))throw new Exception("Hall logical order lost");
            if(!TicketPrintOrder.Order(tickets).Where(t=>t.Row=="1"&&long.TryParse(t.Seat,out var numeric)).Select(t=>t.Seat).SequenceEqual(new[]{"1","2","10"}))throw new Exception("Natural sort lost");
        });
        run("025 RTL letter printing follows numbering direction after rotation and reopen",store=>{
            foreach(var angle in new[]{0d,90d,180d}){
                var seats=HallLayout.Generate("Партер",new[]{new HallRowLayout{Label="1",SeatCount=4}},"1",true,0).ToList();
                HallEditing025.AssignLetter(seats,seats.Single(s=>s.Number=="2"),"б",false);HallEditing023.Rotate(seats,angle);
                var venue=store.CreateHall("025 RTL print "+angle,"",seats);var hall=new Store(store.Path).VenueSeats(venue);
                var tickets=hall.AsEnumerable().Reverse().Select((s,i)=>new TicketInfo(i+1,s.Id,s.Sector,s.Row,s.Number,"R","RTL",100-i,100,"",null)).ToArray();
                if(!TicketPrintOrder.Order(tickets,hall).Select(t=>t.Seat).SequenceEqual(new[]{"1","Б","2","3"}))throw new Exception("RTL middle letter printed out of numbering direction at "+angle);
            }
        });
        run("025 RTL one or zero numeric labels retain durable order after arbitrary letters",store=>{
            foreach(var angle in new[]{0d,90d,180d})foreach(var allLetters in new[]{false,true}){
                var count=allLetters?4:2;var seats=HallLayout.Generate("P",new[]{new HallRowLayout{Label="1",SeatCount=count}},"1",true,0).ToList();
                var original=seats.ToDictionary(s=>s,s=>(s.X,s.Y));var logical=seats.OrderBy(s=>int.Parse(s.Number)).ToArray();
                if(allLetters){foreach(var pair in logical.Zip(new[]{"Б","Ж","А","Г"}))HallEditing025.AssignLetter(seats,pair.First,pair.Second,false);}
                else HallEditing025.AssignLetter(seats,logical[1],"Б",false);
                if(seats.Any(s=>original[s]!=(s.X,s.Y)))throw new Exception("Canonicalization moved geometry");
                HallEditing023.Rotate(seats,angle);var venue=store.CreateHall("025 durable "+allLetters+" "+angle,"",seats);var reopened=new Store(store.Path);var hall=reopened.VenueSeats(venue);
                var tickets=hall.AsEnumerable().Reverse().Select((s,i)=>new TicketInfo(i+1,s.Id,s.Sector,s.Row,s.Number,"R","D",100-i,100,"",null)).ToArray();
                var expected=allLetters?new[]{"Б","Ж","А","Г"}:new[]{"1","Б"};
                if(!TicketPrintOrder.Order(tickets,hall).Select(t=>t.Seat).SequenceEqual(expected))throw new Exception("Lost "+(allLetters?"all-letter":"one-numeric")+" logical order after "+angle+" degrees");
                var copy=reopened.CreateHall("025 durable copy "+allLetters+" "+angle,"",reopened.HallDraft(venue));var copied=reopened.VenueSeats(copy);
                var copiedTickets=copied.Select(s=>new TicketInfo(s.Id,s.Id,s.Sector,s.Row,s.Number,"R","D",s.Id,100,"",null)).ToArray();
                if(!TicketPrintOrder.Order(copiedTickets,copied).Select(t=>t.Seat).SequenceEqual(expected))throw new Exception("Hall copy lost durable order");
            }
        });
        foreach(var row in new[]{false,true})run("025 uppercase letter rejects lowercase "+(row?"row":"place")+" atomically",_=>{
                var seats=HallLayout.Generate("P",new[]{new HallRowLayout{Label="а",SeatCount=3},new HallRowLayout{Label="1",SeatCount=3}},"1",false,0).ToList();if(!row)seats[0].Number="а";
                var target=row?seats[3]:seats[1];var before=seats.Select(s=>(s.Row,s.Number,s.X,s.Y,s.Hidden)).ToArray();var rejected=false;
                try{HallEditing025.AssignLetter(seats,target,"А",row);}catch(TicketException){rejected=true;}
                if(!rejected||!before.SequenceEqual(seats.Select(s=>(s.Row,s.Number,s.X,s.Y,s.Hidden))))throw new Exception("Case-insensitive duplicate "+(row?"row":"place")+" accepted or changed hall");
        });
        run("025 source TTN preview XML equals original package XML",store=>{
            var show=store.Events().First(e=>store.Seats(e.Id).Any(s=>s.Available));var seat=store.Seats(show.Id).First(s=>s.Available);
            var issue=store.Issue(show.Id,new[]{seat.Id},"025 TTN","T025",1);var info=store.GetIssueDocument(show.Id,issue,IssueDocumentKinds.Waybill);var bytes=info.SourceDocument??throw new Exception("No TTN source");var copy=bytes.ToArray();
            using var zip=new System.IO.Compression.ZipArchive(new MemoryStream(bytes),System.IO.Compression.ZipArchiveMode.Read);using var input=zip.GetEntry("word/document.xml")!.Open();var original=System.Xml.Linq.XDocument.Load(input);
            var xml=InvoiceDocuments.IssueDocumentXml(info);if(!System.Xml.Linq.XNode.DeepEquals(xml,original)||!bytes.SequenceEqual(copy))throw new Exception("TTN content changed");
        });
        run("025 sender confirmation rejects change restores choice and avoids menu open prompt",_=>{
            var calls=0;var current="ООО А";
            if(SenderChange025.Accept(current,current,()=>{calls++;return false;})!=current||calls!=0)throw new Exception("Unchanged sender prompted");
            if(SenderChange025.Accept(current,"ООО Б",()=>{calls++;return false;})!=current||calls!=1)throw new Exception("Rejection did not restore sender");
            if(SenderChange025.Accept("","ООО Б",()=>throw new Exception("Initial choice prompted"))!="ООО Б")throw new Exception("Initial choice changed");
        });
    }
}
