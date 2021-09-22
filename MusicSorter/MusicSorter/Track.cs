namespace MusicSorter
{
    public class Track
    {
        public string Album { get; set; }
        public string Artist { get; set; }
        public string Title { get; set; }
        public string CurrentDirectory { get; set; }
        public string NewDirectory { get; set; }

        public Track(string album, string artist, string title ,string currentDirectory, string newDirectory = "")
        {
            Album = album;
            Artist = artist;
            Title = title;
            CurrentDirectory = currentDirectory;
            NewDirectory = newDirectory;
        }
    }
}