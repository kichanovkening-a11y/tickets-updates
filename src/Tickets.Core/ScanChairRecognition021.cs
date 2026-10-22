namespace Tickets.Core;

/// <summary>Experimental chair-outline candidates only. Does not perform OCR or infer verified seat identities.</summary>
public static class ScanChairRecognition021
{
    public sealed record Candidate(double X,double Y,int Width,int Height);
    public static IReadOnlyList<Candidate> Detect(byte[] gray,int width,int height,byte threshold=242)
    {
        if(width<1||height<1||(long)width*height>4000000||gray.Length!=(long)width*height)
            throw new TicketException("Скан должен содержать не более 4 миллионов пикселей после уменьшения.");
        var seen=new bool[gray.Length];var queue=new Queue<int>();var components=new List<Candidate>();
        for(var origin=0;origin<gray.Length;origin++)
        {
            if(seen[origin]||gray[origin]<threshold)continue;
            seen[origin]=true;queue.Enqueue(origin);var minX=width;var minY=height;var maxX=0;var maxY=0;var ink=0;
            while(queue.TryDequeue(out var p))
            {
                var x=p%width;var y=p/width;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);ink++;
                Visit(x-1,y);Visit(x+1,y);Visit(x,y-1);Visit(x,y+1);
            }
            var w=maxX-minX+1;var h=maxY-minY+1;var fill=(double)ink/(w*h);
            // Enclosed bright interiors detect actual chair-cell outlines, even when neighboring
            // outlines share strokes. Dark glyph components alone are never chair evidence.
            if(minX>0&&minY>0&&maxX<width-1&&maxY<height-1&&w>=8&&h>=8&&w<=100&&h<=100&&w/(double)h is >=.65 and <=1.55&&fill>=.35)
                components.Add(new Candidate((minX+maxX)/2.0,(minY+maxY)/2.0,w,h));
        }
        if(components.Count<3)return [];
        if(components.Count>5000)throw new TicketException("Слишком много мелких контуров. Выберите более чистый или обрезанный скан.");
        // Repeated similarly sized outlines are stronger evidence than isolated marks or text glyphs.
        var group=components.GroupBy(c=>(W:(int)Math.Round(c.Width/4.0),H:(int)Math.Round(c.Height/4.0)))
            .OrderByDescending(g=>g.Count()*Math.Sqrt(g.Average(c=>c.Width*c.Height))).First();
        var typicalW=group.Average(c=>c.Width);var typicalH=group.Average(c=>c.Height);
        var repeated=components.Where(c=>Math.Abs(c.Width-typicalW)<=Math.Max(2,typicalW*.25)&&Math.Abs(c.Height-typicalH)<=Math.Max(2,typicalH*.25)).ToArray();
        return repeated.Where(c=>repeated.Count(other=>other!=c&&Math.Abs(other.Y-c.Y)<=typicalH*.7&&Math.Abs(other.X-c.X)<=typicalW*12)>=2)
            .OrderBy(c=>c.Y).ThenBy(c=>c.X).Take(50000).ToArray();
        void Visit(int x,int y)
        {
            if(x<0||x>=width||y<0||y>=height)return;
            var p=y*width+x;if(seen[p]||gray[p]<threshold)return;seen[p]=true;queue.Enqueue(p);
        }
    }
}
