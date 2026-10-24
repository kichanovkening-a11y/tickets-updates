using System.Runtime.InteropServices;

namespace Tickets.App;

internal sealed record TicketlandLogin028(string Username,string Password);

internal static class TicketlandCredentials028
{
    private const uint Generic=1,LocalMachine=2;
    private const int NotFound=1168,MaxBlobBytes=2560,MaxUsernameChars=513;

    internal static TicketlandLogin028? Read(string connectionId)
    {
        var target=Target(connectionId);
        if(!CredRead(target,Generic,0,out var pointer))
        {
            if(Marshal.GetLastWin32Error()==NotFound)return null;
            throw Failure("прочитать");
        }
        var credential=default(Credential);
        try
        {
            credential=Marshal.PtrToStructure<Credential>(pointer);
            if(credential.CredentialBlobSize>MaxBlobBytes||credential.CredentialBlobSize%2!=0||credential.CredentialBlobSize>0&&credential.CredentialBlob==IntPtr.Zero)
                throw Failure("прочитать");
            var username=Marshal.PtrToStringUni(credential.UserName)??"";
            var password=credential.CredentialBlobSize==0?"":Marshal.PtrToStringUni(credential.CredentialBlob,(int)credential.CredentialBlobSize/2)!;
            return new(username,password);
        }
        finally
        {
            if(credential.CredentialBlobSize<=MaxBlobBytes)Clear(credential.CredentialBlob,(int)credential.CredentialBlobSize);
            CredFree(pointer);
        }
    }

    internal static void Save(string connectionId,TicketlandLogin028 login)
    {
        var target=Target(connectionId);
        if(login is null||login.Username is null||login.Password is null||login.Username.Length>MaxUsernameChars||login.Username.Contains('\0')||login.Password.Length>MaxBlobBytes/2)
            throw new ArgumentException("Недопустимые данные для сохранения входа.",nameof(login));
        IntPtr targetBuffer=IntPtr.Zero,usernameBuffer=IntPtr.Zero,passwordBuffer=IntPtr.Zero;
        try
        {
            targetBuffer=Marshal.StringToHGlobalUni(target);
            usernameBuffer=Marshal.StringToHGlobalUni(login.Username);
            passwordBuffer=Marshal.StringToHGlobalUni(login.Password);
            var credential=new Credential{Type=Generic,TargetName=targetBuffer,UserName=usernameBuffer,CredentialBlob=passwordBuffer,CredentialBlobSize=(uint)(login.Password.Length*2),Persist=LocalMachine};
            if(!CredWrite(ref credential,0))throw Failure("сохранить");
        }
        finally
        {
            ClearAndFree(passwordBuffer,login.Password.Length*2+2);
            ClearAndFree(usernameBuffer,login.Username.Length*2+2);
            if(targetBuffer!=IntPtr.Zero)Marshal.FreeHGlobal(targetBuffer);
        }
    }

    internal static void Delete(string connectionId)
    {
        var target=Target(connectionId);
        if(!CredDelete(target,Generic,0)&&Marshal.GetLastWin32Error()!=NotFound)throw Failure("удалить");
    }

    private static string Target(string connectionId)
    {
        if(!Guid.TryParse(connectionId,out var id))throw new ArgumentException("Недопустимый идентификатор подключения.",nameof(connectionId));
        return "Tickets.Ticketland."+id.ToString("D");
    }
    private static InvalidOperationException Failure(string operation)=>new("Не удалось "+operation+" сохранённые данные входа Windows.");
    private static void Clear(IntPtr pointer,int bytes)
    {
        if(pointer==IntPtr.Zero)return;
        for(var offset=0;offset<bytes;offset++)Marshal.WriteByte(pointer,offset,0);
    }
    private static void ClearAndFree(IntPtr pointer,int bytes)
    {
        if(pointer==IntPtr.Zero)return;
        Clear(pointer,bytes);Marshal.FreeHGlobal(pointer);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Credential
    {
        public uint Flags,Type;
        public IntPtr TargetName,Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist,AttributeCount;
        public IntPtr Attributes,TargetAlias,UserName;
    }
    [DllImport("advapi32.dll",EntryPoint="CredReadW",ExactSpelling=true,CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target,uint type,uint flags,out IntPtr credential);
    [DllImport("advapi32.dll",EntryPoint="CredWriteW",ExactSpelling=true,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential,uint flags);
    [DllImport("advapi32.dll",EntryPoint="CredDeleteW",ExactSpelling=true,CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target,uint type,uint flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credential);
}
