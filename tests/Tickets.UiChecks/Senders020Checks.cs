using System.Reflection;
using System.Windows.Controls;
using Tickets.App;
using Tickets.Core;

internal static class Senders020Checks
{
    internal static void Run(Store store,MainWindow main)
    {
        var senderBox=(ComboBox)main.FindName("SenderBox");
        var previous=senderBox.Text;
        const string removed="UI020 удалённый отправитель";
        store.SaveSender(removed);senderBox.Text=removed;store.RemoveSenderChoice(removed);
        typeof(MainWindow).GetMethod("PersistSelectedSender",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null);
        if(senderBox.Text==removed||!store.Senders().Contains(senderBox.Text))throw new Exception("stale deleted sender blocks using an active sender");
        if(new Store(store.Path).Senders().Contains(removed))throw new Exception("ordinary issuance restored a removed sender");
        typeof(MainWindow).GetMethod("SenderDropDownOpened",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[senderBox,EventArgs.Empty]);
        if(senderBox.Items.Cast<string>().Contains(removed)||senderBox.Text==removed)throw new Exception("reopened sender dropdown still offers a removed sender");
        senderBox.Text=previous;
        Console.WriteLine("Senders v0.20: stale issuance selection cannot restore a deleted sender.");
    }
}
