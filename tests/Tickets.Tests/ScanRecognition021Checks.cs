using Tickets.Core;

internal static class ScanRecognition021Checks
{
    public static void Run()
    {
        const int width=220,height=120;var image=Enumerable.Repeat((byte)255,width*height).ToArray();
        // Synthetic outlines only: no reference to any real venue scan or stored hall geometry.
        foreach(var y in new[]{20,55,90})foreach(var x in new[]{20,50,80,110,155})
        {
            for(var k=0;k<12;k++)
            {image[y*width+x+k]=0;image[(y+11)*width+x+k]=0;image[(y+k)*width+x]=0;image[(y+k)*width+x+11]=0;}
        }
        // Isolated rectangle and narrow lettering must not join the repeated chair family.
        for(var k=0;k<4;k++)for(var j=0;j<12;j++)image[(5+j)*width+190+k]=0;
        var found=ScanChairRecognition021.Detect(image,width,height);
        Require(found.Count==15,"recognizes 15 actual synthetic chair outlines, excluding narrow text");
        Require(found.Any(c=>c.X==160.5&&c.Y==95.5),"retains detected pixel geometry and aisle");
        Require(ScanChairRecognition021.Detect(Enumerable.Repeat((byte)255,width*height).ToArray(),width,height).Count==0,"blank scan produces zero fabricated seats");
        Require(ScanChairRecognition021.Detect(new byte[width*height],width,height).Count==0,"one filled background is not interpreted as a hall");
        try{ScanChairRecognition021.Detect(new byte[1],100,100);throw new Exception("Invalid image length accepted");}catch(TicketException){}
        try{ScanChairRecognition021.Detect([],3000,3000);throw new Exception("Unbounded image accepted");}catch(TicketException){}
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
