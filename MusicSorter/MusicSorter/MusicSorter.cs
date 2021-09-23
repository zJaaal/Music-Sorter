using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TagLib;

namespace MusicSorter
{
    public static class MusicSorter
    {
        static List<string> Allowed = new List<string>() { "mp3", "flac", "m4a", "wav"};
        private static readonly object _Lock = new object();
        private static Regex regex = new Regex(@"[\\/:*""<>\|]");
        public static List<string> MapFiles(string directory)
        {
            List<string> files = new List<string>();
            try
            {
                Log.Information("Mapping Files...");
                files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
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
            if(files.Count() >= 4)
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
                            try
                            {
                                var tfile = TagLib.File.Create(file);
                                lock (allTracks)
                                    allTracks.Add(new Track(tfile.Tag.Album, tfile.Tag.JoinedArtists, tfile.Tag.Title, Path.GetFullPath(file)));
                            }
                            catch (TagLib.CorruptFileException ex)
                            {
                                Log.Error($"ID3 Tag not found for: {Path.GetFileName(file)}");
                                continue;
                            }
                        }
                    }));
                }

                Task.WaitAll(tasks.ToArray());
                Log.Information($"{allTracks.Count()} tracks Mapped");

                return allTracks;
            }
            else
            {
                List<Track> allTracks = new();
                foreach (string file in files)
                {
                    var tfile = TagLib.File.Create(file);

                    allTracks.Add(new Track(tfile.Tag.Album.Trim(), tfile.Tag.JoinedArtists.Trim(), tfile.Tag.Title.Trim(), Path.GetFullPath(file)));
                }

                return allTracks;
            }
            
        }

        public static async Task<List<string>> MapArtists(List<Track> tracks, string mainDirectory)
        {
            if(tracks.Count() >= 4)
            {
                List<List<Track>> tracksByChunk = tracks.ChunkBy<Track>(tracks.Count() / 4);
                List<string> artists = new();
                List<Task> tasks = new();

                Log.Verbose("Mapping Artists...");
                foreach (List<Track> trackslist in tracksByChunk)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        foreach (Track track in trackslist)
                        {

                            if (track.Artist is null)
                                track.Artist = "Unknown";

                            track.NewDirectory = CreateArtistDirectory(mainDirectory, track.Artist);

                            if (artists.Contains(track.Artist))
                                continue;

                            lock (artists)
                                artists.Add(track.Artist);

                        }
                    }));
                }
                Task.WaitAll(tasks.ToArray());
                Log.Information($"{artists.Count()} artists found");

                return artists;
            }
            else
            {
                List<string> artists = new();
                foreach (Track track in tracks)
                {

                    if (track.Artist is null)
                        track.Artist = "Unknown";

                    track.Artist = CreateArtistDirectory(mainDirectory, track.Artist);

                    if (artists.Contains(track.Artist))
                        continue;

                        artists.Add(track.Artist);

                }
                return artists;
            }
            
        }
        public static async Task<Dictionary<string, List<Track>>> SortTracksByArtist(List<Track> tracks, List<string> artists)
        {
            if(artists.Count() >= 4)
            {
                Dictionary<string, List<Track>> artistsDictionary = new();
                List<List<string>> artistByChunk = artists.ChunkBy<string>(artists.Count() / 4);
                List<Task> tasks = new();

                Log.Verbose("Sorting tracks by artist...");
                foreach (List<string> artistList in artistByChunk)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        foreach (string artist in artistList)
                        {
                            List<Track> tracksList = tracks.Where(x => x.Artist.Equals(artist)).ToList();

                            lock (artistsDictionary)
                            {
                                if (artistsDictionary.ContainsKey(artist))
                                {
                                    artistsDictionary[artist].AddRange(tracksList);
                                }
                                else
                                    artistsDictionary.Add(artist, tracksList);
                            }
                        }
                    }));
                }
                Task.WaitAll(tasks.ToArray());

                return artistsDictionary;
            }
            else
            {
                Dictionary<string, List<Track>> artistsDictionary = new();

                foreach (string artist in artists)
                {
                    List<Track> tracksList = tracks.Where(x => x.Artist.Equals(artist)).ToList();

                    if (artistsDictionary.ContainsKey(artist))
                    {
                        artistsDictionary[artist].AddRange(tracksList);
                    }
                    else
                        artistsDictionary.Add(artist, tracksList);
                }
                return artistsDictionary;
            }
            
        }
        public static async Task SortByAlbum(Dictionary<string, List<Track>> artistDic, List<string> artists) 
        {
            SemaphoreSlim semaphoreSlim = new(4);
            List<Task> tasks = new();
            Log.Verbose("Sorting Tracks by Album...");
            foreach(string a in artists)
            {
                tasks.Add(Task.Run(async () => {

                    await semaphoreSlim.WaitAsync();

                    foreach (Track t in artistDic[a])
                    {
                        Log.Verbose($"Sorting {a} {t.Album}");
                        try
                        {
                            if (t.Album is null)
                                t.Album = "Unknown";

                            t.NewDirectory = CreateAlbumDirectory(t.NewDirectory, t.Album);

                            if (t.NewDirectory is null)
                                continue;

                            lock (_Lock)
                                System.IO.File.Move(t.CurrentDirectory, t.NewDirectory + Path.GetFileName(t.CurrentDirectory));
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex.ToString());
                            Log.Error(Path.GetFileName(t.CurrentDirectory));
                        }
                        finally
                        {
                            semaphoreSlim.Release();
                        }
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
        }
        public static string CreateArtistDirectory(string mainDirectory, string artist)
        {
            if (regex.IsMatch(artist))
                artist = regex.Replace(artist, "");

            if (Directory.Exists(mainDirectory + artist + "//"))
                return mainDirectory + artist + "//";

            Directory.CreateDirectory(mainDirectory + artist + "//");

            return mainDirectory + artist + "//";
        }
        public static string CreateAlbumDirectory(string mainDirectory, string album)
        {
            if (regex.IsMatch(album))
                album = regex.Replace(album, "");

            if (string.IsNullOrEmpty(album))
                album = "Unknown";

            if (Directory.Exists(mainDirectory + album + "//"))
                return mainDirectory + album + "//";

            Directory.CreateDirectory(mainDirectory + album + "//");

            return mainDirectory + album + "//";
        }
    }
}
