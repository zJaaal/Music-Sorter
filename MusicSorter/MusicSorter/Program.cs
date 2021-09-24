using Serilog;
using Serilog.Sinks;
using System.Diagnostics;

namespace MusicSorter;
public class Program
{
    private static string RootDirectory { get; set; }
    private static string NewRootDirectory { get; set; }
    private static Dictionary<string, List<Track>> ArtistDictionary { get; set; }
    private static List<string> AllFiles {  get; set; }
    private static List<Track> AllTracks { get; set; }
    private static List<string> AllArtists { get; set; }

    static async Task Main(string[] args)
    {
        Stopwatch sw = new Stopwatch();

        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console();
        Log.Logger = logger.CreateLogger();

        Log.Verbose("Welcome to Music Sorter! v0.1.0");
        Log.Verbose("This program will read every ID3 Tag in your music and will sort it by artists and their songs by album.");
        Log.Verbose("In order to scan your music. Please enter the root directory where all your tracks are.");

        do
        {
            RootDirectory = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(RootDirectory))
                Log.Warning("Please enter a Directory");

        } while (string.IsNullOrWhiteSpace(RootDirectory));

        sw.Start();

        Log.Verbose("The program will scan " + RootDirectory + @"\" + " looking for music.");
        RootDirectory +=  @"\";

        AllFiles = MusicSorter.MapFiles(RootDirectory);

        try
        {
            if(!Directory.Exists(NewRootDirectory = RootDirectory + @"SortedMusic\"))
                Directory.CreateDirectory(NewRootDirectory);
        }
        catch(Exception e)
        {
            Console.WriteLine("The entered directory could be invalid:" + e.ToString());
        }

       AllTracks = await MusicSorter.MapTracks(AllFiles);
       AllArtists = await MusicSorter.MapArtists(AllTracks, NewRootDirectory);
       ArtistDictionary = await MusicSorter.SortTracksByArtist(AllTracks, AllArtists);
       await MusicSorter.SortByAlbum(ArtistDictionary, AllArtists);

        sw.Stop();
        TimeSpan ts = sw.Elapsed;

        string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);

        Log.Verbose("Completed in: {0}",elapsedTime);
    }
}
