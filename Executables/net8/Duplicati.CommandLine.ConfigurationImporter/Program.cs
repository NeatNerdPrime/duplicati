namespace Duplicati.CommandLine.ConfigurationImporter.Net8
{
    // Wrapper class to keep code independent
    public static class Program
    {
        public static int Main(string[] args)
            => Duplicati.CommandLine.ConfigurationImporter.ConfigurationImporter.Main(args);
    }
}