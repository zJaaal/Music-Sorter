using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;

namespace MusicSorter
{
    public static class MusicSorter
    {
        static List<string> Allowed = new List<string>() { "mp3", "flac", "m4a", "wav"};
        public static List<string> MapFiles(string directory)
        {
            List<string> files = new List<string>();
            try
            {
                Log.Information("Mapping Files...");
                files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                                 .Where(s => Allowed.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
                                 .ToList();

                Log.Information(files.Count.ToString() + " files found");
            }
            catch(Exception ex)
            {
                Log.Fatal(ex.ToString());
            }

            return files;
        }

        public static async Task<List<Track>> MapTracks(List<string> files)
        {
            List<Track> allTracks = new();
            List<Task> tasks = new();
            int listDivision = files.Count() / 4;

            List<List<string>> filesChunks = files.ChunkBy<string>(listDivision);

            Log.Information("Mapping Tracks...");
            foreach (List<string> listFile in filesChunks)
            {
                tasks.Add(Task.Run(() =>
                {
                    foreach (string file in listFile)
                    {
                        var tfile = TagLib.File.Create(file);

                        lock (allTracks)
                            allTracks.Add(new Track(tfile.Tag.Album, tfile.Tag.JoinedArtists, tfile.Tag.Title, Path.GetFullPath(file)));
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());
            Log.Information($"{allTracks.Count()} tracks Mapped");

            return allTracks;
        }

        public static async Task<List<string>> MapArtists(List<Track> tracks, string mainDirectory)
        {
            List<List<Track>> tracksByChunk = tracks.ChunkBy<Track>(tracks.Count() / 4);
            List<string> artists = new();
            List<Task> tasks = new();

            Log.Verbose("Mapping Artists");
            foreach(List<Track> trackslist in tracksByChunk)
            {
                tasks.Add(Task.Run(() =>
                {
                    foreach (Track track in trackslist)
                    {
                        track.NewDirectory = mainDirectory + track.Artist + "//";

                        if (track.Artist is null)
                            track.Artist = "Unknown";

                        if (artists.Contains(track.Artist))
                            continue;

                        lock(artists)
                        artists.Add(track.Artist);

                        CreateArtistDirectory(mainDirectory, track.Artist);
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
            Log.Information($"{artists.Count()} artists found");

            return artists;
        }
        public static async Task<Dictionary<string, List<Track>>> SortTracksByArtist(List<Track> tracks, List<string> artist)
        {
            Dictionary<string, List<Track>> artistsDictionary = new();
            List<List<string>> artistByChunk = artist.ChunkBy<string>(artist.Count() / 4);
            List<Task> tasks = new();

            Log.Verbose("Sorting tracks by artist...");
            foreach (List<string> artistList  in artistByChunk)
            {
                tasks.Add(Task.Run(() =>
                {
                    foreach (string artist in artistList)
                    {
                        Log.Verbose($"Sorting {artist}");
                        List<Track> tracksList = tracks.Where(x => x.Artist.Equals(artist)).ToList();
                        Log.Verbose($"{tracksList.Count()} songs found");

                        lock (artistsDictionary)
                            artistsDictionary.Add(artist,tracksList);
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());

            return artistsDictionary;
        }
        public static void CreateArtistDirectory(string mainDirectory, string artist)
        {
            if (artist.Contains("\""))
                artist = artist.Replace('\"', ' ');

            Directory.CreateDirectory(mainDirectory + artist + "//");
        }
    }
}
