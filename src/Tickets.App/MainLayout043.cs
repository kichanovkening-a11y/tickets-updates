using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Tickets.App;

public partial class MainWindow
{
    private double hallHeaderHeight044=32;
    private void HallHeaderSizeChanged044(object sender,SizeChangedEventArgs e)
    {
        hallHeaderHeight044=((FrameworkElement)sender).ActualHeight;
        Resources["HallIconsMargin044"]=new Thickness(0,Math.Max(0,hallHeaderHeight044-32),5/CompactText043.PhysicalScale((Visual)Content).X,0);
    }
    private Vector spacingScale043;
    private double sidebarPreferredWidth043=250;
    private bool sidebarDragging043;
    private string SidebarWidthPath043=>Path.Combine(Path.GetDirectoryName(store.Path)!,"hall-sidebar-width.txt");
    private void InitializeLayout043()
    {
        try
        {
            if(File.Exists(SidebarWidthPath043)&&double.TryParse(File.ReadAllText(SidebarWidthPath043),NumberStyles.Float,CultureInfo.InvariantCulture,out var width)&&double.IsFinite(width))
                sidebarPreferredWidth043=Math.Clamp(width,200,500);
        }
        catch(IOException){}catch(UnauthorizedAccessException){}
        Workspace.ColumnDefinitions[2].Width=new GridLength(sidebarPreferredWidth043);
        SidebarSplitter043.DragStarted+=(_,_)=>sidebarDragging043=true;
        SidebarSplitter043.DragCompleted+=(_,e)=>{sidebarDragging043=false;if(!e.Canceled)SaveSidebarWidth043();else ApplySidebarWidth043();};
        SidebarSplitter043.AddHandler(KeyDownEvent,new KeyEventHandler((_,e)=>{
            if(e.Key is Key.Left or Key.Right)Dispatcher.BeginInvoke(new Action(SaveSidebarWidth043));
        }),true);
        Loaded+=(_,_)=>{RefreshPhysicalSpacing043();ApplySidebarWidth043();};
        LayoutUpdated+=(_,_)=>RefreshPhysicalSpacing043();
        SizeChanged+=(_,_)=>ApplySidebarWidth043();
        RefreshPhysicalSpacing043();
    }
    private void RefreshPhysicalSpacing043()
    {
        var scale=CompactText043.PhysicalScale((Visual)Content);
        if(Math.Abs(scale.X-spacingScale043.X)<.001&&Math.Abs(scale.Y-spacingScale043.Y)<.001)return;
        spacingScale043=scale;
        Thickness Insets(double left,double top,double right,double bottom)=>new(left/scale.X,top/scale.Y,right/scale.X,bottom/scale.Y);
        Resources["Px5Inset043"]=Insets(5,5,5,5);
        Resources["Px4Inset043"]=Insets(4,4,4,4);
        Resources["Px2Inset043"]=Insets(2,2,2,2);
        Resources["Px1Border043"]=Insets(1,1,1,1);
        Resources["Px3Border043"]=Insets(3,3,3,3);
        Resources["Px3Bottom043"]=Insets(0,0,0,3);
        Resources["Px5Bottom043"]=Insets(0,0,0,5);
        Resources["Px5Top043"]=Insets(0,5,0,0);
        Resources["Px5Right043"]=Insets(0,0,5,0);
        Resources["Px5Left043"]=Insets(5,0,0,0);
        Resources["BannerPadding043"]=new Thickness(20,5/scale.Y,20,5/scale.Y);
        Resources["WorkspaceMargin043"]=new Thickness(12,5/scale.Y,12,5/scale.Y);
        Resources["EventHeaderMargin043"]=Insets(5,0,0,5);
        Resources["Px3Top044"]=Insets(0,3,0,0);
        Resources["Px4Top044"]=Insets(0,4,0,0);
        Resources["Px10Top044"]=Insets(0,10,0,0);
        Resources["HallButtonPadding044"]=Insets(4,9,4,9);
        Resources["HallIconSize043"]=32d;
        Resources["HallIconsMargin044"]=new Thickness(0,Math.Max(0,hallHeaderHeight044-32),5/scale.X,0);
        Resources["HallHeaderReserve043"]=new Thickness(0,0,64+15/scale.X,5/scale.Y);
        // Text line gaps also depend on DPI; invalidate visible compact text after a monitor change.
        InvalidateMeasure();
    }
    private void ApplySidebarWidth043()
    {
        if(Workspace.ActualWidth<=0)return;
        var max=Math.Max(200,Math.Min(500,Workspace.ActualWidth-6-220));
        // GridSplitter honors the definition's maximum during both mouse and
        // keyboard resizing; constrain the gesture itself, not just restoration.
        Workspace.ColumnDefinitions[2].MaxWidth=max;
        if(sidebarDragging043)return;
        var width=Math.Min(sidebarPreferredWidth043,max);
        if(Math.Abs(Workspace.ColumnDefinitions[2].Width.Value-width)>.1)Workspace.ColumnDefinitions[2].Width=new GridLength(width);
    }
    private void SaveSidebarWidth043()
    {
        sidebarPreferredWidth043=Math.Clamp(Workspace.ColumnDefinitions[2].ActualWidth,200,500);
        ApplySidebarWidth043();
        try
        {
            File.WriteAllText(SidebarWidthPath043+".tmp",sidebarPreferredWidth043.ToString("R",CultureInfo.InvariantCulture));
            File.Move(SidebarWidthPath043+".tmp",SidebarWidthPath043,true);
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException)
        {StatusText.Text="Не удалось сохранить ширину правого блока: "+ex.Message;}
    }
}
