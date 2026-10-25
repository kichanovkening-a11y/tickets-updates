using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Tickets.App;
using Tickets.Core;

internal static class HallConflicts031UiChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Store store)
    {
        var name="Конфликт031 "+Guid.NewGuid().ToString("N");
        var venue=store.CreateHall(name,"",Enumerable.Range(1,3).Select(n=>new DraftSeat{Sector="Партер",Row="1",Number=n.ToString(),X=n*40,Y=40}).ToArray());
        var ids=store.VenueSeats(venue).OrderBy(s=>s.Number).Select(s=>s.Id).ToArray();
        var plan=store.SavePlan(venue,name,ids.Select(id=>new PriceValue(id,500,store.PriceColors().FirstOrDefault(p=>p.Amount==500)?.Color??PricePalette.NextAvailable(store.PriceColors().Select(p=>p.Color)))).ToArray());
        var at=DateTime.Today.AddDays(71);var evt=store.CreateEvent(store.SavePerformance(name),venue,plan,at);
        store.Issue(evt,[ids[0]],"Получатель конфликта","CF031",1);
        var fetched=(store.TicketlandLastSync028??DateTimeOffset.UtcNow).AddSeconds(1);
        store.ApplyTicketlandReport028(new([new(name,at,"Партер","1","1",name+"1",700){Venue=name},new(name,at,"Партер","1","2",name+"2",800){Venue=name}],fetched),true);
        var hall=new HallView();var window=new Window{Content=hall,Width=650,Height=400};window.Show();Pump();
        try
        {
            var seats=new Store(store.Path).Seats(evt);hall.Load(seats,store.Sectors(venue),true);hall.SetSelection([ids[2]]);Pump();
            var timer=(DispatcherTimer)typeof(HallView).GetField("conflictTimer031",Private)!.GetValue(hall)!;
            Check(timer.IsEnabled,"conflict starts animation after cached sale load");
            var initial=Capture(hall);var deadline=DateTime.UtcNow.AddSeconds(2);
            var phase=Phase(hall);while(Phase(hall)==phase&&DateTime.UtcNow<deadline){Pump();Thread.Sleep(10);}Pump();
            var flashed=Capture(hall);Check(!initial.SequenceEqual(flashed),"dispatcher timer changes rendered conflict pixels");
            var first=seats.Single(s=>s.Id==ids[0]);var second=seats.Single(s=>s.Id==ids[1]);
            Check(Pixel(hall,initial,first)!=Pixel(hall,flashed,first),"issued and external sold seat flashes");
            Check(Pixel(hall,initial,second)==Pixel(hall,flashed,second),"external-only sold seat stays steady");
            var point=(Point)typeof(HallView).GetMethod("Screen",Private)!.Invoke(hall,[new Point(first.X+8,first.Y+8)])!;
            typeof(HallView).GetMethod("UpdateTip",Private)!.Invoke(hall,[point]);Pump();
            var tip=(ToolTip)typeof(HallView).GetField("seatTip",Private)!.GetValue(hall)!;
            Check(tip.IsOpen&&tip.Content.ToString()!.Contains("Конфликт")&&tip.Content.ToString()!.Contains("Ticketland")&&tip.Content.ToString()!.Contains("Получатель конфликта"),"hover explains double occupation and local recipient");
            Check(hall.Selected.SetEquals([ids[2]])&&store.Seats(evt).SequenceEqual(seats),"animation and hover do not alter selection or accounting");
            var folder=Environment.GetEnvironmentVariable("TICKETS_UI_CAPTURE_DIR");if(!string.IsNullOrWhiteSpace(folder)){Directory.CreateDirectory(folder);var bitmap=Bitmap(hall);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(folder,"031-ticketland-conflict.png"));encoder.Save(file);}
            window.Hide();Pump();Check(!timer.IsEnabled,"hidden hall stops animation");window.Show();Pump();Check(timer.IsEnabled,"visible hall resumes animation");
            store.ApplyTicketlandReport028(new([],fetched.AddSeconds(1)),true);hall.Load(store.Seats(evt),store.Sectors(venue),false);Pump();
            Check(!timer.IsEnabled&&!Phase(hall)&&store.Seats(evt).Single(s=>s.Id==ids[0]).TicketId.HasValue,"external return stops flashing and keeps local issue");
        }
        finally{window.Close();Pump();}
    }
    private static bool Phase(HallView hall)=>(bool)typeof(HallView).GetField("conflictFlash031",Private)!.GetValue(hall)!;
    private static RenderTargetBitmap Bitmap(HallView hall){var bitmap=new RenderTargetBitmap((int)hall.ActualWidth,(int)hall.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(hall);return bitmap;}
    private static byte[] Capture(HallView hall){var bitmap=Bitmap(hall);var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);return bytes;}
    private static int Pixel(HallView hall,byte[] bytes,SeatInfo seat){var p=(Point)typeof(HallView).GetMethod("Screen",Private)!.Invoke(hall,[new Point(seat.X+4,seat.Y+4)])!;var offset=((int)p.Y*(int)hall.ActualWidth+(int)p.X)*4;return BitConverter.ToInt32(bytes,offset);}
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS 031 "+message);}
}
