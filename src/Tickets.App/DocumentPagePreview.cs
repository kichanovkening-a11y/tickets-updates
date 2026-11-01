using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using Tickets.Core;

namespace Tickets.App;

internal sealed class DocumentPagePreview:UserControl
{
    private readonly TextBlock status=new(){FontSize=12,Margin=new Thickness(5),TextWrapping=TextWrapping.Wrap};
    private readonly ContentControl viewer=new();
    private byte[]? source;
    private Func<double,FlowDocument>? fallback;
    private WordPageLease? lease;
    private CancellationTokenSource? pending;
    internal DocumentPagePreview()
    {
        var root=new DockPanel();Content=root;DockPanel.SetDock(status,Dock.Top);root.Children.Add(status);root.Children.Add(viewer);
        Loaded+=(_,_)=>RefreshPages();Unloaded+=(_,_)=>Release();
        SizeChanged+=(_,_)=>{if(!WordDocumentPages.Available&&source is not null)ShowFallback();};
    }
    internal void SetDocument(IssueDocumentInfo info,Func<double,FlowDocument> portable)=>SetDocument(Bytes(info),portable);
    internal void SetDocument(byte[] bytes,Func<double,FlowDocument> portable)
    {
        fallback=portable;
        if(source is not null&&source.AsSpan().SequenceEqual(bytes))return;
        source=bytes.ToArray();Release();
        if(!WordDocumentPages.Available)ShowFallback();else{status.Text="Подготовка страниц Word…";if(IsLoaded)RefreshPages();}
    }
    internal static byte[] Bytes(IssueDocumentInfo info)
    {
        if(info.SourceDocument is {} bytes)return info.Kind==IssueDocumentKinds.Contract?ContractPagination.Prepare(bytes):bytes;
        var path=Path.Combine(Path.GetTempPath(),"Tickets-view-"+Guid.NewGuid().ToString("N")+".docx");
        try{InvoiceDocuments.SaveIssueDocumentWord(info,path);return File.ReadAllBytes(path);}
        finally{if(File.Exists(path))File.Delete(path);}
    }
    private void ShowFallback()
    {
        if(fallback is null)return;
        status.Text="Просмотр без Word: переносы страниц могут отличаться.";
        viewer.Content=new FlowDocumentScrollViewer{Document=fallback(ActualWidth>100?ActualWidth-20:794),IsToolBarVisible=false,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    }
    private async void RefreshPages()
    {
        if(source is null)return;
        Release();if(!WordDocumentPages.Available){ShowFallback();return;}
        var request=pending=new CancellationTokenSource();var bytes=source;
        status.Text="Подготовка страниц Word…";
        try
        {
            // Briefly coalesce navigation/metadata changes before starting Office.
            await Task.Delay(150,request.Token);
            var path=await WordDocumentPages.RenderAsync(bytes).WaitAsync(request.Token);
            if(request.IsCancellationRequested||!IsLoaded)return;
            lease=new WordPageLease(path);
            var pages=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};
            for(var i=0;i<lease.Pages.Pages.Count;i++)pages.Children.Add(new DocumentPageView{DocumentPaginator=lease.Pages.DocumentPaginator,PageNumber=i,Margin=new Thickness(0,0,0,10)});
            viewer.Content=new ScrollViewer{Content=pages,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
            status.Text="Макет Word · Страниц: "+lease.Pages.Pages.Count;
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(!request.IsCancellationRequested)status.Text=ex.Message+" Повторите открытие вкладки «Документ».";}
    }
    private void Release()
    {
        pending?.Cancel();pending?.Dispose();pending=null;viewer.Content=null;lease?.Dispose();lease=null;
    }
}
